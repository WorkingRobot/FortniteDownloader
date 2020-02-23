using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using fnbot.shop.Web;

namespace FortniteDownloader
{
    class CachedDownloadClient : Client
    {
        public string CacheDirectory { get; }

        public CachedDownloadClient(string cacheDir, bool useCookies = false, bool useProxy = true) : base(useCookies, useProxy)
        {
            CacheDirectory = cacheDir;
            if (!(Directory.CreateDirectory(cacheDir)?.Exists ?? false))
            {
                throw new DirectoryNotFoundException($"Could not create cache directory \"{cacheDir}\"");
            }
        }

        protected override async Task<Response> SendInternalAsync(HttpRequestMessage request, IReadOnlyDictionary<string, string> reqHeaders = null, bool addBody = true)
        {
            if (request.RequestUri.AbsolutePath.EndsWith(".chunk"))
            {
                var hash = GetHash(request.RequestUri.AbsolutePath);
                var cachePath = Path.Join(CacheDirectory, $"{hash[..2]}/{hash[2..]}");
                if (File.Exists(cachePath))
                {
                    var stream = File.OpenRead(cachePath);
                    return new Response(HttpStatusCode.OK, null, stream, stream);
                }
                else
                {
                    var resp = await base.SendInternalAsync(request, reqHeaders, addBody);
                    Directory.CreateDirectory(Path.Join(CacheDirectory, hash[..2]));
                    using (var cacheFile = File.OpenWrite(cachePath))
                    {
                        await resp.Stream.CopyToAsync(resp.Stream);
                    }
                    resp.Stream.Position = 0;
                    return resp;
                }
            }
            return await base.SendInternalAsync(request, reqHeaders, addBody);
        }

        static string GetHash(string path)
        {
            using var hash = SHA256.Create();
            return ToHex(hash.ComputeHash(Encoding.UTF8.GetBytes(path)));
        }

        static readonly uint[] _Lookup32 = Enumerable.Range(0, 256).Select(i => {
            string s = i.ToString("x2");
            return s[0] + ((uint)s[1] << 16);
        }).ToArray();
        static string ToHex(byte[] bytes, int length = -1)
        {
            if (bytes == null)
                return null;
            length = (length == -1 || length > bytes.Length) ? bytes.Length : length;
            var result = new char[length * 2];
            for (int i = 0; i < length; i++)
            {
                var val = _Lookup32[bytes[i]];
                result[2 * i] = (char)val;
                result[2 * i + 1] = (char)(val >> 16);
            }
            return new string(result);
        }
    }
}
