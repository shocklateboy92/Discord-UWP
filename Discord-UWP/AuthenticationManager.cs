﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using Windows.Data.Json;
using Windows.Security.Credentials;
using Newtonsoft.Json;
using System.Runtime.InteropServices;

namespace Discord_UWP
{
    public class AuthenticationManager
    {
        public enum AuthenticationState
        {
            Authenticated,
            InProgress,
            UserInteractionRequired,
            Failed
        }

        public event EventHandler<AuthenticationState> StateChanged;
        AuthenticationState _currentState = AuthenticationState.InProgress;
        public AuthenticationState CurrentState
        {
            get
            {
                return _currentState;
            }
            private set
            {
                if (_currentState != value)
                {
                    _currentState = value;
                    StateChanged?.Invoke(this, value);
                }
            }
        }

        public string SessionToken { get; private set; }

        public async Task TryAuthenticate()
        {
            Log.WriteLine("Checking local storage for saved credentials");

            try
            {
                var vault = new PasswordVault();
                var credsList = vault.FindAllByResource(CredentialResource);

                if (credsList.Any())
                {
                    var creds = credsList.First();
                    creds.RetrievePassword();

                    await DoAuthentication(creds.UserName, creds.Password);
                    CurrentState = AuthenticationState.Authenticated;
                }
                else
                {
                    CurrentState = AuthenticationState.UserInteractionRequired;
                }
            }
            catch (AuthenticationException ex)
            {
                Log.LogExceptionCatch(ex);
                CurrentState = AuthenticationState.Failed;
            }
            catch (COMException ex)
            {
                Log.LogExceptionCatch(ex);
                CurrentState = AuthenticationState.Failed;
            }
            catch (Exception ex)
            {
                Log.LogExceptionCatch(ex);
                CurrentState = AuthenticationState.Failed;
            }
        }

        public async Task DoAuthentication(string userName, string password)
        {
            Log.WriteLine($"Attempting to authenticate user {userName}");
            CurrentState = AuthenticationState.InProgress;


            // According to the fiddler trace of the offical client,
            // all we need to do is make a request to the login endpoint
            var client = new HttpClient();
            var stringContent = new StringContent(
                JsonConvert.SerializeObject(
                    new
                    {
                        email = userName,
                        password = password
                    }
                )
            );
            stringContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            var response = await client.PostAsync(
                LoginEndpoint,
                stringContent
            );

            var vault = new PasswordVault();

            if (response.IsSuccessStatusCode)
            {
                var responseObject = JsonObject.Parse(await response.Content.ReadAsStringAsync());
                var token = responseObject.GetNamedString("token");
                Log.WriteLine("Got token: " + token);
                SessionToken = token;
                CurrentState = AuthenticationState.Authenticated;

                // We don't want duplicates / mutliple users
                foreach (var p in vault.RetrieveAll())
                {
                    vault.Remove(p);
                }

                vault.Add(
                    new PasswordCredential(
                        CredentialResource,
                        userName,
                        password
                    )
                );
            }
            else
            {
                // Try to do something nice with the error
                string content = "";
                if (response.Content != null)
                {
                    content = await response.Content.ReadAsStringAsync();
                }

                Log.WriteLine(
                    string.Format(
                        "auth request returned code {0} ({1}): {2}",
                        response.StatusCode,
                        response.ReasonPhrase, content
                    )
                );

                CurrentState = AuthenticationState.Failed;

                // We don't want to keep around any stale creds
                foreach (var p in vault.RetrieveAll())
                {
                    vault.Remove(p);
                }
            }
        }

        public void ClearUserInfo()
        {
            CurrentState = AuthenticationState.UserInteractionRequired;
            SessionToken = null;

            var vault = new PasswordVault();
            foreach (var p in vault.RetrieveAll())
            {
                vault.Remove(p);
            }
        }

        private const string CredentialResource = "Discord-UWP";
        private static readonly string LoginEndpoint = "https://discordapp.com/api/auth/login";
    }
}
