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
    public partial class EditMixView : UserControl
    {
        private readonly Mix draft;
        private byte[]? customArtwork;
        private bool initializing = true;

        public event Action? CloseRequested;
        public event Action<Mix>? SaveRequested;

        public EditMixView(MusicLibrary library, Mix mix) {
            draft = mix.Clone();
            draft.RefreshSongs(library.Songs);
            customArtwork = draft.CustomArtwork?.ToArray();

            InitializeComponent();
            DataContext = draft;

            ArtworkModeBox.SelectedIndex = customArtwork is { Length: > 0 } ? 1 : 0;
            initializing = false;
            RefreshArtwork();
        }

        private void ArtworkModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (!initializing) RefreshArtwork();
        }

        private void RefreshArtwork() {
            bool custom = ArtworkModeBox.SelectedIndex == 1;
            BrowseArtworkButton.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
            draft.CustomArtwork = custom ? customArtwork : null;

            BindingOperations.GetBindingExpression(ArtworkPreview, ImageBrush.ImageSourceProperty)?.UpdateTarget();
        }

        private void BrowseArtwork_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog dialog = new() {
                Title = "Choose Mix Artwork",
                Filter = "Image Files|*.png;*.jpg;*.jpeg"
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

            try {
                byte[] bytes = File.ReadAllBytes(dialog.FileName);
                new ArtworkConverter().BmpFromBytes(bytes);
                customArtwork = bytes;
                RefreshArtwork();
            }
            catch (Exception ex) {
                MessageBox.Show(Window.GetWindow(this), ex.Message, "Artwork Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e) {
            string title = TitleTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(title)) {
                title = "No Title";
            }
            if (ArtworkModeBox.SelectedIndex == 1 && customArtwork is not { Length: > 0 }) {
                MessageBox.Show("Choose a custom image.");
                return;
            }
            draft.Title = title;
            RefreshArtwork();
            SaveRequested?.Invoke(draft);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) {
            CloseRequested?.Invoke();
        }
    }
}