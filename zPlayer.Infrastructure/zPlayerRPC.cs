using DiscordRPC;
using System.Text;
using zPlayer.Core;

namespace zPlayer.Infrastructure
{
    public sealed class zPlayerRPC : IDisposable
    {
        private readonly DiscordRpcClient client;

        public zPlayerRPC(string applicationId) {
            client = new(applicationId);
            client.Initialize();
        }

        public void Update(Song? song, bool isPlaying, TimeSpan position, TimeSpan duration, string? coverUrl = null) {
            if (song == null || duration <= TimeSpan.Zero) {
                client.ClearPresence();
                return;
            }

            position = TimeSpan.FromTicks(Math.Clamp(position.Ticks, 0, duration.Ticks));
            DateTime start = DateTime.UtcNow - position;

            client.SetPresence(new RichPresence {
                Type = ActivityType.Listening,
                StatusDisplay = StatusDisplayType.Details,
                Details = Limit(song.Title),
                State = Limit(isPlaying ? song.Artist : $"Paused · {song.Artist}"),
                Assets = new Assets {
                    LargeImageKey = coverUrl ?? "zplayer",
                    LargeImageText = Limit(song.AlbumTitle)
                },
                Timestamps = isPlaying ? new Timestamps {
                    Start = start,
                    End = start + duration
                } : null
            });
        }

        private static string Limit(string text) {
            const int maxBytes = 128;
            int bytes = 0;
            StringBuilder result = new();

            foreach (var rune in text.EnumerateRunes()) {
                if (bytes + rune.Utf8SequenceLength > maxBytes) break;
                result.Append(rune.ToString());
                bytes += rune.Utf8SequenceLength;
            }
            return result.ToString();
        }

        public void Dispose() => client.Dispose();
    }
}
