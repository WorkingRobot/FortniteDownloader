using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace FortniteDownloader.Net
{
    public struct Response
    {
        public HttpStatusCode StatusCode { get; }
        public Dictionary<string, string> Headers { get; }
        public Stream Stream { get; }

        public Response(HttpStatusCode statusCode, Dictionary<string, string> headers, Stream stream)
        {
            StatusCode = statusCode;
            Headers = headers;
            Stream = stream;
        }

        public Task<string> GetStringAsync()
        {
            using (var reader = new StreamReader(Stream))
                return reader.ReadToEndAsync();
        }
    }
}
