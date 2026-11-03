using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using zPlayer.App.Converters;
using zPlayer.Core;

namespace zPlayer.App.Views
{
    public partial class EditAlbumView : UserControl
    {
        private readonly Album draft;
        private readonly ArtworkConverter artworkConverter = new();

        private byte[]? customArtwork;
        private bool initializing = true;

        public event Action? CloseRequested;
        public event Action<Album>? SaveRequested;

        public EditAlbumView(Album album) {
            draft = album.Clone();
            customArtwork = draft.CustomArtwork?.ToArray();

            InitializeComponent();
            DataContext = draft;

            var artworkSongs = draft.Songs.Where(song => song.Artwork != null && song.Artwork.Length > 0).ToList();

            ArtworkSongBox.ItemsSource = artworkSongs;
            ArtworkSongBox.SelectedItem = artworkSongs.FirstOrDefault(song => song.Id == draft.ArtworkSongId) ?? artworkSongs.FirstOrDefault();
            ArtworkModeBox.SelectedIndex = customArtwork is { Length: > 0 } ? 2 : draft.ArtworkSongId.HasValue ? 1 : 0;

            initializing = false;
            RefreshArtwork();
        }

        private void ArtworkModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (initializing) return;
            RefreshArtwork();
        }

        private void ArtworkSongBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (initializing) return;
            if (ArtworkModeBox.SelectedIndex == 1) RefreshArtwork();
        }

        private void RefreshArtwork() {
            int mode = ArtworkModeBox.SelectedIndex;
            BrowseArtworkButton.Visibility = mode == 2 ? Visibility.Visible : Visibility.Collapsed;
            SongSelector.Visibility = mode == 1 ? Visibility.Visible : Visibility.Collapsed;

            switch (mode) {
                case 0: // Automatic
                    draft.CustomArtwork = null;
                    draft.ArtworkSongId = null;
                    break;
                case 1: // From Song
                    draft.CustomArtwork = null;
                    draft.ArtworkSongId =
                        (ArtworkSongBox.SelectedItem as Song)?.Id;
                    break;
                case 2: // Custom Image
                    draft.CustomArtwork = customArtwork;
                    draft.ArtworkSongId = null;
                    break;
            }
            BindingOperations.GetBindingExpression(ArtworkPreview, ImageBrush.ImageSourceProperty)?.UpdateTarget();
        }

        private void BrowseArtwork_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog dialog = new() {
                Title = "Choose Album Artwork",
                Filter = "Image Files|*.png;*.jpg;*.jpeg",
                Multiselect = false
            };

            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            try {
                byte[] bytes = File.ReadAllBytes(dialog.FileName);
                artworkConverter.BmpFromBytes(bytes);
                customArtwork = bytes;
                RefreshArtwork();
            }
            catch (Exception ex) {
                MessageBox.Show($"Could not load image:\n{ex.Message}", "Artwork Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) {
            CloseRequested?.Invoke();
        }

        private void Save_Click(object sender, RoutedEventArgs e) {
            string title = TitleTextBox.Text.Trim();
            string artist = ArtistTextBox.Text.Trim();

            if (ArtworkModeBox.SelectedIndex == 1 && ArtworkSongBox.SelectedItem is not Song) {
                MessageBox.Show("This album has no songs with artwork. Select Automatic or Custom Image.", "Missing Artwork", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (ArtworkModeBox.SelectedIndex == 2 && customArtwork is not { Length: > 0 }) {
                MessageBox.Show("Choose a custom image.", "Missing Artwork", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            draft.Title = string.IsNullOrWhiteSpace(title) ? "Unknown Title" : title;
            draft.Artist = string.IsNullOrWhiteSpace(artist) ? "Unknown Artist" : artist;

            RefreshArtwork();
            SaveRequested?.Invoke(draft);
        }
    }
}