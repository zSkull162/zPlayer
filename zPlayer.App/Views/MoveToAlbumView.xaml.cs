using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using zPlayer.Core;

namespace zPlayer.App.Views
{
    public partial class MoveToAlbumView : UserControl
    {
        private readonly Song draft;

        public Album? SourceAlbum { get; }
        public event Action? CloseRequested;
        public event Action<Album>? MoveRequested;

        public MoveToAlbumView(MusicLibrary library, Song song) {
            draft = song.Clone();
            SourceAlbum = song.AlbumId.HasValue ? library.GetAlbum(song.AlbumId.Value) : null;
            InitializeComponent();
            DataContext = draft;

            AlbumBox.ItemsSource = library.GetAlbums().Where(album => album.Id != song.AlbumId).ToList();
            AlbumBox.SelectedIndex = -1;
        }

        private void AlbumBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (MoveButton == null) return;
            MoveButton.IsEnabled = AlbumBox.SelectedItem is Album;
        }

        private void Move_Click(object sender, RoutedEventArgs e) {
            if (AlbumBox.SelectedItem is Album destination) MoveRequested?.Invoke(destination);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) {
            CloseRequested?.Invoke();
        }
    }
}