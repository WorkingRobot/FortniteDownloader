using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FortniteDownloader
{
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
    sealed class WebManifest
    {
        public string ManifestFileVersion; // might be useful later, but eh
        public string AppNameString;
        public string BuildVersionString;
        public string LaunchExeString;
        public WebFileManifest[] FileManifestList;

        public Dictionary<string, string> ChunkHashList;
        // public Dictionary<string, string> ChunkShaList; Not used atm, speed up and decrease memory usage
        public Dictionary<string, string> DataGroupList;
        // public Dictionary<string, string> ChunkFilesizeList; Not used atm, speed up and decrease memory usage
    }

    sealed class WebFileManifest
    {
        public string Filename;
        public string FileHash;
        public WebFileChunk[] FileChunkParts;
        public string[] InstallTags;
    }

    struct WebFileChunk
    {
        public string Guid;
        public string Offset;
        public string Size;
    }
#pragma warning restore CS0649

    // Converted (usable) Download Manifest
    public class Manifest
    {
        public Dictionary<string, FileChunkPart[]> FileManifests;

        internal Manifest(WebManifest webm)
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
        
        const string CHUNK_BASE_URL = Downloader.DOWNLOAD_BASE_URL + "ChunksV3/";
        public string Url => $"{CHUNK_BASE_URL}{DataGroup}/{Hash}_{Guid}.chunk";
    }
}
