using FortniteDownloader.Net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FortniteDownloader
{
    public class Downloader : IDisposable
    {
        readonly Authorization Auth;
        readonly Client Client;

        const string APP_MANIFEST_URL = "https://launcher-public-service-prod06.ol.epicgames.com/launcher/api/public/assets/v2/platform/Windows/namespace/fn/catalogItem/4fe75bbc5a674f4f9b356b5c90567da5/app/Fortnite/label/Live";
        internal const string CHUNK_BASE_URL = "http://epicgames-download1.akamaized.net/Builds/Fortnite/CloudDir/ChunksV3/";

        public Downloader(Authorization auth)
        {
            Auth = auth;
            Client = new Client();
        }

        private Element appManifest;
        public async Task<Element> GetAppManifest(bool forceUpdate = false) =>
            appManifest != null && !forceUpdate ? appManifest :
                (appManifest = JsonConvert.DeserializeObject<ElementList>(
                await Auth.SendRequest(APP_MANIFEST_URL).ConfigureAwait(false)
                ).elements[0]);

        private Manifest downloadManifest;
        public async Task<Manifest> GetDownloadManifest(bool forceUpdate = false) => 
            downloadManifest != null && !forceUpdate ? downloadManifest :
                (downloadManifest = new Manifest(JsonConvert.DeserializeObject<WebManifest>(
                await (await Client.SendAsync("GET", (await GetAppManifest(forceUpdate).ConfigureAwait(false)).manifests[0].BuiltUrl).ConfigureAwait(false)).GetStringAsync().ConfigureAwait(false)
                )));

        public async Task<DownloadStream> OpenFile(string file, bool cachePreviousChunks = false) =>
            new DownloadStream(file, await GetDownloadManifest().ConfigureAwait(false), cachePreviousChunks, Client);

        bool disposed;
        public void Dispose()
        {
            if (disposed) return;
            downloadManifest = null;
            appManifest = null;
            Auth.Dispose();
            Client.Dispose();
            disposed = true;
        }

#pragma warning disable CS0649
        // App Manifest
        struct ElementList
        {
            public Element[] elements;
        }

        public sealed class Element
        {
            public string appName;
            public string labelName;
            public string buildVersion;
            public string hash;
            public ManifestInfo[] manifests;
        }

        public struct ManifestInfo
        {
            public string uri;
            public QueryParam[] queryParams;

            public string BuiltUrl
            {
                get
                {
                    if (queryParams == null) return uri;
                    StringBuilder builder = new StringBuilder(uri);
                    builder.Append('?');
                    foreach (var p in queryParams)
                    {
                        builder.AppendFormat("{0}={1}&", p.name, p.value);
                    }
                    return builder.ToString(0, builder.Length - 1);
                }
            }
        }

        public struct QueryParam
        {
            public string name;
            public string value;
        }
        
        // Download Manifest
        internal sealed class WebManifest
        {
            public string ManifestFileVersion; // might be useful later, but eh
            public string AppNameString;
            public string BuildVersionString;
            public string LaunchExeString;
            public WebFileManifest[] FileManifestList;

            public Dictionary<string, string> ChunkHashList;
            public Dictionary<string, string> ChunkShaList;
            public Dictionary<string, string> DataGroupList;
            public Dictionary<string, string> ChunkFilesizeList;
        }

        internal sealed class WebFileManifest
        {
            public string Filename;
            public string FileHash;
            public WebFileChunk[] FileChunkParts;
            public string[] InstallTags;
        }

        internal struct WebFileChunk
        {
            public string Guid;
            public string Offset;
            public string Size;
        }
#pragma warning restore CS0649
    }

    // Converted (usable) Download Manifest
    public class Manifest
    {
        public Dictionary<string, FileChunkPart[]> FileManifests;

        internal Manifest(Downloader.WebManifest webm)
        {
            SortedDictionary<string, FileChunk> chunks = new SortedDictionary<string, FileChunk>();
            foreach (var kv in webm.ChunkHashList)
            {
                chunks[kv.Key] = new FileChunk
                {
                    DataGroup = int.Parse(webm.DataGroupList[kv.Key]).ToString().PadLeft(2, '0'),
                    Guid = kv.Key,
                    Hash = ToHex(HashToBytes(kv.Value, true))
                };
            }

            FileManifests = new Dictionary<string, FileChunkPart[]>();
            foreach (var filem in webm.FileManifestList)
            {
                FileChunkPart[] parts = new FileChunkPart[filem.FileChunkParts.Length];
                for (int i = 0; i < parts.Length; i++)
                {
                    var part = filem.FileChunkParts[i];
                    parts[i] = new FileChunkPart
                    {
                        Chunk = chunks[part.Guid],
                        Offset = BitConverter.ToInt32(HashToBytes(part.Offset), 0),
                        Size = BitConverter.ToInt32(HashToBytes(part.Size), 0)
                    };
                }
                FileManifests[filem.Filename] = parts;
            }
        }

        static byte[] HashToBytes(string hash, bool reversed = false)
        {
            byte[] ret = new byte[hash.Length / 3];
            for (int i = 0; i < ret.Length; i++)
            {
                ret[i] = byte.Parse(hash.Substring((reversed ? ret.Length - i - 1 : i) * 3, 3));
            }
            return ret;
        }

        static readonly uint[] _Lookup32 = Enumerable.Range(0, 256).Select(i => {
            string s = i.ToString("X2");
            return s[0] + ((uint)s[1] << 16);
        }).ToArray();
        static string ToHex(byte[] bytes, int length = -1)
        {
            if (bytes == null) return null;
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

    public class FileChunkPart
    {
        public FileChunk Chunk;
        public int Offset;
        public int Size;
    }

    public class FileChunk
    {
        public string Guid;
        public string Hash;
        public string DataGroup;

        public string Url => $"{Downloader.CHUNK_BASE_URL}{DataGroup}/{Hash}_{Guid}.chunk";
    }
}
