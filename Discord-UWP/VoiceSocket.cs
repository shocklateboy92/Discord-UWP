using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.Sockets;

namespace Discord_UWP
{
    class VoiceSocket : AbstractSocket
    {
        public Uri GatewayUrl { get; set; }
        public string ServerId { get; set; }
        public string UserId { get; set; }
        public string SessionId { get; set; }
        public object Token { get; set; }

        protected override Task<Uri> GetGatewayUrl()
        {
            return Task.FromResult(GatewayUrl);
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

        protected override void OnMessageReceived(JObject msg)
        {
            Debug.WriteLine("voice recv: " + msg.ToString());
        }
    }
}
