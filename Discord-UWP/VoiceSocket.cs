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
using Concentus.Enums;
using System.Threading;

namespace Discord_UWP
{
    class VoiceSocket : AbstractSocket
    {
        private const int SampleRate = 48000;
        public const int NumChannels = 1;
        private DatagramSocket _udpSocket;
        private DataWriter _udpWriter;
        private AudioGraph _audioGraph;
        private AudioDeviceOutputNode _speakersOutNode;
        private ConcurrentQueue<short> _sampleQueue;

        public string Endpoint { get; set; }
        public string ServerId { get; set; }
        public string UserId { get; set; }
        public string SessionId { get; set; }
        public object Token { get; set; }
        public short? LocalPort { get; private set; }
        public int Ssrc { get; private set; }

        public VoiceSocket()
        {
            CreateEncoder();

            _sampleQueue = new ConcurrentQueue<short>();

            Task.Run(async () =>
            {
                var graph = await AudioGraph.CreateAsync(
                    new AudioGraphSettings(AudioRenderCategory.Communications)
                    {
                        EncodingProperties = AudioEncodingProperties.CreatePcm(SampleRate, VoiceDecoder.NumChannels, 16),
                    }
                );
                Debug.Assert(graph.Status == AudioGraphCreationStatus.Success);
                _audioGraph = graph.Graph;
                var dev = await _audioGraph.CreateDeviceOutputNodeAsync();
                Debug.Assert(dev.Status == AudioDeviceNodeCreationStatus.Success);
                _speakersOutNode = dev.DeviceOutputNode;

                _default = new VoiceDecoder(_audioGraph, Ssrc);
                _default.Node.AddOutgoingConnection(_speakersOutNode);

                _audioGraph.Start();

                var g2Result = await AudioGraph.CreateAsync(
                    new AudioGraphSettings(AudioRenderCategory.Communications)
                    {
                        EncodingProperties = AudioEncodingProperties.CreatePcm(SampleRate, NumChannels, 16),
                    }
                );
                Debug.Assert(g2Result.Status == AudioGraphCreationStatus.Success);
                _ouputGraph = g2Result.Graph;
                Debug.Assert(!Equals(_audioGraph, _ouputGraph));
                Debug.Assert(!ReferenceEquals(_audioGraph, _ouputGraph));
                var inputNodeResult = await _ouputGraph.CreateDeviceInputNodeAsync(Windows.Media.Capture.MediaCategory.Communications);
                Debug.Assert(inputNodeResult.Status == AudioDeviceNodeCreationStatus.Success);

                _micOutputNode = _ouputGraph.CreateFrameOutputNode();
                _ouputGraph.QuantumProcessed += _audioGraph_QuantumProcessed;

                inputNodeResult.DeviceInputNode.AddOutgoingConnection(_micOutputNode);

                //var dev2 = await _ouputGraph.CreateDeviceOutputNodeAsync();
                //Debug.Assert(dev2.Status == AudioDeviceNodeCreationStatus.Success);

                //_frameInputNode = _ouputGraph.CreateFrameInputNode(AudioEncodingProperties.CreatePcm(SampleRate, NumChannels, 16));
                //_frameInputNode.AddOutgoingConnection(dev2.DeviceOutputNode);
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

                //await _audioGraph_QuantumProcessed(null, null);
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
                _default.ProcessPacket(packet);
            }
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
            ticker = new Timer((o) => _audioGraph_QuantumProcessed(null, null), null, 0, targetFrameSize * 1000 / SampleRate);
        }
        Timer ticker;

        ConcurrentQueue<short> _outgoingSampleQueue = new ConcurrentQueue<short>();

                int targetFrameSize = 1920 * NumChannels;
        private void CreateEncoder()
        {
            var error = new BoxedValue<int>(7);
            _encoder = OpusEncoder.Create(SampleRate, NumChannels, OpusApplication.OPUS_APPLICATION_AUDIO, error);
            Debug.Assert(error.Val == 0);
        }

        private static readonly int freq = 440;
        private double _lastRad = 0;
        private static readonly double _radInc = freq * Math.PI * 2 / SampleRate;
        private object _encoderLock = new object();
        int[] SupportedFrameSizes = { 120, 240, 480, 960, 1920 };
        private void _audioGraph_QuantumProcessed(AudioGraph sender, object args)
        {
            try
            {
                //Log.WriteLine($"Attempting to send voice data for {_audioGraph.SamplesPerQuantum} samples");

                var audioFrame = _micOutputNode.GetFrame();
                Debug.Assert(_micOutputNode.EncodingProperties.ChannelCount == NumChannels);
                //Log.WriteLine($"Got frame of type {_micOutputNode.EncodingProperties.Subtype} with {_micOutputNode.EncodingProperties.BitsPerSample} bits");
                var data = GetDataFromFrame(audioFrame);

                if (data.Length == 0)
                {
                    Log.WriteLine("Got Empty frame");
                    return;
                }

                //int frameSize = (int) Math.Round((double) SampleRate * (60/1000) * NumChannels);
                //var data = new short[frameSize];
                //for (int i = 0; i < frameSize; i++)
                //{
                //    data[i] = (short) (Math.Sin(_lastRad) * short.MaxValue);
                //    //if (i % 2 == 0)
                //    _lastRad += _radInc;
                //}
                Log.WriteLine($"Got frame of length {data.Length}");
                int frameSize = 0;
                foreach (var size in SupportedFrameSizes)
                {
                    if (size <= data.Length)
                    {
                        frameSize = size;
                    }
                }

                var encodedData = new byte[data.Length * sizeof(short) * NumChannels];
                //var encodedLen = opus_encoder.opus_encode(_encoder, data.GetPointer(), frameSize, encodedData.GetPointer(), encodedData.Length * 2);
                int encodedLen;
                //lock (_encoderLock)
                //{
                    encodedLen = _encoder.Encode(data, 0, frameSize, encodedData, 0, encodedData.Length);
                //}

                //Debug.Assert(encodedData.Any(x => x != 0));
                //Debug.Assert(encodedData.Count(x => x != 0) == encodedLen);
                //for (int i = encodedLen; i <encodedData.Length; i++)
                //{
                //    Debug.Assert(encodedData[i] == 0);
                //}
                while (encodedData[encodedLen] != 0)
                {
                    encodedLen++;
                }

                //_default.ProcessPacket(encodedData.Take(encodedLen).ToArray());
                //var frame = VoiceDecoder.CreateFrame(data, data.Length / VoiceDecoder.NumChannels);
                //_frameInputNode.AddFrame(frame);

                byte[] header = new byte[12];

                header[0] = (byte)0x80; //flags
                header[1] = (byte)0x78; //flags

                header[8] = (byte)((Ssrc >> 24) & 0xFF); //ssrc
                header[9] = (byte)((Ssrc >> 16) & 0xFF); //ssrc
                header[10] = (byte)((Ssrc >> 8) & 0xFF); //ssrc
                header[11] = (byte)((Ssrc >> 0) & 0xFF); //ssrc

                //sequence big endian
                header[2] = (byte)((___sequence >> 8));
                header[3] = (byte)((___sequence >> 0) & 0xFF);
                ___sequence++;

                //timestamp big endian
                header[4] = (byte)((___timestamp >> 24) & 0xFF);
                header[5] = (byte)((___timestamp >> 16) & 0xFF);
                header[6] = (byte)((___timestamp >> 8));
                header[7] = (byte)((___timestamp >> 0) & 0xFF);

                //_udpWriter.WriteByte(0x80);
                //_udpWriter.WriteByte(0x78);

                //_udpWriter.WriteUInt16((ushort)IPAddress.HostToNetworkOrder((short)___sequence++));
                ////_udpWriter.WriteUInt32((uint)IPAddress.HostToNetworkOrder((uint)audioFrame.RelativeTime?.TotalMilliseconds));
                //_udpWriter.WriteUInt32((uint)IPAddress.HostToNetworkOrder(___timestamp));
                //_udpWriter.WriteUInt32((uint)IPAddress.HostToNetworkOrder(Ssrc));

                _udpWriter.WriteBytes(header);
                _udpWriter.WriteBytes(encodedData.Take(encodedLen).ToArray());
                _udpWriter.StoreAsync().AsTask().Wait();

                ___timestamp += (uint) frameSize;
                //Log.WriteLine($"Sent voice data packet of length ");
            }
            catch (Exception ex)
            {
                Log.LogExceptionCatch(ex);
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
                    float* typedbuf = (float*)buf;
                    int samples = (int) (length / sizeof(float));

                    var data = new short[samples];
                    for (int i = 0; i < samples; i++)
                    {
                        //_outgoingSampleQueue.Enqueue(typedbuf[i]);
                        data[i] = (short) (typedbuf[i] * short.MaxValue);
                        //data[i * 2 + 1] = typedbuf[i];
                    }
                    return data;
                }
            }
        }

        private AudioFrameOutputNode _micOutputNode;
        private OpusEncoder _encoder;
        private AudioGraph _ouputGraph;
        private VoiceDecoder _default;
        private int ___sequence;
        private uint ___timestamp;
        private AudioFrameInputNode _frameInputNode;
    }
}
