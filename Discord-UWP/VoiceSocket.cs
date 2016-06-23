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
        public const int BitsPerSample = 16;
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
                        EncodingProperties = AudioEncodingProperties.CreatePcm(
                            SampleRate,
                            VoiceDecoder.NumChannels,
                            BitsPerSample),
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

                graph = await AudioGraph.CreateAsync(
                    new AudioGraphSettings(AudioRenderCategory.Communications)
                    {
                        EncodingProperties = AudioEncodingProperties.CreatePcm(SampleRate, NumChannels, 16),
                    }
                );
                Debug.Assert(graph.Status == AudioGraphCreationStatus.Success);
                _ouputGraph = graph.Graph;
                var inputNodeResult = await _ouputGraph.CreateDeviceInputNodeAsync(Windows.Media.Capture.MediaCategory.Communications);
                Debug.Assert(inputNodeResult.Status == AudioDeviceNodeCreationStatus.Success);

                _voiceInput = new VoiceEncoder(_ouputGraph);
                inputNodeResult.DeviceInputNode.AddOutgoingConnection(_voiceInput.Node);
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
            await Task.Delay(1000);
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
            //_ouputGraph.QuantumProcessed += _audioGraph_QuantumProcessed;
            _ouputGraph.Start();
            //Log.WriteLine("Attempting to transmit at interval " + VoiceEncoder.DesiredProcessingInterval);
            ticker = new Timer((o) => _audioGraph_QuantumProcessed(null, null), null, 0, VoiceEncoder.DesiredProcessingInterval);
        }
        Timer ticker;

        ConcurrentQueue<short> _outgoingSampleQueue = new ConcurrentQueue<short>();

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
        private async void _audioGraph_QuantumProcessed(AudioGraph sender, object args)
        {
            try
            {

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
                int targetFrameSize = 1920 * NumChannels;
                var voiceData = _voiceInput.GetDataPacket(out targetFrameSize);
                _udpWriter.WriteBytes(voiceData);
                await _udpWriter.StoreAsync();

                ___timestamp += (uint) targetFrameSize;
                //Log.WriteLine($"Sent voice data packet of length ");
            }
            catch (Exception ex)
            {
                Log.LogExceptionCatch(ex);
            }
        }

        private AudioFrameOutputNode _micOutputNode;
        private OpusEncoder _encoder;
        private AudioGraph _ouputGraph;
        private VoiceDecoder _default;
        private int ___sequence;
        private uint ___timestamp;
        private AudioFrameInputNode _frameInputNode;
        private VoiceEncoder _voiceInput;
    }
}
