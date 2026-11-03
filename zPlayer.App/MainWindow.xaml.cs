using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using zPlayer.App.Converters;
using zPlayer.App.Playback;
using zPlayer.App.Views;
using zPlayer.Core;
using zPlayer.App.Commands;
using zPlayer.Infrastructure;
using zPlayer.Infrastructure.Storage;

namespace zPlayer.App
{
    public partial class MainWindow : Window
    {
        private readonly AudiofileMetadataReader metadataReader = new();
        private readonly MusicLibraryDatabase database = new();
        private readonly PlaybackController player = new();
        private readonly ArtworkConverter converter = new();
        private readonly MusicLibrary library = new();
        private readonly MediaKeyListener? mediaKeys;
        private readonly SongCommands songCommands;

        private readonly DispatcherTimer volumeCloseTimer = new() {
            Interval = TimeSpan.FromMilliseconds(125)
        };
        private readonly DispatcherTimer playbackTimer = new() {
            Interval = TimeSpan.FromMilliseconds(50)
        };

        private readonly BitmapImage playIcon = new(new Uri("pack://application:,,,/Assets/Icons/Play_32x.png"));
        private readonly BitmapImage pauseIcon = new(new Uri("pack://application:,,,/Assets/Icons/Pause_32x.png"));

        private bool updatingQueueSelection;
        private bool updatingTimeline;
        private bool queueOpen;
        private const double QueueCollapsedWidth = 0;
        private const double QueueExpandedWidth = 320;

        public MainWindow() {
            songCommands = new(player, ShowEditSong, DeleteSong, ShowMoveToAlbum);

            InitializeComponent();
            InitDatabase();
            mediaKeys = new();
            InitMediaKeys();
            InitPlaybackUI();
            InitVolumePopupTimer();
            InitVolumeSlider();

            UpdatePlaybackUI();
            Loaded += (_, _) => {
                LibraryNavigation.SelectedIndex = 0;
                NavigateToPage("Songs");
            };
        }

        #region Initalization methods
        private void InitDatabase() {
            database.Initalize();

            foreach (Album album in database.LoadAlbums()) {
                library.AddAlbum(album);
            }
            library.AddSongs(database.LoadSongs());
        }

        private void InitMediaKeys() {
            if (mediaKeys == null) return;
            try {
                mediaKeys.PlayPausePressed += player.TogglePlayPause;
                mediaKeys.NextPressed += player.Next;
                mediaKeys.PreviousPressed += player.Previous;
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to register media keys: {ex.Message}");
            }
        }

