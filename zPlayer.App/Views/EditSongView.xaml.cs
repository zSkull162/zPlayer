using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using zPlayer.App.Converters;
using zPlayer.Core;
using zPlayer.Infrastructure;
using zPlayer.Infrastructure.Storage;

namespace zPlayer.App.Views
{
    public partial class EditSongView : UserControl
    {
        private readonly Song draft;
        private readonly MusicLibraryDatabase database;
        private int artworkRequest;
        private bool saving;
        private readonly Album unassignedAlbum;
        private bool initializing = true;
        private byte[]? customArtwork;

        public event Action? CloseRequested;
        public event Action<Song>? SaveRequested;

        public EditSongView(Song song, MusicLibrary library, byte[]? originalArtwork, MusicLibraryDatabase database) {
            this.database = database;
            draft = song.Clone();
            customArtwork = originalArtwork;
            draft.Artwork = originalArtwork;
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

        private async void Save_Click(object sender, RoutedEventArgs e) {
            if (saving) return;
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
            saving = true;
            IsEnabled = false;
            try {
                if (await RefreshArtwork() && IsLoaded) SaveRequested?.Invoke(draft);
            }
            finally {
                IsEnabled = true;
                saving = false;
            }
        }

        private void ArtworkModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (initializing) return;
            _ = RefreshArtwork();
        }

        private void AlbumBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (initializing) return;
            if (ArtworkModeBox.SelectedIndex == 1) _ = RefreshArtwork();
        }

        private async Task<bool> RefreshArtwork() {
            int request = ++artworkRequest;
            byte[]? bytes;
            BrowseArtworkButton.Visibility = ArtworkModeBox.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
            try {
                switch (ArtworkModeBox.SelectedIndex) {
                    case 0: // Embedded in File
                        AudiofileMetadataReader reader = new();
                        bytes = await Task.Run(() => reader.Read(draft.FilePath).Artwork);
                        break;
                    case 1: // Same as Album
                        Album? album = AlbumBox.SelectedItem as Album;
                        bytes = album?.Cover is { } cover ? await Task.Run(() => database.LoadArtwork(cover))
                            ?? throw new InvalidOperationException("The album cover is unavailable.") : null;
                        break;
                    case 2: // Custom Image
                        bytes = customArtwork;
                        break;
                    default:
                        return false;
                }
                if (request != artworkRequest || !IsLoaded) return false;
                draft.Artwork = bytes;
                return true;
            }
            catch (Exception ex) {
                if (request != artworkRequest || !IsLoaded) return false;
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
                _ = RefreshArtwork();
            }
            catch (Exception ex) {
                MessageBox.Show($"Could not load image:\n{ex.Message}", "Artwork Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
