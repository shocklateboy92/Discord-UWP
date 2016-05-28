using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace Discord_UWP
{
    public class DiscordClient
    {
        public static readonly string EndpointBase = "https://discordapp.com/api";

        private GatewaySocket _gateway;
        private VoiceSocket _voice;

        public DiscordClient()
        {
            _gateway = new GatewaySocket
            {
                MessageHandlers = new Dictionary<string, IMessageHandler>
                {
                    {"VOICE_STATE_UPDATE", new MessageHandler<VoiceStateUpdate>(OnVoiceStateUpdate)},
                    {"VOICE_SERVER_UPDATE", new MessageHandler<VoiceServerUpdate>(OnVoiceServerUpdate)}
                }
            };
            _gateway.InitialStateReceived += OnInitialStateReceived;
        }

        private async void OnInitialStateReceived(D initialState)
        {
            foreach (var guild in initialState.Guilds)
            {
                var voiceChannels = guild.Channels.Where(c => string.Compare(c.Type, "voice", ignoreCase: true) == 0).Select(c => $"'{c.Name}' ({c.Id})");
                Debug.WriteLine($"found guild: '{guild.Name}' ({guild.Id}) with voice channels: {string.Join(", ", voiceChannels)}");

                var hotChannel = guild.Channels.FirstOrDefault(c => c.Id == "184883715053322241");

                if (hotChannel != null)
                {
                    await _gateway.SendMessage(new
                    {
                        op = 4,
                        d = new VoiceStateUpdate
                        {
                            GuildId = guild.Id,
                            ChannelId = hotChannel.Id,
                            SelfDeaf = false,
                            SelfMute = true
                        }
                    });
                }
            }
        }

        private void OnVoiceStateUpdate(VoiceStateUpdate voiceState)
        {
            bool doConnect = false;
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

            if (doConnect)
            {
                Task.Run(_voice.BeginConnection);
            }
        }

        private void OnVoiceServerUpdate(VoiceServerUpdate voiceServer)
        {
            bool doConnect = false;
            if (_voice != null)
            {
                doConnect = true;
            }
            else
            {
                _voice = new VoiceSocket();
            }
            _voice.GatewayUrl = new Uri("wss://" + voiceServer.Endpoint.Remove(voiceServer.Endpoint.Length - 3));
            _voice.Token = voiceServer.Token;

            if (doConnect)
            {
                Task.Run(_voice.BeginConnection);
            }
        }

        public async Task UpdateGateway()
        {
            await _gateway.BeginConnection();
        }

        public void CloseSockets()
        {
            Debug.WriteLine("Closing socket...");
            _gateway.CloseSocket();
        }
    }
}
