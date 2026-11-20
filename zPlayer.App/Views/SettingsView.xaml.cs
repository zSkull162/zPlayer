using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using DiscordRPC;
using ColorPicker;
using zPlayer.Core;
using zPlayer.App.Themes;

namespace zPlayer.App.Views
{
    public partial class SettingsView : UserControl
    {
        private readonly BitmapImage playIcon = new(new Uri("pack://application:,,,/Assets/Icons/Play_32x.png"));
        private readonly BitmapImage pauseIcon = new(new Uri("pack://application:,,,/Assets/Icons/Pause_32x.png"));
        private ThemeSettings theme = new();

        public event Action<zPlayerRpcSettings, AppSettings>? SettingsChanged;
        public event Action<ThemeSettings>? ThemeSaveRequested;
        private bool loadingSettings;
        private bool previewIsPaused;

        public SettingsView(zPlayerRpcSettings rpcSettings, AppSettings appSettings) {
            InitializeComponent();
            LoadSettings(rpcSettings, appSettings);
            previewIsPaused = false;
            PreviewPlayPauseIcon.Source = pauseIcon;
            UpdateRpcPreview();

            Unloaded += (_, _) => ThemeSaveRequested?.Invoke(theme);
        }

        public void LoadSettings(zPlayerRpcSettings rpcSettings, AppSettings appSettings) {
            loadingSettings = true;
            try {
                RpcToggle.IsChecked = rpcSettings.Enabled;
                RpcDisplayPausedToggle.IsChecked = rpcSettings.DisplayPausedState;
                RpcDisplayAlbumToggle.IsChecked = rpcSettings.DisplayAlbumName;
                RpcStatusBox.SelectedIndex = rpcSettings.StatusType;

                ImportSummaryToggle.IsChecked = appSettings.ShowImportSummary;
                SongDeleteConfirmationToggle.IsChecked = appSettings.ShowSongDeleteConfirmation;
                AlbumDeleteConfirmationToggle.IsChecked = appSettings.ShowAlbumDeleteConfirmation;
                MixDeleteConfirmationToggle.IsChecked= appSettings.ShowMixDeleteConfirmation;
                DuplicateSongToggle.IsChecked = appSettings.AllowDuplicatePlaybackSongs;

                theme = appSettings.Theme ?? new ThemeSettings();
                ThemeManager.Apply(theme);

                BackgroundPicker.SelectedColor = (Color)Application.Current.FindResource("BackgroundColor");
                SurfacePicker.SelectedColor = (Color)Application.Current.FindResource("SurfaceColor");
                BorderPicker.SelectedColor = (Color)Application.Current.FindResource("BorderColor");
                HighlightPicker.SelectedColor = (Color)Application.Current.FindResource("HighlightColor");
                SelectedPicker.SelectedColor = (Color)Application.Current.FindResource("SelectedColor");
                SelectedPickerSecondary.SelectedColor = (Color)Application.Current.FindResource("SelectedColorSecondary");
                TextPicker.SelectedColor = (Color)Application.Current.FindResource("TextColor");
            }
            finally {
                loadingSettings = false;
            }
            UpdateRpcPreview();
        }

        private void SaveSettings() {
            if (loadingSettings || RpcStatusBox.SelectedIndex < 0) return;
            UpdateRpcPreview();
            SettingsChanged?.Invoke(
                new zPlayerRpcSettings(
                    Enabled: RpcToggle.IsChecked == true,
                    DisplayPausedState: RpcDisplayPausedToggle.IsChecked == true,
                    DisplayAlbumName: RpcDisplayAlbumToggle.IsChecked == true,
                    StatusType: RpcStatusBox.SelectedIndex
                ),
                new AppSettings(
                    ShowImportSummary: ImportSummaryToggle.IsChecked == true,
                    ShowSongDeleteConfirmation: SongDeleteConfirmationToggle.IsChecked == true,
                    ShowAlbumDeleteConfirmation: AlbumDeleteConfirmationToggle.IsChecked == true,
                    ShowMixDeleteConfirmation: MixDeleteConfirmationToggle.IsChecked == true,
                    AllowDuplicatePlaybackSongs: DuplicateSongToggle.IsChecked == true,
                    Theme: theme
                )
            );
        }

        private void SettingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => SaveSettings();
        private void SettingToggle_Click(object sender, RoutedEventArgs e) => SaveSettings();

        #region Discord RPC
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
        }

        private void PreviewPlayPause_Click(object sender, RoutedEventArgs e) {
            previewIsPaused = !previewIsPaused;
            UpdateRpcPausedState();
        }
        #endregion

        private void ResetColor_Click(object sender, RoutedEventArgs e) {
            if (sender is not System.Windows.Controls.Button button) return;
            ThemeSettings defaultTheme = new();
            Color newColor;
            string colorKey;

            switch (button.Tag) {
                default: return;
                case "ResetBackground":
                    colorKey = "BackgroundColor";
                    newColor = ThemeManager.GetColor(defaultTheme.Background);
                    BackgroundPicker.SelectedColor = newColor;
                    break;
                case "ResetSurface":
                    colorKey = "SurfaceColor";
                    newColor = ThemeManager.GetColor(defaultTheme.Surface);
                    SurfacePicker.SelectedColor = newColor;
                    break;
                case "ResetBorder":
                    colorKey = "BorderColor";
                    newColor = ThemeManager.GetColor(defaultTheme.Border);
                    BorderPicker.SelectedColor = newColor;
                    break;
                case "ResetHighlight":
                    colorKey = "HighlightColor";
                    newColor = ThemeManager.GetColor(defaultTheme.Highlight);
                    BorderPicker.SelectedColor = newColor;
                    break;
                case "ResetSelected":
                    colorKey = "SelectedColor";
                    newColor = ThemeManager.GetColor(defaultTheme.Selected);
                    SelectedPicker.SelectedColor = newColor;
                    break;
                case "ResetSelectedSecondary":
                    colorKey = "SelectedColorSecondary";
                    newColor = ThemeManager.GetColor(defaultTheme.SelectedSecondary);
                    SelectedPickerSecondary.SelectedColor = newColor;
                    break;
                case "ResetText":
                    colorKey = "TextColor";
                    newColor = ThemeManager.GetColor(defaultTheme.Text);
                    TextPicker.SelectedColor = newColor;
                    break;
            }
            ThemeManager.SetColor(colorKey, newColor);
            theme = ThemeManager.Capture();
            SaveSettings();
        }

        private void ThemeColor_Changed(object sender, RoutedEventArgs e) {
            if (loadingSettings || sender is not PortableColorPicker picker || picker.Tag is not string key) return;

            ThemeManager.SetColor(key, picker.SelectedColor);
            theme = ThemeManager.Capture();
            SaveSettings();
        }
    }
}
