using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Security.Authentication.Web;
using System.Text.RegularExpressions;

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
            UriBuilder builder = new UriBuilder(OAuthEndpoint);
            Debug.Assert(string.IsNullOrEmpty(builder.Query));

            builder.Query = 
                string.Join(
                    "&", 
                    OAuthQueryParams.Select(
                        (x) => $"{x.Key}={x.Value}"
                    )
                );

            Debug.WriteLine("Making OAuth Request: " + builder.Uri.OriginalString);

            var authResult = await WebAuthenticationBroker.AuthenticateAsync(
                WebAuthenticationOptions.None, builder.Uri, new Uri(OAuthRedirect));

            if (authResult.ResponseStatus == WebAuthenticationStatus.Success)
            {
                Uri resultUrl = new Uri(authResult.ResponseData);
                var match = Regex.Match(resultUrl.Fragment, "access_token=([^&]+)");
                if (match.Success)
                {
                    var ticket = match.Groups[1].Value;
                    Debug.WriteLine("Got Ticket: " + ticket);
                    SessionToken = ticket;
                }
            } else
            {
                throw new AuthenticationException(
                    string.Format(
                        "OAuth endpoint returned {0}: {1}",
                        authResult.ResponseErrorDetail,
                        authResult.ResponseData
                    )
                );
            }
        }
    }
}
