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


        public DiscordClient()
        {
            _gateway = new GatewaySocket();
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
                        d = new
                        {
                            guild_id = guild.Id,
                            channel_id = hotChannel.Id,
                            self_deaf = false,
                            self_mute = true
                        }
                    });
                }
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
