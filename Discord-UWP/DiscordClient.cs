using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace Discord_UWP
{
    public class DiscordClient
    {
        public static readonly string EndpointBase = "https://discordapp.com/api";
        public static readonly string GateWayEndpoint = EndpointBase + "/gateway";

        public async Task UpdateGateway()
        {
            using (var client = new HttpClient())
            {
                UriBuilder builder = new UriBuilder(GateWayEndpoint);
                builder.Query = $"access_token={App.AuthManager.SessionToken}";

                var response = await client.GetAsync(builder.Uri);

                if (response.IsSuccessStatusCode)
                {
                    var jsonData = await response.Content.ReadAsStringAsync();
                    var gateway = JsonObject.Parse(jsonData);
                    var gatewayUrl = gateway["url"];
                    Debug.WriteLine("Got gatway: " + gatewayUrl);
                }
            }
        }
    }
}
