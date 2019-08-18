using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FortniteDownloader
{
    public partial class Downloader
    {
        public static readonly ReadOnlyDictionary<string, string> SavedManifests = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            { "10.0-CL-7658179",  "wcfjh9c-okLtEOiDMkG8VzIC1p-ENg" },
            { "10.0-CL-7704164",  "7C0cEaVSyWc6Fge6RE2N7IqJ4oPhww" },
            { "10.10-CL-7955722", "QpjVgBdS5NubVduhREqDPr8piZTw-w" }
        });
    }
}
