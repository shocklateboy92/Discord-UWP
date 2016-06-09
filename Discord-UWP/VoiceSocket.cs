using Concentus;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.Storage.Streams;
using Concentus.Structs;
using Concentus.Common.CPlusPlus;
using Windows.Media.Audio;
using Windows.Media.Render;
using Windows.Media.MediaProperties;
using Windows.Media;
using Windows.Foundation;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;

namespace Discord_UWP
{
    [ComImport]
    [Guid("5b0d3235-4dba-4d44-865e-8f1d0e4fd04d")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }

    class VoiceSocket : AbstractSocket
    {
        private const int SampleRate = 48000;
        private DatagramSocket _udpSocket;
        private DataWriter _udpWriter;
        private OpusDecoder _decoder;
        private AudioGraph _audioGraph;
        private AudioFrameInputNode _inputFrame;
        private object _inputFrameLock = new object();
        private AudioDeviceOutputNode _outputNode;
        private Queue<AudioFrame> _packetQueue;

        public string Endpoint { get; set; }
        public string ServerId { get; set; }
        public string UserId { get; set; }
        public string SessionId { get; set; }
        public object Token { get; set; }
        public short? LocalPort { get; private set; }
        public int Ssrc { get; private set; }

        public VoiceSocket()
        {
            // note: do this beforehand
            var error = new BoxedValue<int>(7);
            _decoder = opus_decoder.opus_decoder_create(SampleRate, 2, error);
            Debug.Assert(error.Val == 0);

            _packetQueue = new Queue<AudioFrame>();

            Task.Run(async () => {
                var graph = await AudioGraph.CreateAsync(new AudioGraphSettings(AudioRenderCategory.Communications));
                Debug.Assert(graph.Status == AudioGraphCreationStatus.Success);
                _audioGraph = graph.Graph;
                var dev = await _audioGraph.CreateDeviceOutputNodeAsync();
                Debug.Assert(dev.Status == AudioDeviceNodeCreationStatus.Success);
                _outputNode = dev.DeviceOutputNode;
                //_inputFrame = _audioGraph.CreateFrameInputNode(new AudioEncodingProperties
                //{
                //    ChannelCount = 2,
                //});
            });
        }

        protected override Task<Uri> GetGatewayUrl()
        {
            return Task.FromResult(new Uri("wss://" + Endpoint));
        }

        protected override object GetIdentifyPayload()
        {
            return new
            {
                op = 0,
                d = new
                {
                    server_id = ServerId,
                    user_id = UserId,
                    session_id = SessionId,
                    token = Token
                }
            };
        }

        protected async override void OnMessageReceived(JObject msg)
        {
            Log.WriteLine("voice recv: " + msg.ToString());
            int op = msg.Value<int>("op");

            // Voice ready event
            if (op == 2)
            {
                var eData = msg.GetValue("d").ToObject<VoiceReadyData>();
                BeginHeartbeat(eData.HeartbeatInterval, 3);

                _udpSocket = new DatagramSocket();
                _udpSocket.MessageReceived += OnUdpMessageReceived;
                _udpWriter = new DataWriter(_udpSocket.OutputStream);
                //await _udpSocket.ConnectAsync(
                //    new HostName(Endpoint),
                //    eData.Port.ToString()
                //);
                await _udpSocket.ConnectAsync(new EndpointPair(new HostName("192.168.1.104"), "7771", new HostName(Endpoint), eData.Port.ToString()));


                // Packet to ask the server to send back our (NAT/external) address
                _udpWriter.WriteUInt32(eData.Ssrc);
                _udpWriter.WriteBytes(new byte[70 - sizeof(uint)]);

                await _udpWriter.StoreAsync();
            }

            // Session information
            else if (op == 4)
            {
                await SendMessage(new
                {
                    op = 5,
                    d = new
                    {
                        speaking = true,
                        delay = 0
                    }
                });
            }
        }

        private async void OnUdpMessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            var reader = args.GetDataReader();
            reader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;

