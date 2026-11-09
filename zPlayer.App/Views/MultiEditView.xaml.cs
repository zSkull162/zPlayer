using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using zPlayer.App.Converters;

namespace zPlayer.App.Views
{
    public enum MultiArtworkMode {
        Keep, Automatic, SameAsAlbum, Custom
    }

    public sealed record MultiEditChanges(string? Artist, MultiArtworkMode ArtworkMode, byte[]? Artwork);

    public partial class MultiEditView : UserControl
    {
        private byte[]? artwork;
        private bool editingAlbums;

        public event Action? CloseRequested;
        public event Action<MultiEditChanges>? SaveRequested;

        public MultiEditView(int count, bool editingAlbums) {
            this.editingAlbums = editingAlbums;
            InitializeComponent();

            ViewTitle.Text = $"Editing {count} {(editingAlbums ? "Albums" : "Songs")}";

            ArtistTextBox.Text = "-";
            AutomaticArtworkItem.Content = editingAlbums ? "Automatic" : "Embedded in each file";
            SameAsAlbumItem.Visibility = editingAlbums ? Visibility.Collapsed : Visibility.Visible;
            SameAsAlbumItem.IsEnabled = !editingAlbums;

            CustomImageFilePathText.Text = "No image selected";
            CustomImageFilePathText.Visibility = Visibility.Collapsed;
        }

        private void ArtworkModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (BrowseArtworkButton == null) return;
            bool customMode = ArtworkModeBox.SelectedIndex == 3;

            BrowseArtworkButton.Visibility = customMode ? Visibility.Visible : Visibility.Collapsed;
            CustomImageFilePathText.Visibility = customMode ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BrowseArtwork_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog dialog = new() {
                Title = "Choose Artwork",
                Filter = "Image Files|*.png;*.jpg;*.jpeg"
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

            try {
                byte[] bytes = File.ReadAllBytes(dialog.FileName);
                new ArtworkConverter().BmpFromBytes(bytes);
                artwork = bytes;
                CustomImageFilePathText.Text = Path.GetFileName(dialog.FileName);
            }
            catch (Exception ex) {
                MessageBox.Show(Window.GetWindow(this), ex.Message, "Artwork Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e) {
            string? artist;
            if (ArtistTextBox.Text == "-") artist = null;
            else artist = ArtistTextBox.Text.Trim();

            MultiArtworkMode mode = (MultiArtworkMode)ArtworkModeBox.SelectedIndex;

            if (artist != null && string.IsNullOrWhiteSpace(artist)) {
                artist = "Unknown Artist";
            }
            if (mode == MultiArtworkMode.Custom && artwork == null) {
                MessageBox.Show("Choose an image.");
                return;
            }
            SaveRequested?.Invoke(new MultiEditChanges(artist, mode, artwork?.ToArray()));
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) {
            CloseRequested?.Invoke();
        }
    }
}
