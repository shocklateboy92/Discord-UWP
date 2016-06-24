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

        public string Endpoint { get; set; }
        public string ServerId { get; set; }
        public string UserId { get; set; }
        public string SessionId { get; set; }
        public object Token { get; set; }
        public short? LocalPort { get; private set; }
        public uint Ssrc { get; private set; }

        public VoiceSocket()
        {
            _dataManager = new VoiceDataManager();
            Task.Run(_dataManager.Initialize);
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

                if (!eData.Modes.Contains("plain"))
                {
                    Log.Error("Server requires encryption. Currently not supported");
                    return;
                }

                Ssrc = eData.Ssrc;
                _dataSocket = new VoiceDataSocket
                {
                    Ssrc = eData.Ssrc
                };
                _dataSocket.Ready += async (o, args) =>
                {
                    await SendMessage(new
                    {
                        op = 1,
                        d = new
                        {
                            protocol = "udp",
                            data = new
                            {
                                address = args.Address,
                                port = 7771,
                                mode = "plain"
                            },
                        }
                    });
                };
                _dataSocket.PacketReceived += (o, args) =>
                {
                    _dataManager.ProcessIncomingData(args.Data, args.Ssrc);
                };
                await _dataSocket.Initialize(Endpoint, eData.Port);
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


        private async Task StartSendingVoice()
        {
            await Task.Delay(1000);
            _dataManager.OutgoingDataReady += _audioGraph_QuantumProcessed;
            _dataManager.StartOutgoingAudio();
        }

        private async void _audioGraph_QuantumProcessed(object sender, VoiceDataManager.VoicePacket args)
        {
            try
            {
                //var voiceData = args.Data;
                //if (voiceData == null)
                //{
                //    return;
                //}

                //_udpWriter.WriteByte(0x80);
                //_udpWriter.WriteByte(0x78);

                //_udpWriter.WriteUInt16(___sequence++);
                //_udpWriter.WriteUInt32(___timestamp);
                //_udpWriter.WriteUInt32((uint) Ssrc);
                //_udpWriter.WriteBytes(voiceData);
                //await _udpWriter.StoreAsync();

                //___timestamp += (uint) args.FrameSize;
            }
            catch (Exception ex)
            {
                Log.LogExceptionCatch(ex);
            }
        }

        private ushort ___sequence;
        private uint ___timestamp;
        private VoiceDataManager _dataManager;
        private VoiceDataSocket _dataSocket;
    }
}
