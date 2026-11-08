
namespace zPlayer.Core
{
    public class Album
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Title { get; set; } = "Unknown Album";
        public string Artist { get; set; } = "Unknown Artist";

        public List<Song> Songs { get; set; } = [];
        public int SongCount => Songs.Count;

        public byte[]? CustomArtwork { get; set; }
        public  Guid? ArtworkSongId { get; set; }

        public TimeSpan Duration => TimeSpan.FromTicks(Songs.Sum(s => s.Duration.Ticks));
        public ArtworkRef? CustomCover { get; set; }
        public ArtworkRef? Cover {
            get {
                if (CustomCover is { } custom) return custom;
                ArtworkRef? selected = Songs.FirstOrDefault(song => song.Id == ArtworkSongId)?.Cover;
                return selected ?? Songs.FirstOrDefault(song => song.Cover.HasValue)?.Cover;
            }
        }

        public Album Clone() {
            Album copy = (Album)MemberwiseClone();
            copy.Songs = [.. Songs];
            copy.CustomArtwork = CustomArtwork?.ToArray();
            return copy;
        }
    }
}