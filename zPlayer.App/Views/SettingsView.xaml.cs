using System.Windows.Controls;
using System.Windows;

namespace zPlayer.App.Views
{
    public partial class SettingsView : UserControl
    {
        private bool discordRpcEnabled;
        private bool rpcDisplayPaused;
        private bool rpcDisplayAlbum;
        private int rpcStatusType = -1;

        public SettingsView() {
            InitializeComponent();
            rpcStatusType = StatusBox.SelectedIndex;
            if (rpcStatusType == -1) rpcStatusType = 2;
            UpdateRpcPreview();
        }

        private void StatusBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            rpcStatusType = StatusBox.SelectedIndex;
            if (rpcStatusType == -1) rpcStatusType = 2;
            UpdateRpcPreview();
        }

        private void UpdateRpcPreview() {
            RpcOptions.Visibility = discordRpcEnabled ? Visibility.Visible : Visibility.Collapsed;
            if (!discordRpcEnabled) return;

            PreviewAlbumText.Visibility = rpcDisplayAlbum ? Visibility.Visible : Visibility.Collapsed;
            if (rpcDisplayPaused) {
                PreviewArtistText.Text = "(Paused) • Artist";
                PreviewProgressBar.Visibility = Visibility.Collapsed;
                PreviewPausedInfo.Visibility = Visibility.Visible;
            }
            else {
                PreviewArtistText.Text = "Artist";
                PreviewProgressBar.Visibility = Visibility.Visible;
                PreviewPausedInfo.Visibility = Visibility.Collapsed;
            }

            PreviewUserArtistText.Text = rpcStatusType switch {
                1 => "Song title",
                2 => "Song artist",
                _ => "zPlayer"
            };
        }
    }
}
