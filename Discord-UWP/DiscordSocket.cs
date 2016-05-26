using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace Discord_UWP
{
    public abstract class AbstractSocket
    {

        private Uri _endpointAddress;

        private MessageWebSocket _gatewaySocket;
        private DataWriter _gatewayWriter;

        private Timer _heartbeatTimer;
        private int _heartbeatCount;

        public AbstractSocket()
        {
            _gatewaySocket = new MessageWebSocket();
            _gatewaySocket.Control.MessageType = SocketMessageType.Utf8;
            _gatewaySocket.MessageReceived += OnMessageReceived;
            _gatewaySocket.Closed += OnSocketClosed;

            _heartbeatCount = 0;
        }

        public delegate void MessageHandler(string message);

        public event MessageHandler MessageReceived;

        private void OnSocketClosed(IWebSocket sender, WebSocketClosedEventArgs args)
        {
            Debug.WriteLine($"Socket closed with code '{args.Code}' for reason: {args.Reason}");

            // Currenly we don't have any logic anywhere in the App to re-open
            // the socket once it's closed. So we may as well exit if this happens.
            App.Current.Exit();
        }

        protected void BeginHeartbeat(int interval, int opCode)
        {
            _heartbeatTimer = new Timer(
                new TimerCallback(async (o) => await SendMessage(
                    new { op = opCode, d = _heartbeatCount++ })
                ),
                null,
                interval,
                interval
            );
        }

        protected abstract Task<Uri> GetGatewayUrl();

        protected abstract object GetIdentifyPayload();

        protected abstract void OnMessageReceived(
            MessageWebSocket sender, 
            MessageWebSocketMessageReceivedEventArgs args);

        public async Task BeginConnection()
        {
            await _gatewaySocket.ConnectAsync(await GetGatewayUrl());
            await SendMessage(GetIdentifyPayload());
        }

        public void CloseSocket()
        {
            _gatewaySocket.Dispose();
        }

        /// <summary>
        /// Serializes a given object into JSON and sends it down the WebSocket.
        /// </summary>
        /// <param name="handshake"></param>
        /// <returns></returns>
        public async Task SendMessage(object handshake)
        {
            var jsonHandshake = JsonConvert.SerializeObject(handshake);
            Debug.WriteLine(jsonHandshake);

            if (_gatewayWriter == null)
                _gatewayWriter = new DataWriter(_gatewaySocket.OutputStream);
            _gatewayWriter.WriteString(jsonHandshake);
            await _gatewayWriter.StoreAsync();
        }
    }
}
