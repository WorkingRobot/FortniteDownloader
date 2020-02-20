using fnbot.shop.Web;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using JName = System.Text.Json.Serialization.JsonPropertyNameAttribute;

namespace FortniteDownloader
{
    public sealed class Authorization : IDisposable
    {
        public string AccessToken { get; private set; }

        public DateTimeOffset Expiration { get; private set; }

        private readonly Client Client = new Client();

        const string OAUTH_TOKEN_URL = "https://account-public-service-prod03.ol.epicgames.com/account/api/oauth/token";
        readonly string LAUNCHER_AUTH_HEADER = $"basic {GenerateHeader("34a02cf8f4414e29b15921876da36f9a", "daafbccc737745039dffe53d94fc76cf")}";

        public Authorization() { }

        public Authorization(string accessToken, DateTimeOffset accessExpiration)
        {
            AccessToken = accessToken;
            Expiration = accessExpiration;
        }

        public async Task Login()
        {
            Client.SetHeader("Authorization", LAUNCHER_AUTH_HEADER);
            (AccessToken, Expiration) = await JsonSerializer.DeserializeAsync<TokenResponse>((await Client.SendFormAsync("POST", OAUTH_TOKEN_URL, new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" },
                { "token_type", "eg1" },
            }).ConfigureAwait(false)).Stream);
            Client.SetHeader("Authorization", "bearer " + AccessToken);
        }

        public async Task<string> SendRequest(string uri, bool skipAuthChecking = false)
        {
            if (!skipAuthChecking) await RefreshIfInvalid().ConfigureAwait(false);
            return await (await Client.SendAsync("GET", uri).ConfigureAwait(false)).GetStringAsync().ConfigureAwait(false);
        }
        public async Task<Stream> SendRequestAsync(string uri, bool skipAuthChecking = false)
        {
            if (!skipAuthChecking)
                await RefreshIfInvalid().ConfigureAwait(false);
            return (await Client.SendAsync("GET", uri).ConfigureAwait(false)).Stream;
        }

        public async Task<bool> RefreshIfInvalid()
        {
            if (Expiration < DateTimeOffset.UtcNow)
            {
                await Login().ConfigureAwait(false);
                return true;
            }
            return false;
        }

        bool disposed;
        public void Dispose()
        {
            if (disposed) return;
            Client.Dispose();
            disposed = true;
        }

        static string GenerateHeader(string id, string secret) => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{id}:{secret}"));

#pragma warning disable CS0649
        class TokenResponse
        {
            [JName("access_token")]
            public string AccessToken { get; set; }
            [JName("expires_at")]
            public DateTimeOffset ExpiresAt { get; set; }

            public void Deconstruct(out string AccessToken, out DateTimeOffset AccessExpiration)
            {
                AccessToken = this.AccessToken;
                AccessExpiration = ExpiresAt;
            }
        }
#pragma warning restore CS0649
    }
}
