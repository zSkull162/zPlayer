using DiscordRPC;
using System.Data;
using System.Text;
using zPlayer.Core;

namespace zPlayer.Infrastructure
{
    public sealed class zPlayerRPC : IDisposable
    {
        private readonly DiscordRpcClient client;

        public zPlayerRPC(string applicationId) {
            client = new(applicationId) {
                SkipIdenticalPresence = false
            };
            client.Initialize();
        }

        public void Update(zPlayerRpcSettings settings, Song? song, bool isPlaying, TimeSpan position, TimeSpan duration, string? coverUrl = null) {
            if (song == null || duration <= TimeSpan.Zero || !settings.Enabled) {
                client.ClearPresence();
                return;
            }
            position = TimeSpan.FromTicks(Math.Clamp(position.Ticks, 0, duration.Ticks));
            DateTime start = DateTime.UtcNow - position;

            string largeImageText = Limit(song.AlbumTitle);
            if (!settings.DisplayAlbumName) largeImageText = string.Empty;

            Timestamps? timestamps = new() { Start = start, End = start + duration };
            if (!isPlaying) {
                if (settings.DisplayPausedState && settings.DisplayAlbumName) largeImageText = Limit($"{largeImageText}  •  (Paused)");
                else if (settings.DisplayPausedState && !settings.DisplayAlbumName) largeImageText = "(Paused)";
                timestamps = null;
            }

            StatusDisplayType statusType = settings.StatusType switch {
                1 => StatusDisplayType.Details,
                2 => StatusDisplayType.State,
                _ => StatusDisplayType.Name
            };

            client.SetPresence(new RichPresence {
                Type = ActivityType.Listening,
                StatusDisplay = statusType,
                Details = Limit(song.Title),
                State = Limit(song.Artist),
                Assets = new Assets {
                    LargeImageKey = coverUrl ?? "zplayer",
                    LargeImageText = largeImageText
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
