using System.Collections.ObjectModel;

namespace zPlayer.Core
{
    public class MusicLibrary
    {
        private readonly ObservableCollection<Song> songs = [];
        private readonly List<Album> albums = [];
        private readonly List<Mix> mixes = [];

        public ReadOnlyObservableCollection<Song> Songs { get; }
        public IReadOnlyList<Album> Albums => albums;
        public IReadOnlyList<Mix> Mixes => mixes;

        public List<Album> GetAlbums() => albums.OrderBy(a => a.Title).ToList();
        public List<Mix> GetMixes() => mixes.OrderBy(mix => mix.Title, StringComparer.OrdinalIgnoreCase).ToList();

        public MusicLibrary() {
            Songs = new(songs);
        }

        public void Clear() {
            songs.Clear();
            albums.Clear();
            mixes.Clear();
        }

        #region Mix Methods
        public void RefreshMixes() {
            if (mixes.Count == 0) return;

            Dictionary<Guid, Song> available = songs.ToDictionary(song => song.Id);
            foreach (Mix mix in mixes) {
                mix.RefreshSongs(available);
            }
        }

        public Mix? GetMix(Guid mixId) => mixes.FirstOrDefault(mix => mix.Id == mixId);

        public bool AddMix(Mix mix) {
            if (mixes.Any(existing => existing.Id == mix.Id)) return false;
            mixes.Add(mix);
            return true;
        }

        public bool RemoveMix(Guid mixId) {
            return mixes.RemoveAll(mix => mix.Id == mixId) > 0;
        }

        public Song[] GetMixSongs(Guid mixId) {
            Mix? mix = GetMix(mixId);
            if (mix == null) return [];

            Dictionary<Guid, Song> available = songs.ToDictionary(song => song.Id);
            IEnumerable<MixSong> entries = mix.Entries.Where(entry => available.ContainsKey(entry.SongId));

            IOrderedEnumerable<MixSong> OrderBy<T>(Func<MixSong, T> selector, IComparer<T>? comparer = null) {
                return mix.SortDescending ? entries.OrderByDescending(selector, comparer) : entries.OrderBy(selector, comparer);
            }

            IOrderedEnumerable<MixSong> sorted = mix.SortType switch {
                MixSortType.Title => OrderBy(entry => available[entry.SongId].Title, StringComparer.OrdinalIgnoreCase),
                MixSortType.Artist => OrderBy(entry => available[entry.SongId].Artist, StringComparer.OrdinalIgnoreCase),
                MixSortType.Album => OrderBy(entry => available[entry.SongId].AlbumTitle, StringComparer.OrdinalIgnoreCase),
                MixSortType.Length => OrderBy(entry => available[entry.SongId].Duration),
                MixSortType.TimeAdded => OrderBy(entry => entry.AddedAt),
                _ => OrderBy(entry => entry.Position)
            };

            return sorted.ThenBy(entry => entry.Position).ThenBy(entry => entry.SongId)
                .Select(entry => available[entry.SongId]).ToArray();
        }
        #endregion

        #region Album Methods
        public Album CreateAlbum(string title, string artist) {
            Album album = new() {
                Title = title,
                Artist = artist
            };
            albums.Add(album);
            return album;
        }

        public bool AddAlbum(Album album) {
            if (albums.Any(a => a.Id == album.Id)) return false;
            albums.Add(album);
            return true;
        }

        public bool RemoveAlbum(Guid albumId) {
            Album? album = albums.FirstOrDefault(a => a.Id == albumId);
            if (album == null) return false;

            foreach (Song song in songs) {
                if (song.AlbumId != albumId) continue;
                song.AlbumId = null;
            }
            album.Songs.Clear();
            return albums.Remove(album);
        }

        public Album? GetAlbum(Guid albumId) => albums.FirstOrDefault(a => a.Id == albumId);

        public Album GetOrCreateAlbum(Song song) {
            Album? album = albums.FirstOrDefault(a =>
                string.Equals(a.Title, song.AlbumTitle, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(a.Artist, song.AlbumArtist, StringComparison.OrdinalIgnoreCase)
            );
            if (album != null) return album;
            return CreateAlbum(song.AlbumTitle, song.AlbumArtist);
        }

        public bool MoveSongToAlbum(Guid songId, Guid albumId) {
            Song? song = songs.FirstOrDefault(s => s.Id == songId);
            Album? album = albums.FirstOrDefault(a => a.Id == albumId);
            if (song == null || album == null) return false;

            foreach (Album existingAlbum in albums) {
                existingAlbum.Songs.RemoveAll(s => s.Id == songId);
            }
            song.AlbumId = albumId;
            if (!album.Songs.Any(s => s.Id == songId)) album.Songs.Add(song);
            return true;
        }

        public bool RenameAlbum(Guid albumId, string newTitle) {
            Album? album = GetAlbum(albumId);
            if (album == null || string.IsNullOrEmpty(newTitle)) return false;

            album.Title = newTitle.Trim();
            return true;
        }

        public bool SetAlbumArtist(Guid albumId, string newArtist) {
            Album? album = GetAlbum(albumId);
            if (album == null || string.IsNullOrEmpty(newArtist)) return false;

            album.Artist = newArtist.Trim();
            return true;
        }
        #endregion

        #region Song Methods
        /// <returns>True if the song was successfully added to the library</returns>
        public bool AddSong(Song song) {
            if (song == null) return false;

            bool alreadyExists = songs.Any(existing =>
                string.Equals(existing.FilePath, song.FilePath, StringComparison.OrdinalIgnoreCase)
            );
            if (alreadyExists) return false;

            songs.Add(song);
            if (song.AlbumId.HasValue) {
                Album? album = albums.FirstOrDefault(a => a.Id == song.AlbumId.Value);
                if (album != null && !album.Songs.Any(s => s.Id == song.Id)) album.Songs.Add(song);
            }
            return true;
        }

        /// <returns>The amount of songs that were sucessfully added to the library</returns>
        public int AddSongs(IEnumerable<Song> newSongs) {
            int addedCount = 0;
            foreach (Song song in newSongs) {
                if (AddSong(song)) addedCount++;
            }
            return addedCount;
        }

        /// <returns>True if the song was successfully removed from the library</returns>
        public bool RemoveSong(Guid songId) {
            Song? song = songs.FirstOrDefault(s => s.Id == songId);
            if (song == null) return false;

            foreach (Album album in albums) {
                album.Songs.RemoveAll(s => s.Id == songId);
            }
            foreach (Mix mix in mixes) {
                mix.RemoveSongs([songId]);
            }
            return songs.Remove(song);
        }
        #endregion
    }
}