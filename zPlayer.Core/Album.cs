
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
        public byte[]? Artwork => GetArtwork();

        public byte[]? GetArtwork() {
            if (CustomArtwork != null && CustomArtwork.Length > 0) return CustomArtwork; // Prioritize custom artwork first,

            if (ArtworkSongId.HasValue) {
                Song? selected = Songs.FirstOrDefault(s => s.Id == ArtworkSongId.Value);
                if (selected?.Artwork != null && selected.Artwork.Length > 0) return selected.Artwork; // then artwork that was selected from a song in the list,
            }

            foreach (Song song in Songs) { // and finally, the first song in the list that has an embedded cover
                if (song.Artwork != null && song.Artwork.Length > 0) return song.Artwork;
            }
            return null;
        }

        public Album Clone() {
            Album copy = (Album)MemberwiseClone();
            copy.Songs = [.. Songs];
            copy.CustomArtwork = CustomArtwork?.ToArray();
            return copy;
        }
    }
}