using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using Windows.Data.Json;

namespace Discord_UWP
{
    public class AuthenticationManager
    {
        private static readonly string LoginEndpoint = "https://discordapp.com/api/auth/login";

        public string SessionToken { get; private set; }

        public async Task DoAuthentication()
        {
            var file = File.OpenText("credentials.json");

            var body = new StreamContent(file.BaseStream);
            body.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            // According to the fiddler trace of the offical client,
            // all we need to do is make a request to the login endpoint
            var client = new HttpClient();
            var response = await client.PostAsync(LoginEndpoint, body);

            if (response.IsSuccessStatusCode)
            {
                var responseObject = JsonObject.Parse(await response.Content.ReadAsStringAsync());
                var token = responseObject.GetNamedString("token");
                Debug.WriteLine("Got token: " + token);
                SessionToken = token;
            }
            else
            {
                // Try to do something nice with the error
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
