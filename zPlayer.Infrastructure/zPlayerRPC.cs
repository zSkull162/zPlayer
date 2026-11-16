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

            string state = Limit(song.Artist);
            Timestamps? timestamps = new() { Start = start, End = start + duration };
            if (!isPlaying) {
                state = Limit($"(Paused) • {song.Artist}");
                timestamps = null;
            }

            client.SetPresence(new RichPresence {
                Type = ActivityType.Listening,
                StatusDisplay = StatusDisplayType.State,
                Details = Limit(song.Title),
                State = state,
                Assets = new Assets {
                    LargeImageKey = coverUrl ?? "zplayer",
                    LargeImageText = Limit(song.AlbumTitle)
                },
                Timestamps = timestamps
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
