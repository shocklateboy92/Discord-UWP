using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Security.Authentication.Web;
using System.Text.RegularExpressions;
using System.IO;
using System.Net.Http;
using Windows.Data.Json;

namespace Discord_UWP
{
    public class AuthenticationManager
    {
        private static readonly string OAuthEndpoint = "https://discordapp.com/oauth2/authorize";
        private static readonly string OAuthRedirect = "http://home.lasath.org";
        private static readonly string OAuthAppId = "181594107653652480";
        private static readonly string OAuthAppSecret = "ua9SUGpzEA9DpaxIipSSmQQTJBXYK4fL";

        private static readonly IList<string> OAuthScopes = new List<string>
        {
            "identify",
            "email",
            "connections",
            "guilds",
            "guilds.join"
        };

        private readonly IDictionary<string, string> OAuthQueryParams = new Dictionary<string, string>
        {
            { "response_type", "token" },
            { "client_id", OAuthAppId },
            { "client_secret", OAuthAppSecret },
            {
                "redirect_url",
                Uri.EscapeUriString(OAuthRedirect)
            },
            {
                "scope",
                Uri.EscapeDataString(
                    string.Join(" ", OAuthScopes)
                )
            }
        };

        public string SessionToken { get; private set; }

        public async Task DoAuthentication()
        {
            var file = File.OpenText("credentials.json");

            var client = new HttpClient();
            var body = new StreamContent(file.BaseStream);
            body.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            var response = await client.PostAsync("https://discordapp.com/api/auth/login", body);

            if (response.IsSuccessStatusCode)
            {
                var responseObject = JsonObject.Parse(await response.Content.ReadAsStringAsync());
                var token = responseObject.GetNamedString("token");
                Debug.WriteLine("Got token: " + token);
                SessionToken = token;
            }
            else
            {
                string content = "";
                if (response.Content != null)
                {
                    content = await response.Content.ReadAsStringAsync();
                }

                throw new AuthenticationException(
                    string.Format(
                        "auth request returned code {0} ({1}): {2}",
                        response.StatusCode,
                        response.ReasonPhrase, content
                    )
                );
            }
        }
    }
}
