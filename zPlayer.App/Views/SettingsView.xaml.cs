using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace zPlayer.App.Views
{
    public partial class SettingsView : UserControl
    {
        private readonly BitmapImage playIcon = new(new Uri("pack://application:,,,/Assets/Icons/Play_32x.png"));
        private readonly BitmapImage pauseIcon = new(new Uri("pack://application:,,,/Assets/Icons/Pause_32x.png"));

        private bool previewIsPaused;

        public SettingsView() {
            InitializeComponent();
            previewIsPaused = false;
            PreviewPlayPauseIcon.Source = pauseIcon;
            PreviewPlayPauseButton.Visibility = Visibility.Collapsed;
            UpdateRpcPreview();
        }

        private void RpcStatusBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            UpdateRpcPreview();
        }

        private void UpdateRpcPreview() {
            if (PreviewUserArtistText == null) return;

            bool enabled = RpcToggle.IsChecked == true;
            bool displayAlbum = RpcDisplayAlbumToggle.IsChecked == true;

            RpcOptions.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            if (!enabled) return;
            PreviewAlbumText.Visibility = displayAlbum ? Visibility.Visible : Visibility.Collapsed;
            UpdateRpcPausedState();

            PreviewUserArtistText.Text = RpcStatusBox.SelectedIndex switch {
                1 => "Song title",
                2 => "Song artist",
                _ => "zPlayer"
            };
        }

        private void UpdateRpcPausedState() {
            bool displayPaused = RpcDisplayPausedToggle.IsChecked == true;

            if (displayPaused && previewIsPaused) {
                PreviewArtistText.Text = "(Paused) • Artist";
                PreviewProgressBar.Visibility = Visibility.Collapsed;
                PreviewPausedInfo.Visibility = Visibility.Visible;
            }
            else {
                PreviewArtistText.Text = "Artist";
                PreviewProgressBar.Visibility = Visibility.Visible;
                PreviewPausedInfo.Visibility = Visibility.Collapsed;
            }
            PreviewPlayPauseIcon.Source = previewIsPaused ? playIcon : pauseIcon;
            PreviewPlayPauseButton.Visibility = displayPaused ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RpcToggle_Click(object sender, RoutedEventArgs e) {
            UpdateRpcPreview();
        }

        private void PreviewPlayPause_Click(object sender, RoutedEventArgs e) {
            previewIsPaused = !previewIsPaused;
            UpdateRpcPausedState();
        }
    }
}
