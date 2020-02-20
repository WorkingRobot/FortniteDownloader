using fnbot.shop.Web;
using Ionic.Zlib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FortniteDownloader
{
    public class DownloadStream : Stream
    {
        readonly FileChunkPart[] Chunks;
        readonly byte[][] DownloadedChunks;
        readonly Client Client;

        readonly bool CacheChunks;
        int ChunkID = -1;
        byte[] ChunkBuffer;

        public DownloadStream(string filename, Manifest manifest, bool cachePreviousChunks, Client client = null)
        {
            Chunks = manifest.FileManifests[filename];
            CacheChunks = cachePreviousChunks;
            if (CacheChunks)
                DownloadedChunks = new byte[Chunks.Length][];
            Length = Chunks.Sum(c => (long)c.Size);
            ChunkCount = Chunks.Length;
            Client = client ?? new Client();
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length { get; }
        public int ChunkCount { get; }

        private long position;
        public override long Position {
            get => position;
            set
            {
                if (value >= Length || value < 0)
                    throw new ArgumentOutOfRangeException();
                position = value;
            }
        }

        public override void Flush() => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public Task Prefetch(long offset, long count, int concurrentDownloads = 10)
        {
            if (!CacheChunks)
                throw new InvalidOperationException($"{nameof(CacheChunks)} must be true to prefetch chunks");
            var (i, startPos) = GetChunkIndex(offset);
            if (i == -1)
                return Task.CompletedTask; // throw maybe?

            var tasks = new List<Task>();
            var sem = new SemaphoreSlim(concurrentDownloads);
            while (count > 0)
            {
                tasks.Add(Prefetch(i));
                if (++i == Chunks.Length) break;
                count -= Chunks[i].Size - startPos;
                if (startPos != 0) startPos = 0;
            }
            return Task.WhenAll(tasks).ContinueWith(t => sem.Dispose());

            async Task Prefetch(int i)
            {
                await sem.WaitAsync();
                await GetChunk(i);
                sem.Release();
            }
        }

        public void ClearCache()
        {
            if (!CacheChunks)
                throw new InvalidOperationException($"{nameof(CacheChunks)} must be true to clear cache");
            for (int i = 0; i < DownloadedChunks.Length; i++)
            {
                DownloadedChunks[i] = null;
            }
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var (i, startPos) = GetChunkIndex(position);
            if (i == -1) return 0;
            byte[] chunkBuffer;
            int bytesRead = 0;
            while (true)
            {
                chunkBuffer = await GetChunk(i).ConfigureAwait(false);
                if (count - bytesRead > chunkBuffer.Length - startPos)
                {
                    // TODO: use Unsafe.CopyBlock
                    Buffer.BlockCopy(chunkBuffer, startPos, buffer, offset + bytesRead, chunkBuffer.Length - startPos);
                    bytesRead += chunkBuffer.Length - startPos;
                }
                else
                {
                    Buffer.BlockCopy(chunkBuffer, startPos, buffer, offset + bytesRead, count - bytesRead);
                    bytesRead += count - bytesRead;
                    break;
                }
                if (++i == Chunks.Length) break;
                if (startPos != 0) startPos = 0;
            }
            position += bytesRead;
            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;
                case SeekOrigin.Current:
                    Position = offset + position;
                    break;
                case SeekOrigin.End:
                    Position = Length + offset;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return position;
        }

        (int Index, int ChunkPos) GetChunkIndex(long position)
        {
            int size;
            for (int i = 0; i < Chunks.Length; i++)
            {
                size = Chunks[i].Size;
                if (position < size) return (i, (int)position);
                position -= size;
            }
            return (-1, -1);
        }

        public async Task<byte[]> GetChunk(int i)
        {
            if (CacheChunks && DownloadedChunks[i] != null)
                return DownloadedChunks[i];
            if (i == ChunkID)
                return ChunkBuffer;
            using var chunkStream = (await Client.SendAsync("GET", Chunks[i].Chunk.Url).ConfigureAwait(false)).Stream;

            // maybe clean this up, even if it's pretty optimized
            chunkStream.Position = 8;
            var headerSize = chunkStream.ReadByte();
            chunkStream.Position = 40;
            var compressed = chunkStream.ReadByte() == 1;

            chunkStream.Position = headerSize;
            byte[] buffer = new byte[Chunks[i].Size];
            if (!compressed)
            {
                chunkStream.Position += Chunks[i].Offset;
                await chunkStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            }
            else
            {
                using var decompressionStream = new ZlibStream(chunkStream, CompressionMode.Decompress);

                // no way to seek to a position, might use a different library for this
                var offsetBuffer = new byte[Chunks[i].Offset];
                await decompressionStream.ReadAsync(offsetBuffer, 0, offsetBuffer.Length).ConfigureAwait(false);

                await decompressionStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            }
            if (CacheChunks)
            {
                return DownloadedChunks[i] = buffer;
            }
            ChunkID = i;
            return ChunkBuffer = buffer;
        }
    }
}