        private void InitPlaybackUI() {
            TimelineSlider.ValueChanged += TimelineSlider_ValueChanged;

            playbackTimer.Tick += (_, _) => UpdateTimeline();
            playbackTimer.Start();

            player.StateChanged += (_, _) => UpdatePlaybackUI();
            player.PlaybackError += message => {
                MessageBox.Show(this, message, "Playback Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };
        }

        private void InitVolumePopupTimer() {
            volumeCloseTimer.Tick += (_, _) => {
                volumeCloseTimer.Stop();
                if (!VolumeButton.IsMouseOver && !VolumePopup.IsMouseOver && !VolumeSlider.IsMouseCaptureWithin) {
                    HideVolumePopup();
                }
            };
        }

        private void InitVolumeSlider() {
            double savedVolume = database.LoadVolume();
            player.Volume = savedVolume;
            VolumeSlider.Value = savedVolume;

            VolumeSlider.ValueChanged += VolumeSlider_ValueChanged;
            VolumeSlider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(VolumeSlider_DragCompleted), handledEventsToo: true);
        }
        #endregion

        protected override void OnClosed(EventArgs e) {
            playbackTimer.Stop();
            volumeCloseTimer.Stop();
            SaveVolumeSetting();
            mediaKeys?.Dispose();
            player.Dispose();
            base.OnClosed(e);
        }

        #region Playback UI
        private void UpdatePlaybackUI() {
            Song? song = player.GetCurrentSong();

            PlaybackBarRow.Height = new GridLength(player.QueueCount > 0 ? 100 : 0);

            SongTitleText.Text = song?.Title ?? "Nothing playing";
            SongArtistText.Text = song?.Artist ?? "";

            CurrentSongArtwork.Source = converter.Convert(song?.Artwork!, typeof(ImageSource), null!, CultureInfo.CurrentCulture) as ImageSource;

            updatingQueueSelection = true;
            try {
                QueueList.ItemsSource = player.GetQueueSongs();
                QueueList.SelectedIndex = player.CurrentQueueIndex;
            }
            finally {
                updatingQueueSelection = false;
            }
            if (QueueList.SelectedItem != null) QueueList.ScrollIntoView(QueueList.SelectedItem);

            LoopButtonImage.DataContext = new {
                LoopMode = player.Loop
            };
            ShuffleButton.IsEnabled = player.QueueCount > 1;
            ShuffleButton.Opacity = ShuffleButton.IsEnabled ? 1 : 0.25;

            PlayPauseImage.Source = player.IsPlaying ? pauseIcon : playIcon;
            UpdateTimeline();
        }

        private static string FormatPlaybackTime(TimeSpan time) {
            if (time < TimeSpan.Zero)
                time = TimeSpan.Zero;

            return time.TotalHours >= 1
                ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
                : $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";
        }

        private void UpdateTimeline() {
            if (TimelineSlider.IsMouseCaptureWithin) return;

            TimeSpan duration = player.Duration;
            TimeSpan position = player.Position;
            double durationSeconds = Math.Max(0, duration.TotalSeconds);
            double positionSeconds = Math.Clamp(position.TotalSeconds, 0, durationSeconds);

            updatingTimeline = true;
            try {
                TimelineSlider.IsEnabled = durationSeconds > 0;
                TimelineSlider.Maximum = durationSeconds > 0 ? durationSeconds : 1;
                TimelineSlider.Value = positionSeconds;

                CurTime.Text = FormatPlaybackTime(TimeSpan.FromSeconds(positionSeconds));
                TotalTime.Text = FormatPlaybackTime(duration);
            }
            finally {
                updatingTimeline = false;
            }
        }

        private void TimelineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (updatingTimeline || !TimelineSlider.IsEnabled) return;

            TimeSpan position = TimeSpan.FromSeconds(e.NewValue);
            CurTime.Text = FormatPlaybackTime(position);
            player.Seek(position);
        }
        #endregion

        #region Volume slider
        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            player.Volume = e.NewValue;
        }

        private void VolumeSlider_DragCompleted(object sender, DragCompletedEventArgs e) {
            SaveVolumeSetting();
        }

