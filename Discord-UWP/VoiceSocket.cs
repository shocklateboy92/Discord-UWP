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
using Concentus.Opus.Enums;

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
        public const int NumChannels = 2;
        private DatagramSocket _udpSocket;
        private DataWriter _udpWriter;
        private OpusDecoder _decoder;
        private AudioGraph _audioGraph;
        private AudioFrameInputNode _inputFrame;
        private object _inputFrameLock = new object();
        private AudioDeviceOutputNode _speakersOutNode;
        private ConcurrentQueue<short> _sampleQueue;
        private BinaryWriter _outStream;

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
            _decoder = opus_decoder.opus_decoder_create(48000, 2, error);
            Debug.Assert(error.Val == 0);

            _encoder = opus_encoder.opus_encoder_create(SampleRate, NumChannels, OpusApplication.OPUS_APPLICATION_VOIP, error);
            Debug.Assert(error.Val == 0);

            _sampleQueue = new ConcurrentQueue<short>();

            Task.Run(async () =>
            {
                var graph = await AudioGraph.CreateAsync(
                    new AudioGraphSettings(AudioRenderCategory.Communications)
                    {
                        EncodingProperties = AudioEncodingProperties.CreatePcm(SampleRate, 2, 16),
                    }
                );
                Debug.Assert(graph.Status == AudioGraphCreationStatus.Success);
                _audioGraph = graph.Graph;
                var dev = await _audioGraph.CreateDeviceOutputNodeAsync();
                Debug.Assert(dev.Status == AudioDeviceNodeCreationStatus.Success);
                _speakersOutNode = dev.DeviceOutputNode;

                _inputFrame = _audioGraph.CreateFrameInputNode(
                    AudioEncodingProperties.CreatePcm(
                        SampleRate, NumChannels,
                        sizeof(short) * 8
                    )
                );
                _inputFrame.AddOutgoingConnection(_speakersOutNode);
                _inputFrame.QuantumStarted += _inputFrame_QuantumStarted;
                _audioGraph.Start();

                var g2Result = await AudioGraph.CreateAsync(
                    new AudioGraphSettings(AudioRenderCategory.Communications)
                    {
                        EncodingProperties = AudioEncodingProperties.CreatePcm(SampleRate, 2, 16),
                    }
                );
                Debug.Assert(g2Result.Status == AudioGraphCreationStatus.Success);
                _ouputGraph = g2Result.Graph;
                Debug.Assert(!Equals(_audioGraph, _ouputGraph));
                Debug.Assert(!ReferenceEquals(_audioGraph, _ouputGraph));
                _ouputGraph.Stop();
                var inputNodeResult = await _ouputGraph.CreateDeviceInputNodeAsync(Windows.Media.Capture.MediaCategory.Communications);
                Debug.Assert(inputNodeResult.Status == AudioDeviceNodeCreationStatus.Success);

                _micOutputNode = _ouputGraph.CreateFrameOutputNode(AudioEncodingProperties.CreatePcm(48000, 2, sizeof(short)));
                //_micOutputNode.Stop();
                _ouputGraph.QuantumProcessed += _audioGraph_QuantumProcessed;

                inputNodeResult.DeviceInputNode.AddOutgoingConnection(_micOutputNode);
                _ouputGraph.Start();
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

                await StartSendingVoice();
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
                //var num_channels = opus_decoder.opus_packet_get_nb_channels(packet.GetPointer());
                var num_channels = 2;
                var num_samples = opus_decoder.opus_packet_get_nb_samples(packet.GetPointer(), packetLength, 48000);
                //Log.WriteLine($"{num_frames} frames.");
                //Log.WriteLine($"Data = [{string.Join(", ", packet.Select(x => (int)x))}]");

                int frame_size = 5760;
                var pcm = new short[frame_size * num_channels];

                var processed = opus_decoder.opus_decode(_decoder, packet.GetPointer(), packetLength, pcm.GetPointer(), frame_size, 0);
                if (processed < 0)
                {
                    Log.WriteLine("Failed to decode with error: " + processed);
                }

                uint count = (uint)pcm.Count(c => c != 0);
                if (count > 0)
                {
                    Log.WriteLine($"Got decoded data of length {count}/{processed * sizeof(short)}");
                }

                //var period = processed * 24;
                //for (int i = 0; i < period; i++)
                //{
                //    _packetQueue.Enqueue((short)(short.MaxValue * Math.Sin(((double)i) / period * Math.PI * 2)));
                //}

                for (int i = 0; i < processed * num_channels; i++)
                {
                    _sampleQueue.Enqueue(pcm[i]);
                }

                //try
                //{
                //    lock (_inputFrameLock)
                //    {
                //        ProcessPcmData(pcm, processed * 2, num_samples);
                //    }
                //}
                //catch (Exception ex)
                //{
                //    Log.WriteLine($"Got {ex.GetType().Name} with HResult = {ex.HResult}: {ex.Message}");
                //}
            }
            //Log.WriteLine($"Got SSRC({ssrc}) IP addr: {strAddress}");
            //Log.WriteLine("udp recv: " + data);
        }

        private async Task StartSendingVoice()
        {
            await Task.Delay(3000);
            //await SendMessage(new
            //{
            //    op = 5,
            //    d = new
            //    {
            //        user_id = App.Client.UserId,
            //        ssrc = Ssrc,
            //        speaking = true
            //    }
            //});
            _ouputGraph.Start();
        }

        ConcurrentQueue<short> _outgoingSampleQueue = new ConcurrentQueue<short>();

        private async void _audioGraph_QuantumProcessed(AudioGraph sender, object args)
        {
            try
            {
                //Log.WriteLine($"Attempting to send voice data for {_audioGraph.SamplesPerQuantum} samples");

                var audioFrame = _micOutputNode.GetFrame();
                //var data = GetDataFromFrame(audioFrame);

                //var encodedData = new byte[data.Length * sizeof(short)];
                //var encodedLen = opus_encoder.opus_encode(_encoder, data.GetPointer(), data.Length, encodedData.GetPointer(), encodedData.Length);

                //_udpWriter.WriteByte(0x80);
                //_udpWriter.WriteByte(0x78);

                //_udpWriter.WriteUInt16((ushort)IPAddress.HostToNetworkOrder((short)__sequenceNumber++));
                //_udpWriter.WriteUInt32((uint)IPAddress.HostToNetworkOrder(DateTime.Now.Ticks));
                //_udpWriter.WriteUInt32((uint)IPAddress.HostToNetworkOrder(Ssrc));

                //_udpWriter.WriteBytes(encodedData.Take(encodedLen).ToArray());
                //await _udpWriter.StoreAsync();

                Log.WriteLine($"Sent voice data packet of length ");
            }
            catch (Exception ex)
            {
                Log.LogExceptionCatch(ex);
                _micOutputNode.Start();
            }
        }

        private unsafe short[] GetDataFromFrame(AudioFrame frame)
        {
            using (var buffer = frame.LockBuffer(AudioBufferAccessMode.Read))
            {
                using (var reference = buffer.CreateReference())
                {
                    byte* buf;
                    uint length;
                    ((IMemoryBufferByteAccess)reference).GetBuffer(out buf, out length);
                    short* typedbuf = (short*)buf;

                    var data = new short[length / sizeof(short)];
                    for (int i = 0; i < length / sizeof(short); i++)
                    {
                        //_outgoingSampleQueue.Enqueue(typedbuf[i]);
                        data[i] = typedbuf[i];
                    }
                    return data;
                }
            }
        }

        static int freq = 420;
        double last_rad = 0;
        static double radsPerSample = freq * 2 * Math.PI / SampleRate;
        private AudioFrameOutputNode _micOutputNode;
        private OpusEncoder _encoder;
        private int __sequenceNumber;
        private AudioGraph _ouputGraph;

        private void _inputFrame_QuantumStarted(AudioFrameInputNode sender, FrameInputNodeQuantumStartedEventArgs args)
        {
            if (0 < args.RequiredSamples)
            {
                //for (int i = 0; i < args.RequiredSamples; i++)
                //{
                //    _packetQueue.Enqueue((short)(Math.Sin(last_rad) * short.MaxValue));
                //    _packetQueue.Enqueue((short)(Math.Sin(last_rad) * short.MaxValue));
                //    last_rad += radsPerSample;
                //}
                if (args.RequiredSamples * sender.EncodingProperties.ChannelCount > _sampleQueue.Count) { return; }
                ProcessPcmData(args.RequiredSamples);
            }
        }

        unsafe private void ProcessPcmData(int num_samples)
        {
            var eprop = _inputFrame.EncodingProperties;
            Debug.Assert(eprop.ChannelCount == 2);
            var frame = new AudioFrame((uint)num_samples * eprop.BitsPerSample / 8 * eprop.ChannelCount);
            using (AudioBuffer buffer = frame.LockBuffer(AudioBufferAccessMode.Write))
            {
                using (IMemoryBufferReference reference = buffer.CreateReference())
                {
                    byte* dataInBytes;
                    uint capacityInBytes;
                    ((IMemoryBufferByteAccess)reference).GetBuffer(out dataInBytes, out capacityInBytes);
                    Debug.Assert(capacityInBytes >= num_samples * sizeof(short));

                    short* dataInShort = (short*) dataInBytes;
                    for (int i = 0; i < num_samples * 2; i++)
                    {
                        var success = _sampleQueue.TryDequeue(out dataInShort[i]);
                        Debug.Assert(success);
                    }
                }
            }

            //_packetQueue.Enqueue(frame);
            //if (_packetQueue.Count > 60)
            //{
            //    while (_packetQueue.Count > 0)
            //    {
                    _inputFrame.AddFrame(frame);
            //foreach (short i in pcm.Take(dataLen))
            //{
            //    _outStream.Write(i);
            //}
            //    }
            //}
        }
    }
}
