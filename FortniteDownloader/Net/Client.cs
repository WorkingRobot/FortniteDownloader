using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace FortniteDownloader.Net
{
    // Heavily based off of https://github.com/discord-net/Discord.Net/blob/dev/src/Discord.Net.Rest/Net/DefaultRestClient.cs for reliablility
    public class Client : IDisposable
    {
        public readonly CookieContainer Cookies;
        private readonly HttpClient HttpClient;
        private bool IsDisposed;

        public Client(bool useProxy = true)
        {
            Cookies = new CookieContainer();
            HttpClient = new HttpClient(new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip,
                UseCookies = true,
                CookieContainer = Cookies,
                UseProxy = useProxy
            });
        }

        public void SetHeader(string key, string value, bool validate = true)
        {
            HttpClient.DefaultRequestHeaders.Remove(key);
            if (value != null)
            {
                if (validate)
                    HttpClient.DefaultRequestHeaders.Add(key, value);
                else
                    HttpClient.DefaultRequestHeaders.TryAddWithoutValidation(key, value);
            }
        }

        public Task<Response> SendAsync(string method, string uri, bool headerOnly = false) => SendInternalAsync(new HttpRequestMessage(GetMethod(method), new Uri(uri)), headerOnly);

        public Task<Response> SendFormAsync(string method, string uri, IReadOnlyDictionary<string, string> formParams, bool headerOnly = false) =>
            SendInternalAsync(new HttpRequestMessage(GetMethod(method), new Uri(uri))
            {
                Content = formParams != null ? new FormUrlEncodedContent(formParams) : null
            }, headerOnly);

        private async Task<Response> SendInternalAsync(HttpRequestMessage request, bool headerOnly)
        {
            HttpResponseMessage response;
            using (request)
                response = await HttpClient.SendAsync(request).ConfigureAwait(false);

            var headers = response.Headers.ToDictionary(x => x.Key, x => x.Value.FirstOrDefault(), StringComparer.OrdinalIgnoreCase);
            var stream = !headerOnly ? await response.Content.ReadAsStreamAsync().ConfigureAwait(false) : null;

            return new Response(response.StatusCode, headers, stream);
        }

        private void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                    HttpClient.Dispose();
                IsDisposed = true;
            }
        }
        public void Dispose()
        {
            Dispose(true);
        }

        // unsure if there is a performance improvement here or if it's just a precaution in D.Net
        private static readonly HttpMethod Patch = new HttpMethod("PATCH");
        private HttpMethod GetMethod(string method)
        {
            switch (method)
            {
                case "DELETE": return HttpMethod.Delete;
                case "GET": return HttpMethod.Get;
                case "PATCH": return Patch;
                case "POST": return HttpMethod.Post;
                case "PUT": return HttpMethod.Put;
                default: throw new ArgumentOutOfRangeException(nameof(method), $"Unknown HttpMethod: {method}");
            }
        }
    }
}
