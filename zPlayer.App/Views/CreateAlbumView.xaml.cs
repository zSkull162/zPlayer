using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using zPlayer.App.Converters;
using zPlayer.Core;

namespace zPlayer.App.Views
{
    public partial class CreateAlbumView : UserControl
    {
        private readonly Album draft = new() {
            Title = "",
            Artist = ""
        };

        private readonly ArtworkConverter artworkConverter = new();

        private byte[]? customArtwork;
        private bool initializing = true;

        public event Action? CloseRequested;
        public event Action<Album>? CreateRequested;

        public CreateAlbumView() {
            InitializeComponent();
            DataContext = draft;
            initializing = false;
            RefreshArtwork();
            Loaded += (_, _) => TitleTextBox.Focus();
        }

        private void ArtworkModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (initializing) return;
            RefreshArtwork();
        }

        private void RefreshArtwork() {
            bool useCustomArtwork = ArtworkModeBox.SelectedIndex == 1;
            BrowseArtworkButton.Visibility = useCustomArtwork ? Visibility.Visible : Visibility.Collapsed;
            draft.CustomArtwork = useCustomArtwork ? customArtwork : null;

            draft.ArtworkSongId = null;
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

        private void Create_Click(object sender, RoutedEventArgs e) {
            string title = TitleTextBox.Text.Trim();
            string artist = ArtistTextBox.Text.Trim();

            draft.Title = string.IsNullOrWhiteSpace(title) ? "No Title" : title;
            draft.Artist = string.IsNullOrWhiteSpace(artist) ? "Unknown Artist" : artist;

            RefreshArtwork();
            CreateRequested?.Invoke(draft);
        }
    }
}