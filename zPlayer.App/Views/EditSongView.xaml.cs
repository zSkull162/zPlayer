using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using zPlayer.App.Converters;
using zPlayer.Core;
using zPlayer.Infrastructure;

namespace zPlayer.App.Views
{
    public partial class EditSongView : UserControl
    {
        private readonly Song draft;
        private readonly Album unassignedAlbum;
        private bool initializing = true;
        private byte[]? customArtwork;

        public event Action? CloseRequested;
        public event Action<Song>? SaveRequested;

        public EditSongView(Song song, MusicLibrary library) {
            draft = song.Clone();
            customArtwork = draft.Artwork?.ToArray();
            InitializeComponent();
            DataContext = draft;

            unassignedAlbum = new Album {
                Title = "(No Album)",
                Artist = ""
            };

            List<Album> choices = library.GetAlbums();
            choices.Insert(0, unassignedAlbum);
            AlbumBox.ItemsSource = choices;
            AlbumBox.SelectedItem = choices.FirstOrDefault(album => album.Id == draft.AlbumId) ?? unassignedAlbum;
            ArtworkModeBox.SelectedIndex = 2;
            BrowseArtworkButton.Visibility = Visibility.Visible;
            initializing = false;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) {
            CloseRequested?.Invoke();
        }

        private void Save_Click(object sender, RoutedEventArgs e) {
            string title = draft.Title.Trim();
            string artist = draft.Artist.Trim();

            if (string.IsNullOrWhiteSpace(title) ||
                string.IsNullOrWhiteSpace(artist)) {
                MessageBox.Show("Enter a title and artist.", "Missing Song Information", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            draft.Title = title;
            draft.Artist = artist;

            Album? selectedAlbum = AlbumBox.SelectedItem as Album;
            Guid? selectedAlbumId = selectedAlbum == null || ReferenceEquals(selectedAlbum, unassignedAlbum) ? null : selectedAlbum.Id;

            if (draft.AlbumId != selectedAlbumId) {
                draft.AlbumId = selectedAlbumId;

                draft.AlbumTitle = selectedAlbumId.HasValue ? selectedAlbum!.Title : "Unknown Album";
                draft.AlbumArtist = selectedAlbumId.HasValue ? selectedAlbum!.Artist : "Unknown Artist";
            }
            if (!RefreshArtwork()) return;
            SaveRequested?.Invoke(draft);
        }

        private void ArtworkModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (initializing) return;
            RefreshArtwork();
        }

        private void AlbumBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (initializing) return;
            if (ArtworkModeBox.SelectedIndex == 1) RefreshArtwork();
        }

        private bool RefreshArtwork() {
            BrowseArtworkButton.Visibility = ArtworkModeBox.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
            try {
                switch (ArtworkModeBox.SelectedIndex) {
                    case 0: // Embedded in File
                        AudiofileMetadataReader reader = new();
                        draft.Artwork = reader.Read(draft.FilePath).Artwork;
                        break;
                    case 1: // Same as Album
                        Album? album = AlbumBox.SelectedItem as Album;
                        draft.Artwork = album == null || ReferenceEquals(album, unassignedAlbum) ? null : album.GetArtwork()?.ToArray();
                        break;
                    case 2: // Custom Image
                        draft.Artwork = customArtwork;
                        break;
                    default:
                        return false;
                }
                return true;
            }
            catch (Exception ex) {
                draft.Artwork = null;
                MessageBox.Show($"Could not load artwork:\n{ex.Message}", "Artwork Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void BrowseArtwork_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog dialog = new() {
                Title = "Choose Song Artwork",
                Filter = "Image Files|*.png;*.jpg;*.jpeg",
                Multiselect = false
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

            try {
                byte[] bytes = File.ReadAllBytes(dialog.FileName);
                new ArtworkConverter().BmpFromBytes(bytes);
                customArtwork = bytes;
                RefreshArtwork();
            }
            catch (Exception ex) {
                MessageBox.Show($"Could not load image:\n{ex.Message}", "Artwork Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