        private void SaveVolumeSetting() {
            try {
                database.SaveVolume(player.Volume);
            }
            catch (Exception ex) {
                MessageBox.Show(this, $"Could not save the volume setting:\n{ex.Message}", "Settings Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Volume Popup
        private void ShowVolumePopup() {
            volumeCloseTimer.Stop();
            VolumePopup.IsOpen = true;

            VolumeSlider.BeginAnimation(
                UIElement.OpacityProperty,
                new DoubleAnimation {
                    To = 1, Duration = TimeSpan.FromMilliseconds(150)
                }
            );
        }

        private void HideVolumePopup() {
            if (!VolumePopup.IsOpen) return;
            DoubleAnimation fadeOut = new() {
                To = 0, Duration = TimeSpan.FromMilliseconds(150)
            };
            fadeOut.Completed += (_, _) => {
                if (!VolumeButton.IsMouseOver && !VolumePopup.IsMouseOver && !VolumeSlider.IsMouseCaptureWithin) {
                    VolumePopup.IsOpen = false;
                }
            };
            VolumeSlider.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private void Volume_MouseEnter(object sender, RoutedEventArgs e) {
            ShowVolumePopup();
        }

        private void Volume_MouseLeave(object sender, RoutedEventArgs e) {
            volumeCloseTimer.Stop();
            volumeCloseTimer.Start();
        }
        #endregion

        private void NavigateToPage(string tag) {
            switch (tag) {
                default:
                    SongsView songView = new SongsView(library, songCommands);
                    songView.ImportSongsRequested += ImportSongs;
                    MainContent.Content = songView;
                    break;
                case "Albums":
                    AlbumsView albumView = new(library);
                    albumView.AlbumOpened += OpenAlbumDetails;
                    albumView.CreateAlbumRequested += ShowCreateAlbum;
                    albumView.EditAlbumRequested += ShowEditAlbum;
                    MainContent.Content = albumView;
                    break;
            }
        }

        private void ImportSongs() {
            OpenFileDialog dialog = new() {
                Title = "Import Songs",
                Filter = "Audio Files|*.mp3;*.flac;*.ogg;*.wav",
                Multiselect = true,
                CheckFileExists = true
            };
            if (dialog.ShowDialog(this) != true) return;

            int imported = 0;
            int skipped = 0;
            List<string> errors = [];
            HashSet<string> existingPaths = new(library.Songs.Select(song => Path.GetFullPath(song.FilePath)), StringComparer.OrdinalIgnoreCase);

            foreach (string selectedPath in dialog.FileNames) {
                try {
                    string filePath = Path.GetFullPath(selectedPath);
                    if (existingPaths.Contains(filePath)) {
                        skipped++;
                        continue;
                    }

                    Song song = metadataReader.Read(filePath);
                    Album? album = library.Albums.FirstOrDefault(existing =>
                            string.Equals(existing.Title, song.AlbumTitle, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(existing.Artist, song.AlbumArtist, StringComparison.OrdinalIgnoreCase));

                    if (album == null) {
                        album = new Album {
                            Title = song.AlbumTitle,
                            Artist = song.AlbumArtist
                        };
                        database.SaveAlbum(album);
                        library.AddAlbum(album);
                    }

                    song.AlbumId = album.Id;
                    song.AlbumPosIndex = album.Songs.Select(existing => existing.AlbumPosIndex).DefaultIfEmpty(-1).Max() + 1;
                    database.SaveSong(song);
                    library.AddSong(song);
                    existingPaths.Add(filePath);
                    imported++;
                }
                catch (Exception ex) {
                    errors.Add($"{Path.GetFileName(selectedPath)}: {ex.Message}");
                }
            }

            CollectionViewSource.GetDefaultView(library.Songs).Refresh();
            if (MainContent.Content is AlbumsView albumsView) albumsView.RefreshAlbums();
            else if (MainContent.Content is AlbumDetailsView detailsView) detailsView.RefreshView();

            if (errors.Count > 0) {
                string summary = $"Imported: {imported}\nAlready in library: {skipped}\nFailed: {errors.Count}\n\n" + string.Join("\n", errors.Take(10));
                if (errors.Count > 10) summary += "\nAdditional errors omitted.";
                MessageBox.Show(this, summary, "Import Songs", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #region Popup menus
        private void ShowEditSong(Song song) {
            EditSongView view = new(song, library);

            view.CloseRequested += ClosePopup;
            view.SaveRequested += edited => {
                try {
                    if (edited.AlbumId != song.AlbumId) {
                        Album? destination = edited.AlbumId.HasValue ? library.GetAlbum(edited.AlbumId.Value) : null;

                        edited.AlbumPosIndex = destination == null ? 0 : destination.Songs.Select(existing => existing.AlbumPosIndex)
                                .DefaultIfEmpty(-1).Max() + 1;
                    }
                    database.SaveSong(edited);
                }
                catch (Exception ex) {
                    MessageBox.Show(this, $"Could not save the song:\n{ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                foreach (Album album in library.Albums) {
                    album.Songs.RemoveAll(existing => existing.Id == song.Id);
                }
                if (edited.AlbumId.HasValue) {
                    Album? targetAlbum = library.GetAlbum(edited.AlbumId.Value);
                    targetAlbum?.Songs.Add(song);
                }
                song.ApplyEdits(edited);
                RefreshAfterSongEdit();
                ClosePopup();
            };
            OpenPopup(view);
        }

        private void ShowEditAlbum(Album album) {
            EditAlbumView view = new(album);

            view.CloseRequested += ClosePopup;
            view.SaveRequested += edited => {
                try {
                    database.SaveAlbum(edited);
                }
                catch (Exception ex) {
                    MessageBox.Show(this, $"Could not save the album:\n{ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                album.Title = edited.Title;
                album.Artist = edited.Artist;
                album.CustomArtwork = edited.CustomArtwork?.ToArray();
                album.ArtworkSongId = edited.ArtworkSongId;

                foreach (Song song in library.Songs.Where(song => song.AlbumId == album.Id)) {
                    song.AlbumTitle = album.Title;
                    song.AlbumArtist = album.Artist;
                }
                CollectionViewSource.GetDefaultView(library.Songs).Refresh();

                if (MainContent.Content is AlbumsView albumsView) albumsView.RefreshAlbums();
                else if (MainContent.Content is AlbumDetailsView detailsView) detailsView.RefreshView();

                UpdatePlaybackUI();
                ClosePopup();
            };
            OpenPopup(view);
        }

        private void ShowCreateAlbum() {
            CreateAlbumView view = new();
            view.CloseRequested += ClosePopup;
            view.CreateRequested += album => {
                try {
                    database.SaveAlbum(album);
                }
                catch (Exception ex) {
                    MessageBox.Show(this, $"Could not create the album:\n{ex.Message}", "Create Album Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                library.AddAlbum(album);
                if (MainContent.Content is AlbumsView albumsView) albumsView.RefreshAlbums();
                ClosePopup();
            };
            OpenPopup(view);
        }

        private void ShowMoveToAlbum(Song song) {
            MoveToAlbumView view = new(library, song);

            view.CloseRequested += ClosePopup;
            view.MoveRequested += selectedAlbum => {
                Album? destination = library.GetAlbum(selectedAlbum.Id);
                if (destination == null || !library.Songs.Any(existing => existing.Id == song.Id)) {
                    MessageBox.Show(this, "The song or destination album is no longer in the library.", "Move Song", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (destination.Id == song.AlbumId) return;

                Song updated = song.Clone();
                updated.AlbumId = destination.Id;
                updated.AlbumTitle = destination.Title;
                updated.AlbumArtist = destination.Artist;
                updated.AlbumPosIndex = destination.Songs.Select(existing => existing.AlbumPosIndex).DefaultIfEmpty(-1).Max() + 1;

                try {
                    database.SaveSong(updated);
                }
                catch (Exception ex) {
                    MessageBox.Show(this, $"Could not move the song:\n{ex.Message}", "Move Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                foreach (Album album in library.Albums) {
                    album.Songs.RemoveAll(existing => existing.Id == song.Id);
                }
                song.ApplyEdits(updated);
                destination.Songs.Add(song);

                RefreshAfterSongEdit();
                ClosePopup();
            };
            OpenPopup(view);
        }
        #endregion

        private void OpenAlbumDetails(Album album) {
            AlbumDetailsView detailsView = new(album, songCommands);

            detailsView.BackRequested += () => NavigateToPage("Albums");
            detailsView.PlayRequested += songs => player.PlayQueue(songs);
            detailsView.AddToQueueRequested += player.AddToQueue;
            detailsView.InsertNextRequested += player.InsertNext;
            detailsView.EditAlbumRequested += ShowEditAlbum;
            detailsView.EditSongRequested += ShowEditSong;
            detailsView.SongOrderRequested += SaveAlbumSongOrder;

            MainContent.Content = detailsView;
        }

        private void DeleteSong(Song song) {
            if (MessageBox.Show(this, $"Remove \"{song.Title}\" from your library?", "Delete Song", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            try {
                database.DeleteSong(song.Id);
            }
            catch (Exception ex) {
                MessageBox.Show(this, $"Could not delete the song:\n{ex.Message}", "Delete Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            library.RemoveSong(song.Id);

            int queueIndex = player.GetQueueSongs().FindIndex(existing => existing.Id == song.Id);
            if (queueIndex >= 0) player.RemoveQueueIndex(queueIndex);

            RefreshAfterSongEdit();
        }

        private void SaveAlbumSongOrder(Album album, IReadOnlyList<Song> reordered) {
            try {
                database.SaveAlbumSongOrder(album.Id, reordered.Select(song => song.Id).ToArray());
            }
            catch (Exception ex) {
                MessageBox.Show(this, $"Could not save the song order:\n{ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            album.Songs.Clear();
            album.Songs.AddRange(reordered);

            for (int i = 0; i < album.Songs.Count; i++) {
                album.Songs[i].AlbumPosIndex = i;
            }
            if (MainContent.Content is AlbumDetailsView detailsView) detailsView.RefreshView();
        }

        private void RefreshAfterSongEdit() {
            if (MainContent.Content is AlbumDetailsView details) {
                details.RefreshView();
            }
            else if (MainContent.Content is AlbumsView) {
                NavigateToPage("Albums");
            }
            CollectionViewSource.GetDefaultView(library.Songs).Refresh();
            UpdatePlaybackUI();
        }

        #region Popup methods
        private void OpenPopup(UserControl content) {
            PopupContent.Content = content;

            PopupOverlay.BeginAnimation(UIElement.OpacityProperty, null);
            PopupOverlay.Opacity = 0;
            PopupOverlay.Visibility = Visibility.Visible;

            DoubleAnimation fadeIn = new(0, 1, TimeSpan.FromSeconds(0.15)) {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            PopupOverlay.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        private void ClosePopup() {
            DoubleAnimation fadeOut = new(0, TimeSpan.FromSeconds(0.15)) {
                FillBehavior = FillBehavior.Stop
            };
            fadeOut.Completed += (_, _) => {
                PopupOverlay.Visibility = Visibility.Collapsed;
                PopupOverlay.Opacity = 0;
                PopupContent.Content = null;
            };
            PopupOverlay.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private void PopupOverlay_Click(object sender, RoutedEventArgs e) {
            ClosePopup();
        }
        #endregion

        private void QueueSong_Click(object sender, RoutedEventArgs e) {
            if (sender is not Button { DataContext: Song song }) return;

            int index = player.GetQueueSongs().FindIndex(existing => existing.Id == song.Id);
            if (index >= 0) player.PlayQueueIndex(index);

            e.Handled = true;
        }

        private void QueueList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (!IsInitialized || updatingQueueSelection) return;
            updatingQueueSelection = true;
            try {
                QueueList.SelectedIndex = player.CurrentQueueIndex;
            }
            finally {
                updatingQueueSelection = false;
            }
        }

        private void RemoveQueueSong_Click(object sender, RoutedEventArgs e) {
            if (sender is not Button { DataContext: Song song }) return;

            int index = player.GetQueueSongs().FindIndex(existing => existing.Id == song.Id);
            if (index >= 0) player.RemoveQueueIndex(index);

            e.Handled = true;
        }

        private void LibraryNavigation_SelectionChanged(object sender, RoutedEventArgs e) {
            if (!IsLoaded || LibraryNavigation.SelectedItem is not ListBoxItem item) return;
            AppNavigation.SelectedIndex = -1;
            NavigateToPage(item.Tag.ToString()!);
        }
    
        private void AppNavigation_SelectionChanged(object sender, RoutedEventArgs e) {
            if (!IsLoaded || AppNavigation.SelectedItem is not ListBoxItem item) return;
            LibraryNavigation.SelectedIndex = -1;
            NavigateToPage(item.Tag.ToString()!);
        }

        #region Button click events
        private void ToggleQueue_Click(object sender, RoutedEventArgs e) {
            queueOpen = !queueOpen;
            double targetWidth = queueOpen ? QueueExpandedWidth : QueueCollapsedWidth;
            QueueColumn.Width = new(targetWidth);
        }

        private void ClearQueue_Click(object sender, RoutedEventArgs e) {
            player.ClearQueue();
        }

        private void LoopButton_Click(object sender, RoutedEventArgs e) {
            player.CycleLoopMode();
        }

        private void ShuffleButton_Click(object sender, RoutedEventArgs e) {
            player.ShuffleQueue();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e) {
            player.Previous();
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e) {
            player.TogglePlayPause();
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e) {
            player.Next();
        }
        #endregion
    }
}