using System.IO;
using Microsoft.Data.Sqlite;
using zPlayer.Core;

namespace zPlayer.Infrastructure.Storage
{
    public class MusicLibraryDatabase
    {
        private readonly string connectionString;

        public MusicLibraryDatabase() {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "zPlayer");
            Directory.CreateDirectory(folder);

            string databasePath = Path.Combine(folder, "library.db");
            connectionString = new SqliteConnectionStringBuilder {
                DataSource = databasePath,
                ForeignKeys = true
            }.ToString();
        }

        public void Initalize() {
            using SqliteConnection connection = new(connectionString);
            connection.Open();

            using SqliteTransaction transaction = connection.BeginTransaction();
            using (SqliteCommand command = connection.CreateCommand()) {
                command.Transaction = transaction;
                command.CommandText = """
                    CREATE TABLE IF NOT EXISTS Songs (
                        Id TEXT PRIMARY KEY,
                        Title TEXT NOT NULL,
                        Artist TEXT NOT NULL,
                        FilePath TEXT NOT NULL UNIQUE,
                        DurationTicks INTEGER NOT NULL,
                        AlbumTitle TEXT NOT NULL,
                        AlbumArtist TEXT NOT NULL,
                        Artwork BLOB,
                        AlbumId TEXT,
                        AlbumPosIndex INTEGER NOT NULL DEFAULT 0
                    );

                    CREATE TABLE IF NOT EXISTS Albums (
                        Id TEXT PRIMARY KEY,
                        Title TEXT NOT NULL,
                        Artist TEXT NOT NULL,
                        CustomArtwork BLOB,
                        ArtworkSongId TEXT
                    );

                    CREATE TABLE IF NOT EXISTS PlaybackSettings (
                        Id INTEGER PRIMARY KEY CHECK (Id = 1),
                        Volume REAL NOT NULL
                    );

                    CREATE TABLE IF NOT EXISTS Mixes (
                        Id TEXT PRIMARY KEY,
                        Title TEXT NOT NULL,
                        SortType INTEGER NOT NULL DEFAULT 5,
                        SortDescending INTEGER NOT NULL DEFAULT 0
                    );

                    CREATE TABLE IF NOT EXISTS MixSongs (
                        MixId TEXT NOT NULL,
                        SongId TEXT NOT NULL,
                        Position INTEGER NOT NULL CHECK(Position >= 0),
                        AddedUtcTicks INTEGER NOT NULL,
                        PRIMARY KEY (MixId, SongId),
                        UNIQUE (MixId, Position),
                        FOREIGN KEY (MixId) REFERENCES Mixes(Id) ON DELETE CASCADE,
                        FOREIGN KEY (SongId) REFERENCES Songs(Id) ON DELETE CASCADE
                    );

                    CREATE INDEX IF NOT EXISTS IX_MixSongs_SongId ON MixSongs(SongId);
                """;
                command.ExecuteNonQuery();
            }
            transaction.Commit();
        }

        public void ClearLibrary() {
            using SqliteConnection connection = new(connectionString);
            connection.Open();

            using SqliteTransaction transaction = connection.BeginTransaction();
            using SqliteCommand command = connection.CreateCommand();

            command.Transaction = transaction;
            command.CommandText = """
                DELETE FROM MixSongs;
                DELETE FROM Mixes;
                DELETE FROM Songs;
                DELETE FROM Albums;
            """;
            command.ExecuteNonQuery();
            transaction.Commit();
        }

        public double LoadVolume() {
            using SqliteConnection connection = new(connectionString);
            connection.Open();

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT Volume FROM PlaybackSettings WHERE Id = 1;";

            object? result = command.ExecuteScalar(); 
            if (result == null || result == DBNull.Value) return 0.5f;

            double volume = Convert.ToDouble(result);
            return double.IsFinite(volume) ? Math.Clamp(volume, 0, 1) : 0.5;
        }

