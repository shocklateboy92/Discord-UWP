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
        public static readonly string GatewayEndpoint = EndpointBase + "/gateway";

        private MessageWebSocket _gatewaySocket;
        private DataWriter _gatewayWriter;

        private Timer _heartbeatTimer;

        public DiscordClient()
        {
            _gatewaySocket = new MessageWebSocket();
            _gatewaySocket.Control.MessageType = SocketMessageType.Utf8;
            _gatewaySocket.MessageReceived += OnMessageReceived;
            _gatewaySocket.Closed += OnSocketClosed;

            _heartbeatTimer = new Timer(
                new TimerCallback(SendHeartbeat),
                null,
                Timeout.Infinite,
                Timeout.Infinite
            );
        }

        public async Task UpdateGateway()
        {
            using (var client = new HttpClient())
            {
                // Get the current gateway URL
                // TODO: cache this on disk somewhere
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
                            v = 4,
                            token = App.AuthManager.SessionToken,
                            properties = new Dictionary<string, string> {
                                { "$os", "Windows" },
                                { "$browser", "" },
                                { "$device", "Windows_Phone" },
                                { "$referrer", "" },
                                { "$referring_domain", "" }
                            },
                            compress = false,
                        }
                    };

                    await SendMessage(handshake);
                }
            }
        }

        private async Task SendMessage(object handshake)
        {
            var jsonHandshake = JsonConvert.SerializeObject(handshake);
            Debug.WriteLine(jsonHandshake);

            if (_gatewayWriter == null)
            {
                _gatewayWriter = new DataWriter(_gatewaySocket.OutputStream);
            }
            _gatewayWriter.WriteString(jsonHandshake);
            await _gatewayWriter.StoreAsync();
        }

        public void CloseSocket()
        {
            Debug.WriteLine("Closing socket...");
            _gatewaySocket.Dispose();
        }

        private void OnMessageReceived(MessageWebSocket sender, MessageWebSocketMessageReceivedEventArgs args)
        {
            var reader = args.GetDataReader();
            var data = reader.ReadString(reader.UnconsumedBufferLength);
            var msgBase = JsonObject.Parse(data);

            if (msgBase.GetNamedString("t") == "READY")
            {
                Debug.WriteLine("Got ready message!");
                MessageFormat msg = JsonConvert.DeserializeObject<MessageFormat>(data);

                Debug.WriteLine("Getting ready for heartbeat interval: " + msg.D.HeartbeatInterval);
                _heartbeatTimer.Change(0, msg.D.HeartbeatInterval);
            } else
            {
                Debug.WriteLine(data);
            }
        }

        private void OnSocketClosed(IWebSocket sender, WebSocketClosedEventArgs args)
        {
            Debug.WriteLine($"Socket closed with code '{args.Code}' for reason: {args.Reason}");

            // Currenly we don't have any logic anywhere in the App to re-open
            // the socket once it's closed. So we may as well exit if this happens.
            App.Current.Exit();
        }

        private async void SendHeartbeat(object state)
        {
            Debug.WriteLine("sending heartbeat...");
            await SendMessage(new { op = 1, d = 251 });
        }
    }
}