            if (LocalPort == null)
            {
                Ssrc = IPAddress.NetworkToHostOrder(reader.ReadInt32());
                Log.WriteLine("Got UDP Packet with SSRC: " + Ssrc);
                var remainingBytes = reader.ReadString(reader.UnconsumedBufferLength - 2);
                var localAddress = string.Join("", remainingBytes.TakeWhile(c => c != '\u0000'));
                Log.WriteLine("Localhost = " + localAddress);

                LocalPort = IPAddress.NetworkToHostOrder((short)reader.ReadUInt16());
                Log.WriteLine("LocalPort = " + LocalPort);

                await SendMessage(new
                {
                    op = 1,
                    d = new
                    {
                        protocol = "udp",
                        data = new
                        {
                            address = localAddress,
                            port = 7771,
                            mode = "plain"
                        },
                    }
                });
            }
            else
            {
                var header = new byte[12];
                reader.ReadBytes(header);
                if (header[0] != 0x80) return; //flags
                if (header[1] != 0x78) return; //payload type. you know, from before.

                ushort sequenceNumber = (ushort)((header[2] << 8) | header[3] << 0);
                uint timDocuestamp = (uint)((header[4] << 24) | header[5] << 16 | header[6] << 8 | header[7] << 0);
                uint ssrc = (uint)((header[8] << 24) | (header[9] << 16) | (header[10] << 8) | (header[11] << 0));

                int packetLength = (int)reader.UnconsumedBufferLength;
                byte[] packet = new byte[packetLength];
                reader.ReadBytes(packet);

                if (packetLength < 12)
                {
                    return;
                }

                Log.WriteLine($"Decoding voice data: FromSsrc = {ssrc}, SequenceNumber = {sequenceNumber}, Timestamp = {timDocuestamp}, PacketSize = {packetLength}");
                var num_frames = opus_decoder.opus_packet_get_nb_frames(packet.GetPointer(), packetLength);
                var num_channels = opus_decoder.opus_packet_get_nb_channels(packet.GetPointer());
                var num_samples = opus_decoder.opus_packet_get_nb_samples(packet.GetPointer(), packetLength, SampleRate);
                //Log.WriteLine($"{num_frames} frames.");
                //Log.WriteLine($"Data = [{string.Join(", ", packet.Select(x => (int)x))}]");

                int frame_size = 5760;
                var pcm = new short[frame_size * 2];

                var processed = opus_decoder.opus_decode(_decoder, packet.GetPointer(), packetLength, pcm.GetPointer(), frame_size, 0);
                if (processed < 0)
                {
                    Log.WriteLine("Failed to decode with error: " + processed);
                }

                uint count = (uint)pcm.Count(c => c != 0);
                if (count > 0)
                {
                    Log.WriteLine($"Got decoded data of length {count}/{processed * 2}");
                }

                if (_inputFrame == null)
                {
                    lock (_inputFrameLock)
                    {
                        if (_inputFrame == null)
                        {
                            _inputFrame = _audioGraph.CreateFrameInputNode(
                                AudioEncodingProperties.CreatePcm(
                                    SampleRate, (uint)num_channels,
                                    (uint)(processed * 2 / num_samples * 8)
                                )
                            );
                            _inputFrame.AddOutgoingConnection(_outputNode);
                            _audioGraph.Start();
                        }
                    }
                }

                try
                {
                    lock (_inputFrameLock)
                    {
                        ProcessPcmData(pcm, processed * 2, num_samples);
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteLine($"Got {ex.GetType().Name} with HResult = {ex.HResult}: {ex.Message}");
                }
            }
            //Log.WriteLine($"Got SSRC({ssrc}) IP addr: {strAddress}");
            //Log.WriteLine("udp recv: " + data);
        }

        unsafe private void ProcessPcmData(short[] pcm, int dataLen, int num_samples)
        {
            var eprop = _inputFrame.EncodingProperties;
            var frame = new AudioFrame((uint)dataLen);
            using (AudioBuffer buffer = frame.LockBuffer(AudioBufferAccessMode.Write))
            {
                using (IMemoryBufferReference reference = buffer.CreateReference())
                {
                    byte* dataInBytes;
                    uint capacityInBytes;
                    ((IMemoryBufferByteAccess)reference).GetBuffer(out dataInBytes, out capacityInBytes);
                    //Debug.Assert(capacityInBytes > dataLen);

                    Marshal.Copy(pcm, 0, (IntPtr)dataInBytes, dataLen / 2);
                }
            }

            //_packetQueue.Enqueue(frame);
            //if (_packetQueue.Count > 60)
            //{
            //    while (_packetQueue.Count > 0)
            //    {
                    _inputFrame.AddFrame(frame);
            //    }
            //}
        }
    }
}
