using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using zPlayer.Core;

namespace zPlayer.App.Views
{
    public partial class MoveToAlbumView : UserControl
    {
        private readonly Album unassignedAlbum = new() {
            Title = "(No Album)",
            Artist = ""
        };

        public Album? SourceAlbum { get; }
        public event Action? CloseRequested;
        public event Action<Album?>? MoveRequested;

        public MoveToAlbumView(MusicLibrary library, Song song) : this(library, [song]) { }

        public MoveToAlbumView(MusicLibrary library, Song[] songs) {
            if (songs.Length == 0) return;

            bool sameAlbum = songs.Select(song => song.AlbumId).Distinct().Count() == 1;
            Guid? sourceId = sameAlbum ? songs[0].AlbumId : null;

            SourceAlbum = sourceId.HasValue ? library.GetAlbum(sourceId.Value) : null;
            InitializeComponent();

            DataContext = new {
                Title = songs.Length == 1 ? songs[0].Title : $"{songs.Length} selected songs",
                AlbumTitle = sameAlbum ? songs[0].AlbumTitle : "(Multiple albums)"
            };

            List<Album> choices = library.GetAlbums().Where(album => !sameAlbum || album.Id != sourceId).ToList();
            if (songs.Any(song => song.AlbumId.HasValue)) {
                choices.Insert(0, unassignedAlbum);
            }
            AlbumBox.ItemsSource = choices;
            AlbumBox.SelectedIndex = -1;
            AlbumBox.SelectedIndex = -1;
        }

        private void AlbumBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (MoveButton == null) return;
            MoveButton.IsEnabled = AlbumBox.SelectedItem is Album;
        }

        private void Move_Click(object sender, RoutedEventArgs e) {
            if (AlbumBox.SelectedItem is Album destination) {
                MoveRequested?.Invoke(ReferenceEquals(destination, unassignedAlbum) ? null : destination);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) {
            CloseRequested?.Invoke();
        }
    }
}