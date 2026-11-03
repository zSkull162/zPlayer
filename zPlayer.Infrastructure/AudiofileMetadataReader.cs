using zPlayer.Core;

namespace zPlayer.Infrastructure
{
    public class AudiofileMetadataReader
    {
        public Song Read(string filePath) {
            using var file = TagLib.File.Create(filePath);

            string title = file.Tag.Title;
            title = DefaultIfNull(title, Path.GetFileNameWithoutExtension(filePath));
            string artist = file.Tag.FirstPerformer;
            artist = DefaultIfNull(artist, "Unknown Artist");
            string album = file.Tag.Album;
            album = DefaultIfNull(album, "Unknown Album");
            string albumArtist = string.Join(", ", file.Tag.AlbumArtists.Where(a => !string.IsNullOrWhiteSpace(a)));
            albumArtist = DefaultIfNull(albumArtist, "Unknown Artist");
            byte[]? artwork = null;
            if (file.Tag.Pictures != null && file.Tag.Pictures.Length > 0) {
                artwork = file.Tag.Pictures[0].Data.Data;
            }

            return new Song {
                FilePath = filePath,
                Title = title,
                Artist = artist,
                AlbumTitle = album,
                AlbumArtist = albumArtist,
                Duration = file.Properties.Duration,
                Artwork = artwork,
            };
        }

        /// <summary>Checks if the originalString is null or whitespace, and returns defaultValue if so</summary>
        /// <returns>The originalString if it is not null or whitespace, defaultValue otherwise</returns>
        private static string DefaultIfNull(string originalString, string defaultValue) {
            if (string.IsNullOrWhiteSpace(originalString)) return defaultValue;
            return originalString;
        }
    }
}
