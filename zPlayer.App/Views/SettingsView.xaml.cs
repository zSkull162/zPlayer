using DiscordRPC;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using zPlayer.Core;

namespace zPlayer.App.Views
{
    public partial class SettingsView : UserControl
    {
        private readonly BitmapImage playIcon = new(new Uri("pack://application:,,,/Assets/Icons/Play_32x.png"));
        private readonly BitmapImage pauseIcon = new(new Uri("pack://application:,,,/Assets/Icons/Pause_32x.png"));

        public event Action<zPlayerRpcSettings>? SettingsChanged;
        private bool loadingSettings;
        private bool previewIsPaused;

        public SettingsView(zPlayerRpcSettings settings) {
            InitializeComponent();
            LoadSettings(settings);
            previewIsPaused = false;
            PreviewPlayPauseIcon.Source = pauseIcon;
            // PreviewPlayPauseButton.Visibility = Visibility.Collapsed;
            UpdateRpcPreview();
        }

        public void LoadSettings(zPlayerRpcSettings settings) {
            loadingSettings = true;
            try {
                RpcToggle.IsChecked = settings.Enabled;
                RpcDisplayPausedToggle.IsChecked = settings.DisplayPausedState;
                RpcDisplayAlbumToggle.IsChecked = settings.DisplayAlbumName;
                RpcStatusBox.SelectedIndex = settings.StatusType;
            }
            finally {
                loadingSettings = false;
            }
        }

        private void SaveSettings() {
            if (loadingSettings || RpcStatusBox.SelectedIndex < 0) return;
            UpdateRpcPreview();
            SettingsChanged?.Invoke(new zPlayerRpcSettings(
                    Enabled: RpcToggle.IsChecked == true,
                    DisplayPausedState: RpcDisplayPausedToggle.IsChecked == true,
                    DisplayAlbumName: RpcDisplayAlbumToggle.IsChecked == true,
                    StatusType: RpcStatusBox.SelectedIndex
                )
            );
        }

        private void UpdateRpcPreview() {
            if (PreviewUserArtistText == null) return;

            bool enabled = RpcToggle.IsChecked == true;
            bool displayAlbum = RpcDisplayAlbumToggle.IsChecked == true;

            RpcOptions.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            if (!enabled) return;
            UpdateRpcPausedState();

            PreviewUserArtistText.Text = RpcStatusBox.SelectedIndex switch {
                1 => "Song title",
                2 => "Song artist",
                _ => "zPlayer"
            };
        }

        private void UpdateRpcPausedState() {
            bool displayPaused = RpcDisplayPausedToggle.IsChecked == true;
            bool displayAlbum = RpcDisplayAlbumToggle.IsChecked == true;

            string albumText = "Album Name";
            if (!displayAlbum) albumText = string.Empty;
            if (previewIsPaused) {
                if (displayPaused && displayAlbum) albumText = $"{albumText}  •  (Paused)";
                else if (displayPaused && !displayAlbum) albumText = "(Paused)";
            }
            PreviewAlbumText.Text = albumText;
            PreviewAlbumText.Visibility = string.IsNullOrEmpty(albumText) ? Visibility.Collapsed : Visibility.Visible;

            PreviewProgressBar.Visibility = previewIsPaused ? Visibility.Collapsed : Visibility.Visible;
            PreviewPausedInfo.Visibility = previewIsPaused ? Visibility.Visible : Visibility.Collapsed;
            PreviewPlayPauseIcon.Source = previewIsPaused ? playIcon : pauseIcon;
            // PreviewPlayPauseButton.Visibility = displayPaused ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RpcStatusBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            SaveSettings();
        }

        private void RpcToggle_Click(object sender, RoutedEventArgs e) {
            SaveSettings();
        }

        private void PreviewPlayPause_Click(object sender, RoutedEventArgs e) {
            previewIsPaused = !previewIsPaused;
            UpdateRpcPausedState();
        }
    }
}
