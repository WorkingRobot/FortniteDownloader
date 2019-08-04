using FortniteDownloader.Net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;

namespace FortniteDownloader
{
    public class Authorization : IDisposable
    {
        private readonly string Username;
        private readonly string Password;

        public string AccessToken { get; private set; }
        public string RefreshToken { get; private set; }

        public DateTimeOffset AccessExpiration { get; private set; }
        public DateTimeOffset RefreshExpiration { get; private set; }

        private readonly Client Client = new Client();

        const string LAUNCHER_LOGIN_INIT_URL = "https://launcher-website-prod07.ol.epicgames.com/epic-login";
        const string LAUNCHER_LOGIN_URL = "https://accounts.launcher-website-prod07.ol.epicgames.com/login/doLauncherLogin";
        const string OAUTH_TOKEN_URL = "https://account-public-service-prod03.ol.epicgames.com/account/api/oauth/token";
        const string LAUNCHER_AUTH_HEADER = "basic MzRhMDJjZjhmNDQxNGUyOWIxNTkyMTg3NmRhMzZmOWE6ZGFhZmJjY2M3Mzc3NDUwMzlkZmZlNTNkOTRmYzc2Y2Y=";

        public Authorization(string user, string pass)
        {
            Username = user;
            Password = pass;
        }

        public Authorization(string username, string password, string accessToken, string refreshToken, DateTimeOffset accessExpiration, DateTimeOffset refreshExpiration)
        {
            Username = username;
            Password = password;
            AccessToken = accessToken;
            RefreshToken = refreshToken;
            AccessExpiration = accessExpiration;
            RefreshExpiration = refreshExpiration;
        }

        public async Task Login()
        {
            var resp = await Client.SendAsync("GET", LAUNCHER_LOGIN_INIT_URL).ConfigureAwait(false);
            var launcherInfo = JsonConvert.DeserializeObject<LauncherParams>((await resp.GetStringAsync()).Split(".__setAuthorizeRedirParams(", 2)[1].Split(");", 2)[0]);

            resp = await Client.SendAsync("GET", $"{LAUNCHER_LOGIN_URL}?client_id={launcherInfo.client_id}&redirectUrl={HttpUtility.UrlEncode(launcherInfo.redirectUrl)}", true).ConfigureAwait(false);

            Client.SetHeader("X-XSRF-TOKEN", Client.Cookies.GetCookies(new Uri(LAUNCHER_LOGIN_URL))["XSRF-TOKEN"].Value);

            resp = await Client.SendFormAsync("POST", LAUNCHER_LOGIN_URL, new Dictionary<string, string>
            {
                { "fromForm", "yes" },
                { "authType", "" },
                { "linkExtAuth", "" },
                { "client_id", launcherInfo.client_id },
                { "redirectUrl", launcherInfo.redirectUrl },
                { "epic_username", Username },
                { "password", Password },
                { "rememberMe", "YES" }
            }, true).ConfigureAwait(false);
            Client.SetHeader("X-XSRF-TOKEN", null);

            resp = await Client.SendAsync("GET", launcherInfo.redirectUrl).ConfigureAwait(false);
            var code = (await resp.GetStringAsync().ConfigureAwait(false)).Split("loginWithExchangeCode('", 2)[1].Split("',", 2)[0];

            Client.SetHeader("Authorization", LAUNCHER_AUTH_HEADER);
            resp = await Client.SendFormAsync("POST", OAUTH_TOKEN_URL, new Dictionary<string, string>
            {
                { "grant_type", "exchange_code" },
                { "token_type", "eg1" },
                { "exchange_code", code }
            }).ConfigureAwait(false);
            var tokens = JsonConvert.DeserializeObject<ExchangeResponse>(await resp.GetStringAsync().ConfigureAwait(false));

            AccessToken = tokens.access_token;
            RefreshToken = tokens.refresh_token;
            AccessExpiration = tokens.expires_at;
            RefreshExpiration = tokens.refresh_expires_at;
            Client.SetHeader("Authorization", "bearer " + AccessToken);
        }

        public async Task<string> SendRequest(string uri, bool skipAuthChecking = false)
        {
            if (!skipAuthChecking) await RefreshIfInvalid().ConfigureAwait(false);
            return await (await Client.SendAsync("GET", uri).ConfigureAwait(false)).GetStringAsync().ConfigureAwait(false);
        }

        public async Task<bool> RefreshIfInvalid()
        {
            if (AccessExpiration < DateTimeOffset.UtcNow)
            {
                if (RefreshExpiration < DateTimeOffset.UtcNow)
                {
                    await Login().ConfigureAwait(false);
                    return true;
                }
                
                Client.SetHeader("Authorization", LAUNCHER_AUTH_HEADER);
                var resp = await Client.SendFormAsync("POST", OAUTH_TOKEN_URL, new Dictionary<string, string>
                {
                    { "grant_type", "refresh_token" },
                    { "token_type", "eg1" },
                    { "refresh_token", RefreshToken }
                }).ConfigureAwait(false);
                var tokens = JsonConvert.DeserializeObject<ExchangeResponse>(await resp.GetStringAsync().ConfigureAwait(false));

                AccessToken = tokens.access_token;
                RefreshToken = tokens.refresh_token;
                AccessExpiration = tokens.expires_at;
                RefreshExpiration = tokens.refresh_expires_at;
                Client.SetHeader("Authorization", "bearer " + AccessToken);
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

#pragma warning disable CS0649
        struct LauncherParams
        {
            public string client_id;
            public string redirectUrl;
        }

        struct ExchangeResponse
        {
            public string access_token;
            public DateTimeOffset expires_at;
            public string refresh_token;
            public DateTimeOffset refresh_expires_at;
        }
#pragma warning restore CS0649
    }

    // I hate myself for doing this, but .Net Standard doesn't support it
    static class Extensions
    {
        internal static string[] Split(this string me, string spliterator, int count)
        {
            return me.Split(new string[] { spliterator }, count, StringSplitOptions.None);
        }
    }
}
