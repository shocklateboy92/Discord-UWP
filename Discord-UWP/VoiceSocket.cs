using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace Discord_UWP
{
    class VoiceSocket : AbstractSocket
    {
        private DatagramSocket _udpSocket;
        private DataWriter _udpWriter;

        public string Endpoint { get; set; }
        public string ServerId { get; set; }
        public string UserId { get; set; }
        public string SessionId { get; set; }
        public object Token { get; set; }

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
            Debug.WriteLine("voice recv: " + msg.ToString());
            int op = msg.Value<int>("op");

            // Voice ready event
            if (op == 2)
            {
                var eData = msg.ToObject<VoiceReadyData>();
                BeginHeartbeat(eData.HeartbeatInterval, 3);

                _udpSocket = new DatagramSocket();
                _udpSocket.MessageReceived += OnUdpMessageReceived;
                _udpWriter = new DataWriter(_udpSocket.OutputStream);
                await _udpSocket.ConnectAsync(
                    new EndpointPair(
                        new HostName("192.168.1.104"),
                        "7771",
                        new HostName(Endpoint),
                        eData.Port.ToString()
                    )
                );

                //_udpWriter.WriteByte(0x80); // type
                //_udpWriter.WriteByte(0x78); // version
                //_udpWriter.WriteUInt16(0);  // sequence
                //_udpWriter.WriteUInt32(unchecked((uint)TimeSpan.FromTicks(DateTime.Now.Ticks).TotalMilliseconds));

                //_udpWriter.WriteUInt32(eData.Ssrc);
                //_udpWriter.WriteBytes(new byte[70 - sizeof(uint)]);

                //await _udpWriter.StoreAsync();

                await SendMessage(new
                {
                    op = 1,
                    d = new
                    {
                        protocol = "udp",
                        data = new
                        {
                            address = "home.lasath.org",
                            port = 7001,
                            mode = "plain"
                        },
                    }
                });
            }
        }

        private void OnUdpMessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            var reader = args.GetDataReader();
            var data = reader.ReadString(reader.UnconsumedBufferLength);
            Debug.WriteLine("udp recv: " + data);
        }
    }
}