        public void SaveVolume(double volume) {
            using SqliteConnection connection = new(connectionString);
            connection.Open();

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO PlaybackSettings (Id, Volume)
                VALUES (1, $volume)
                ON CONFLICT(Id) DO UPDATE SET
                Volume = excluded.Volume;
            """;

            command.Parameters.AddWithValue( "$volume", Math.Clamp(volume, 0, 1));
            command.ExecuteNonQuery();
        }

        #region Save/Load Mix Methods
        public void SaveMix(Mix mix) => SaveMixes([mix]);

        public void SaveMixes(IEnumerable<Mix> mixes) {
            Mix[] drafts = mixes.Select(mix => mix.Clone()).ToArray();

            if (drafts.Select(mix => mix.Id).Distinct().Count() != drafts.Length) {
                throw new ArgumentException("The same mix cannot be saved twice in one batch.", nameof(mixes));
            }

            foreach (Mix mix in drafts) {
                if (mix.Id == Guid.Empty || string.IsNullOrWhiteSpace(mix.Title)) {
                    throw new ArgumentException("Each mix needs an ID and title.", nameof(mixes));
                }
                if (mix.Entries.Any(entry => entry.SongId == Guid.Empty || entry.Position < 0) ||
                    mix.Entries.Select(entry => entry.SongId).Distinct().Count() != mix.Entries.Count ||
                    mix.Entries.Select(entry => entry.Position).Distinct().Count() != mix.Entries.Count) {
                    throw new ArgumentException("Mix songs must have valid IDs and unique, nonnegative positions.", nameof(mixes));
                }
            }

            using SqliteConnection connection = new(connectionString);
            connection.Open();
            using SqliteTransaction transaction = connection.BeginTransaction();

            foreach (Mix mix in drafts) {
                using (SqliteCommand command = connection.CreateCommand()) {
                    command.Transaction = transaction;
                    command.CommandText = """
                        INSERT INTO Mixes (Id, Title, SortType, SortDescending)
                        VALUES ($id, $title, $sortType, $sortDescending)
                        ON CONFLICT(Id) DO UPDATE SET
                            Title = excluded.Title,
                            SortType = excluded.SortType,
                            SortDescending = excluded.SortDescending;
                    """;
                    command.Parameters.AddWithValue("$id", mix.Id.ToString());
                    command.Parameters.AddWithValue("$title", mix.Title);
                    command.Parameters.AddWithValue("sortType", (int)mix.SortType);
                    command.Parameters.AddWithValue("$sortDescending", mix.SortDescending ? 1 : 0);
                    command.ExecuteNonQuery();
                }

                using (SqliteCommand command = connection.CreateCommand()) {
                    command.Transaction = transaction;
                    command.CommandText = "DELETE FROM MixSongs WHERE MixId = $mixId;";
                    command.Parameters.AddWithValue("$mixId", mix.Id.ToString());
                    command.ExecuteNonQuery();
                }

                using (SqliteCommand command = connection.CreateCommand()) {
                    command.Transaction = transaction;
                    command.CommandText = """
                        INSERT INTO MixSongs (MixId, SongId, Position, AddedUtcTicks)
                        VALUES ($mixId, $songId, $position, $addedUtcTicks);
                    """;

                    command.Parameters.AddWithValue("$mixId", mix.Id.ToString());
                    SqliteParameter songId = command.Parameters.Add("$songId", SqliteType.Text);
                    SqliteParameter position = command.Parameters.Add("$position", SqliteType.Integer);
                    SqliteParameter addedAt = command.Parameters.Add("$addedUtcTicks", SqliteType.Integer);

                    foreach (MixSong entry in mix.Entries) {
                        songId.Value = entry.SongId.ToString();
                        position.Value = entry.Position;
                        addedAt.Value = entry.AddedAt.UtcDateTime.Ticks;
                        command.ExecuteNonQuery();
                    }
                }
            }
            transaction.Commit();
        }

        public List<Mix> LoadMixes() {
            List<Mix> mixes = [];
            Dictionary<Guid, Mix> lookup = [];

            using SqliteConnection connection = new(connectionString);
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();

            command.CommandText = """
                SELECT m.Id, m.Title, ms.SongId, ms.Position, ms.AddedUtcTicks,
                m.SortType, m.SortDescending
                FROM Mixes AS m
                LEFT JOIN MixSongs AS ms ON ms.MixId = m.Id
                ORDER BY m.Title COLLATE NOCASE, m.Id, ms.Position;
            """;

            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read()) {
                Guid mixId = Guid.Parse(reader.GetString(0));

                if (!lookup.TryGetValue(mixId, out Mix? mix)) {
                    mix = new Mix {
                        Id = mixId,
                        Title = reader.GetString(1),
                        SortType = (MixSortType)reader.GetInt32(5),
                        SortDescending = reader.GetInt32(6) != 0
                    };
                    lookup.Add(mixId, mix);
                    mixes.Add(mix);
                }
                if (!reader.IsDBNull(2)) {
                    mix.Entries.Add(new MixSong(Guid.Parse(reader.GetString(2)), reader.GetInt32(3), new DateTimeOffset(reader.GetInt64(4), TimeSpan.Zero)));
                }
            }
            return mixes;
        }

        public void DeleteMix(Guid mixId) => DeleteMixes([mixId]);

        public void DeleteMixes(IEnumerable<Guid> mixIds) {
            using SqliteConnection connection = new(connectionString);
            connection.Open();
            using SqliteTransaction transaction = connection.BeginTransaction();
            using SqliteCommand command = connection.CreateCommand();

            command.Transaction = transaction;
            command.CommandText = "DELETE FROM Mixes WHERE Id = $id;";
            SqliteParameter id = command.Parameters.Add("$id", SqliteType.Text);

            foreach (Guid mixId in mixIds.Distinct()) {
                id.Value = mixId.ToString();
                command.ExecuteNonQuery();
            }
            transaction.Commit();
        }
        #endregion

        #region Save/Load Album Methods
        public void DeleteAlbum(Guid albumId) {
            using SqliteConnection connection = new(connectionString);
            connection.Open();

            using SqliteTransaction transaction = connection.BeginTransaction();
            using (SqliteCommand command = connection.CreateCommand()) {
                command.Transaction = transaction;
                command.CommandText = """
                    UPDATE Songs
                    SET AlbumId = NULL
                    WHERE AlbumId = $albumId;
                """;
                command.Parameters.AddWithValue("$albumId", albumId.ToString());
                command.ExecuteNonQuery();
            }
            using (SqliteCommand command = connection.CreateCommand()) {
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM Albums WHERE Id = $albumId;";
                command.Parameters.AddWithValue("$albumId", albumId.ToString());
                command.ExecuteNonQuery();
            }
            transaction.Commit();
        }

        public void DeleteAlbums(IEnumerable<Guid> albumIds) {
            using SqliteConnection connection = new(connectionString);
            connection.Open();
            using SqliteTransaction transaction = connection.BeginTransaction();
            using SqliteCommand command = connection.CreateCommand();

            command.Transaction = transaction;
            command.CommandText = """
                UPDATE Songs SET AlbumId = NULL WHERE AlbumId = $id;
                DELETE FROM Albums WHERE Id = $id;
            """;

            SqliteParameter id = command.Parameters.Add("$id", SqliteType.Text);

            foreach (Guid albumId in albumIds.Distinct()) {
                id.Value = albumId.ToString();
                command.ExecuteNonQuery();
            }
            transaction.Commit();
        }

        public void SaveAlbum(Album album) => SaveAlbums([album]);

        public void SaveAlbums(IEnumerable<Album> albums) {
            using SqliteConnection connection = new(connectionString);
            connection.Open();
            using SqliteTransaction transaction = connection.BeginTransaction();

            foreach (Album album in albums) {
                SaveAlbumCore(connection, transaction, album);
            }
            transaction.Commit();
        }

        public List<Album> LoadAlbums() {
            List<Album> albums = [];

            using SqliteConnection connection = new(connectionString);
            connection.Open();

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT Id, Title, Artist, CustomArtwork, ArtworkSongId
                FROM Albums;
            """;

            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read()) {
                Album album = new() {
                    Id = Guid.Parse(reader.GetString(0)),
                    Title = reader.GetString(1),
                    Artist = reader.GetString(2),
                    CustomArtwork = reader.IsDBNull(3) ? null : (byte[])reader.GetValue(3),
                    ArtworkSongId = reader.IsDBNull(4) ? null : Guid.Parse(reader.GetString(4))
                };
                albums.Add(album);
            }
            return albums;
        }

        public void SaveAlbumSongOrder(Guid albumId, IReadOnlyList<Guid> songIds) {
            using SqliteConnection connection = new(connectionString);
            connection.Open();

            using SqliteTransaction transaction = connection.BeginTransaction();
            HashSet<Guid> existingIds = [];

            using (SqliteCommand command = connection.CreateCommand()) {
                command.Transaction = transaction;
                command.CommandText = "SELECT Id FROM Songs WHERE AlbumId = $albumId;";
                command.Parameters.AddWithValue("$albumId", albumId.ToString());

                using SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read()) existingIds.Add(Guid.Parse(reader.GetString(0)));
            }

            if (songIds.Count != existingIds.Count || !existingIds.SetEquals(songIds)) {
                throw new InvalidOperationException("The song order does not match the album's current songs.");
            }

            using (SqliteCommand command = connection.CreateCommand()) {
                command.Transaction = transaction;
                command.CommandText = """
                    UPDATE Songs
                    SET AlbumPosIndex = $position
                    WHERE Id = $id AND AlbumId = $albumId;
                """;

                command.Parameters.AddWithValue("$albumId", albumId.ToString());
                SqliteParameter idParameter = command.Parameters.Add("$id", SqliteType.Text);
                SqliteParameter positionParameter = command.Parameters.Add("$position", SqliteType.Integer);

                for (int i = 0; i < songIds.Count; i++) {
                    idParameter.Value = songIds[i].ToString();
                    positionParameter.Value = i;
                    command.ExecuteNonQuery();
                }
            }
            transaction.Commit();
        }

        private void SaveAlbumCore(SqliteConnection connection, SqliteTransaction transaction, Album album) {
            using (SqliteCommand command = connection.CreateCommand()) {
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO Albums (Id, Title, Artist, CustomArtwork, ArtworkSongId)
                    VALUES ($id, $title, $artist, $artwork, $artworkSongId)
                    ON CONFLICT(Id) DO UPDATE SET
                        Title = excluded.Title,
                        Artist = excluded.Artist,
                        CustomArtwork = excluded.CustomArtwork,
                        ArtworkSongId = excluded.ArtworkSongId;
                """;

                command.Parameters.AddWithValue("$id", album.Id.ToString());
                command.Parameters.AddWithValue("$title", album.Title);
                command.Parameters.AddWithValue("$artist", album.Artist);
                command.Parameters.AddWithValue("$artwork", (object?)album.CustomArtwork ?? DBNull.Value);
                command.Parameters.AddWithValue("$artworkSongId", album.ArtworkSongId.HasValue ? album.ArtworkSongId.Value.ToString() : DBNull.Value);
                command.ExecuteNonQuery();
            }

            using (SqliteCommand command = connection.CreateCommand()) {
                command.Transaction = transaction;
                command.CommandText = """
                    UPDATE Songs
                    SET AlbumTitle = $title, AlbumArtist = $artist
                    WHERE AlbumId = $albumId;
                """;

                command.Parameters.AddWithValue("$title", album.Title);
                command.Parameters.AddWithValue("$artist", album.Artist);
                command.Parameters.AddWithValue("$albumId", album.Id.ToString());
                command.ExecuteNonQuery();
            }
        }
        #endregion

        #region Save/Load Song Methods
        public void DeleteSong(Guid songId) {
            using SqliteConnection connection = new(connectionString);
            connection.Open();

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Songs WHERE Id = $id;";
            command.Parameters.AddWithValue("$id", songId.ToString());
            command.ExecuteNonQuery();
        }

        public void DeleteSongs(IEnumerable<Guid> songIds) {
            using SqliteConnection connection = new(connectionString);
            connection.Open();
            using SqliteTransaction transaction = connection.BeginTransaction();
            using SqliteCommand command = connection.CreateCommand();

            command.Transaction = transaction;
            command.CommandText = "DELETE FROM Songs WHERE Id = $id;";
            SqliteParameter id = command.Parameters.Add("$id", SqliteType.Text);

            foreach (Guid songId in songIds.Distinct()) {
                id.Value = songId.ToString();
                command.ExecuteNonQuery();
            }
            transaction.Commit();
        }

        public void SaveSong(Song song) => SaveSongs([song]);

        public void SaveSongs(IEnumerable<Song> songs) {
            using SqliteConnection connection = new(connectionString);
            connection.Open();
            using SqliteTransaction transaction = connection.BeginTransaction();

            foreach (Song song in songs) {
                SaveSongCore(connection, transaction, song);
            }
            transaction.Commit();
        }

        public List<Song> LoadSongs() {
            List<Song> songs = [];
            using SqliteConnection connection = new SqliteConnection(connectionString);
            connection.Open();

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT Id, Title, Artist, FilePath,
                       DurationTicks, AlbumTitle,
                       AlbumArtist, Artwork, AlbumId, AlbumPosIndex
                FROM Songs
                ORDER BY AlbumId, AlbumPosIndex, rowid;
            """;

            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read()) {
                Song song = new() {
                    Id = Guid.Parse(reader.GetString(0)),
                    Title = reader.GetString(1),
                    Artist = reader.GetString(2),
                    FilePath = reader.GetString(3),
                    Duration = TimeSpan.FromTicks(reader.GetInt64(4)),
                    AlbumTitle = reader.GetString(5),
                    AlbumArtist = reader.GetString(6),
                    Artwork = reader.IsDBNull(7) ? null : (byte[])reader.GetValue(7),
                    AlbumId = reader.IsDBNull(8) ? null : Guid.Parse(reader.GetString(8)),
                    AlbumPosIndex = reader.GetInt32(9)
                };
                songs.Add(song);
            }
            return songs;
        }

        private static void SaveSongCore(SqliteConnection connection, SqliteTransaction transaction, Song song) {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = """
                INSERT INTO Songs (
                    Id, Title, Artist, FilePath,
                    DurationTicks, AlbumTitle,
                    AlbumArtist, Artwork,
                    AlbumId, AlbumPosIndex
                )
                VALUES (
                    $id, $title, $artist, $path,
                    $duration, $album, $albumArtist,
                    $artwork, $albumId, $albumPosIndex
                )
                ON CONFLICT(Id) DO UPDATE SET
                    Title = excluded.Title,
                    Artist = excluded.Artist,
                    FilePath = excluded.FilePath,
                    DurationTicks = excluded.DurationTicks,
                    AlbumTitle = excluded.AlbumTitle,
                    AlbumArtist = excluded.AlbumArtist,
                    Artwork = excluded.Artwork,
                    AlbumId = excluded.AlbumId,
                    AlbumPosIndex = excluded.AlbumPosIndex;
             """;

            command.Parameters.AddWithValue("$id", song.Id.ToString());
            command.Parameters.AddWithValue("$title", song.Title);
            command.Parameters.AddWithValue("$artist", song.Artist);
            command.Parameters.AddWithValue("$path", song.FilePath);
            command.Parameters.AddWithValue("$duration", song.Duration.Ticks);
            command.Parameters.AddWithValue("$album", song.AlbumTitle);
            command.Parameters.AddWithValue("$albumArtist", song.AlbumArtist);
            command.Parameters.AddWithValue("$artwork", (object?)song.Artwork ?? DBNull.Value);
            command.Parameters.AddWithValue("$albumId", song.AlbumId.HasValue ? song.AlbumId.Value.ToString() : DBNull.Value);
            command.Parameters.AddWithValue("$albumPosIndex", song.AlbumPosIndex);

            command.ExecuteNonQuery();
        }
        #endregion
    }
}