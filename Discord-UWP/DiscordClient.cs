using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace Discord_UWP
{
    public class DiscordClient
    {
        public static readonly string EndpointBase = "https://discordapp.com/api";
        public static readonly string GatewayEndpoint = EndpointBase + "/gateway";

        private MessageWebSocket _gatewaySocket;
        private DataWriter _gatewayWriter;

        public DiscordClient()
        {
            _gatewaySocket = new MessageWebSocket();
            _gatewaySocket.Control.MessageType = SocketMessageType.Utf8;
            _gatewaySocket.MessageReceived += OnMessageReceived;
            _gatewaySocket.Closed += OnSocketClosed;
        }

        public async Task UpdateGateway()
        {
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(new Uri(GatewayEndpoint));

                if (response.IsSuccessStatusCode)
                {
                    var responseData = await response.Content.ReadAsStringAsync();
                    var gateway = JsonObject.Parse(responseData);
                    var gatewayUrl = gateway["url"].GetString();
                    Debug.WriteLine("Got gatway: " + gatewayUrl);

                    var builder = new UriBuilder(gatewayUrl);
                    builder.Query = "v=4&encoding=json";
                    await _gatewaySocket.ConnectAsync(new Uri(gatewayUrl));
                    var handshake = new
                    {
                        op = 2,
                        d = new
                        {
                            token = App.AuthManager.SessionToken,
                            properties = new Dictionary<string, string> {
                                { "$os", "Windows" },
                                { "$browser", "Chrome" },
                                { "$device", "" },
                                { "$referrer", "" },
                                { "$referring_domain", "" }
                            },
                            compress = false,
                            large_threshold = 50,
                            shard = new List<int> { 0, 1 }
                        }
                    };

                    var jsonHandshake = JsonConvert.SerializeObject(handshake);
                    Debug.WriteLine(jsonHandshake);

                    _gatewayWriter = new DataWriter(_gatewaySocket.OutputStream);
                    _gatewayWriter.WriteString(jsonHandshake);
                    await _gatewayWriter.StoreAsync();
                }
            }
        }

        private void OnMessageReceived(MessageWebSocket sender, MessageWebSocketMessageReceivedEventArgs args)
        {
            Debug.WriteLine("msg: " + args.ToString());
        }

        private void OnSocketClosed(IWebSocket sender, WebSocketClosedEventArgs args)
        {
            Debug.WriteLine($"Socket closed with code '{args.Code}' for reason: {args.Reason}");
            App.Current.Exit();
        }
    }
}
