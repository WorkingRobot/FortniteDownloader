using FortniteDownloader.Net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FortniteDownloader
{
    public class Authorization : IDisposable
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
            (AccessToken, Expiration) = JsonConvert.DeserializeObject<TokenResponse>(await (await Client.SendFormAsync("POST", OAUTH_TOKEN_URL, new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" },
                { "token_type", "eg1" },
            }).ConfigureAwait(false)).GetStringAsync().ConfigureAwait(false));
            Client.SetHeader("Authorization", "bearer " + AccessToken);
        }

        public async Task<string> SendRequest(string uri, bool skipAuthChecking = false)
        {
            if (!skipAuthChecking) await RefreshIfInvalid().ConfigureAwait(false);
            return await (await Client.SendAsync("GET", uri).ConfigureAwait(false)).GetStringAsync().ConfigureAwait(false);
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
        struct TokenResponse
        {
            public string access_token;
            public DateTimeOffset expires_at;

            public void Deconstruct(out string AccessToken, out DateTimeOffset AccessExpiration)
            {
                AccessToken = access_token;
                AccessExpiration = expires_at;
            }
        }
#pragma warning restore CS0649
    }

    // I hate myself for doing this, but .Net Standard doesn't support it
    static class Extensions
    {
        internal static string[] Split(this string me, string spliterator, int count = int.MaxValue) =>
            me.Split(new string[] { spliterator }, count, StringSplitOptions.None);
    }
}
