using System;
using System.Threading.Tasks;

namespace FortniteDownloader
{
    public interface IDownloader : IDisposable
    {
        string ManifestUrl { get; }

        Task<Manifest> GetDownloadManifest(bool forceUpdate = false);

        Task<DownloadStream> OpenFile(string file, bool cachePreviousChunks = false);
    }
}
