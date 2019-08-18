using FortniteDownloader.Net;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace FortniteDownloader
{
    public class AuthedDownloader : IDownloader
    {
        readonly Authorization Auth;
        readonly Client Client;

        const string APP_MANIFEST_URL = "https://launcher-public-service-prod06.ol.epicgames.com/launcher/api/public/assets/v2/platform/Windows/namespace/fn/catalogItem/4fe75bbc5a674f4f9b356b5c90567da5/app/Fortnite/label/Live";

        public AuthedDownloader()
        {
            Auth = new Authorization();
            Client = new Client();
        }

        public AuthedDownloader(Authorization auth)
        {
            Auth = auth;
            Client = new Client();
        }

        public string ManifestUrl => GetAppManifest().GetAwaiter().GetResult().manifests[0].BuiltUrl;

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
    }
}
