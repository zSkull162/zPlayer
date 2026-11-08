using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace zPlayer.Core
{
    public class Song : INotifyPropertyChanged
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        private Guid? albumId;
        private int albumPosIndex;
        private string title  = "Unknown Title";
        private string artist  = "Unknown Artist";
        private string albumTitle  = "Unknown Album";
        private string albumArtist  = "Unknown Artist";
        private byte[]? artwork;
        private ArtworkRef? cover;

        public ArtworkRef? Cover {
            get => cover;
            set => SetField(ref cover, value);
        }
        private string filePath  = string.Empty;
        private TimeSpan duration;

        #region Public fields
        public Guid? AlbumId {
            get => albumId;
            set => SetField(ref albumId, value);
        }
        public int AlbumPosIndex {
            get => albumPosIndex;
            set => SetField(ref albumPosIndex, value);
        }
        public string Title {
            get => title;
            set => SetField(ref title, value);
        }
        public string Artist {
            get => artist;
            set => SetField(ref artist, value);
        }
        public string AlbumTitle {
            get => albumTitle;
            set => SetField(ref albumTitle, value);
        }
        public string AlbumArtist {
            get => albumArtist;
            set => SetField(ref albumArtist, value);
        }
        public byte[]? Artwork {
            get => artwork;
            set => SetField(ref artwork, value);
        }
        public string FilePath {
            get => filePath;
            set => SetField(ref filePath, value);
        }
        public TimeSpan Duration {
            get => duration;
            set => SetField(ref duration, value);
        }
        #endregion

        public event PropertyChangedEventHandler? PropertyChanged;

        private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public Song Clone() {
            Song copy = (Song)MemberwiseClone();
            copy.PropertyChanged = null;
            copy.artwork = artwork?.ToArray();
            return copy;
        }

        public void ApplyEdits(Song edited) {
            Title = edited.Title;
            Artist = edited.Artist;
            Cover = edited.Cover;
            AlbumId = edited.AlbumId;
            AlbumTitle = edited.AlbumTitle;
            AlbumArtist = edited.AlbumArtist;
            AlbumPosIndex = edited.AlbumPosIndex;
        }
    }
}