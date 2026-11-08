using System.IO;
using System.Threading;
using System.Windows.Media.Imaging;
using zPlayer.App.Converters;
using zPlayer.Core;
using zPlayer.Infrastructure.Storage;

namespace zPlayer.App.Artwork
{
    internal sealed class ArtworkLoader(MusicLibraryDatabase database)
    {
        private const long CacheBudget = 32 * 1024 * 1024;
        private readonly object gate = new();
        private readonly SemaphoreSlim workers = new(2);
        private readonly Dictionary<(ArtworkRef Cover, int Width), Pending> pending = new();
        private readonly Dictionary<(ArtworkRef Cover, int Width), LinkedListNode<CacheEntry>> cache = new();
        private readonly LinkedList<CacheEntry> recent = new();
        private long cacheBytes;

        private sealed record CacheEntry((ArtworkRef Cover, int Width) Key, BitmapSource? Image, long Bytes);

        private sealed class Pending
        {
            public CancellationTokenSource Cancellation { get; } = new();
            public Task<BitmapSource?> Task { get; set; } = null!;
            public int Waiters { get; set; }
        }

        public async Task<BitmapSource?> GetAsync(ArtworkRef cover, int width, CancellationToken cancellationToken) {
            var key = (cover, width);
            Pending request;
            lock (gate) {
                if (cache.TryGetValue(key, out var node)) {
                    recent.Remove(node);
                    recent.AddFirst(node);
                    return node.Value.Image;
                }

                if (!pending.TryGetValue(key, out request!)) {
                    request = new Pending();
                    var token = request.Cancellation.Token;
                    request.Task = Task.Run(() => LoadAsync(key, token));
                    pending.Add(key, request);
                }
                request.Waiters++;
            }

            try {
                return await request.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            finally {
                lock (gate) {
                    if (--request.Waiters == 0) {
                        pending.Remove(key);
                        request.Cancellation.Cancel();
                        _ = request.Task.ContinueWith(task => {
                            _ = task.Exception;
                            request.Cancellation.Dispose();
                        }, TaskScheduler.Default);
                    }
                }
            }
        }

        private async Task<BitmapSource?> LoadAsync((ArtworkRef Cover, int Width) key, CancellationToken token) {
            await workers.WaitAsync(token).ConfigureAwait(false);
            try {
                token.ThrowIfCancellationRequested();
                ArtworkConverter decoder = new();
                byte[]? thumbnail = database.LoadArtworkThumbnail(key.Cover, key.Width);
                BitmapSource? image = null;

                if (thumbnail != null) {
                    image = decoder.BmpFromBytes(thumbnail);
                }
                else if (database.LoadArtwork(key.Cover) is { Length: > 0 } original) {
                    token.ThrowIfCancellationRequested();
                    image = DecodeCover(decoder, original, key.Width);

                    PngBitmapEncoder encoder = new();
                    encoder.Frames.Add(BitmapFrame.Create(image));
                    using MemoryStream stream = new();
                    encoder.Save(stream);
                    token.ThrowIfCancellationRequested();
                    database.SaveArtworkThumbnail(key.Cover, key.Width, stream.ToArray());
                }

                lock (gate) {
                    token.ThrowIfCancellationRequested();
                    long bytes = image == null ? 128 : (long)image.PixelWidth * image.PixelHeight * Math.Max(4, (image.Format.BitsPerPixel + 7) / 8);
                    if (bytes <= CacheBudget && !cache.ContainsKey(key)) {
                        while (cacheBytes + bytes > CacheBudget || cache.Count >= 512) {
                            var last = recent.Last!;
                            cacheBytes -= last.Value.Bytes;
                            cache.Remove(last.Value.Key);
                            recent.RemoveLast();
                        }
                        cache.Add(key, recent.AddFirst(new CacheEntry(key, image, bytes)));
                        cacheBytes += bytes;
                    }
                }
                return image;
            }
            finally {
                workers.Release();
            }
        }
        private static BitmapSource DecodeCover(ArtworkConverter decoder, byte[] bytes, int size) {
            using MemoryStream stream = new(bytes);
            BitmapFrame frame = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnDemand).Frames[0];
            int width = frame.PixelWidth;
            if (frame.PixelHeight > width) width = Math.Max(1, (int)((long)size * width / frame.PixelHeight));
            return decoder.BmpFromBytes(bytes, Math.Min(width, size));
        }
    }
}
