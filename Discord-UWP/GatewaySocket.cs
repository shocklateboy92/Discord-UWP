using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Networking.Sockets;
using Windows.Web.Http;

namespace Discord_UWP
{
    class GatewaySocket : AbstractSocket
    {
        public static readonly string GatewayEndpoint = DiscordClient.EndpointBase + "/gateway";

        public delegate void InitialStateHandler(D initialState);
        public event InitialStateHandler InitialStateReceived;

        protected override async Task<Uri> GetGatewayUrl()
        {
            using (var client = new HttpClient())
            {
                // Get the current gateway URL
                // TODO: cache this on disk somewhere
                var response = await client.GetAsync(new Uri(GatewayEndpoint));
                response.EnsureSuccessStatusCode();

                var gatewayUrl = JsonObject
                    .Parse(await response.Content.ReadAsStringAsync())
                    .GetNamedString("url");
                Debug.WriteLine("Got gatway: " + gatewayUrl);

                return new Uri(gatewayUrl);
            }
        }

        protected override object GetIdentifyPayload()
        {
            return new
            {
                op = 2,
                d = new
                {
                    v = 4,
                    token = App.AuthManager.SessionToken,
                    properties = new Dictionary<string, string> {
                                { "$os", "Windows" },
                                { "$browser", "" },
                                { "$device", "Windows_Phone" },
                                { "$referrer", "" },
                                { "$referring_domain", "" }
                            },
                    compress = false
                }
            };
        }

        private VoiceSocket _voice;

        protected override void OnMessageReceived(MessageWebSocket sender, MessageWebSocketMessageReceivedEventArgs args)
        {
            var reader = args.GetDataReader();
            var data = reader.ReadString(reader.UnconsumedBufferLength);
            var msgBase = JObject.Parse(data);

            var type = msgBase.GetValue("t").ToString();
            var rawEvent = msgBase.GetValue("d");
            var doConnect = false;
            switch (type)
            {
                case "READY":
                    var state = rawEvent.ToObject<D>();
                    BeginHeartbeat(state.HeartbeatInterval, opCode: 1);
                    InitialStateReceived?.Invoke(state);
                    break;
                case "VOICE_STATE_UPDATE":
                    Debug.WriteLine("gateway recv: " + data);
                    var voiceState = rawEvent.ToObject<VoiceStateUpdate>();
                    if (_voice != null)
                    {
                        // Other event has already happened
                        doConnect = true;
                    }
                    else
                    {
                        _voice = new VoiceSocket();
                    }
                    _voice.UserId = voiceState.UserId;
                    _voice.SessionId = voiceState.SessionId;
                    _voice.ServerId = voiceState.GuildId;
                    break;
                case "VOICE_SERVER_UPDATE":
                    Debug.WriteLine("gateway recv: " + data);
                    if (_voice != null)
                    {
                        doConnect = true;
                    }
                    else
                    {
                        _voice = new VoiceSocket();
                    }
                    var voiceServer = rawEvent.ToObject<VoiceServerUpdate>();
                    _voice.GatewayUrl = new Uri("wss://" + voiceServer.Endpoint.Remove(voiceServer.Endpoint.Length - 3));
                    _voice.Token = voiceServer.Token;
                    break;
            }
            if (doConnect)
            {
                Task.Run(_voice.BeginConnection);
            }
        }
    }
}
