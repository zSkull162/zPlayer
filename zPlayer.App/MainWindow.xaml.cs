#region Namespaces
using Microsoft.Win32;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using zPlayer.App.Commands;
using zPlayer.App.Artwork;
using zPlayer.App.Playback;
using zPlayer.App.Views;
using zPlayer.Core;
using zPlayer.Infrastructure;
using zPlayer.Infrastructure.Storage;
#endregion

namespace zPlayer.App
{
    public partial class MainWindow : Window
    {
        #region Fields
        private readonly AudiofileMetadataReader metadataReader = new();
        private readonly MusicLibraryDatabase database = new();
        private readonly PlaybackController player = new();
        private readonly MusicLibrary library = new();
        private readonly MediaKeyListener? mediaKeys;
        private readonly zPlayerRPC discordRPC;

        private readonly SongCommands songCommands;
        private readonly AlbumCommands albumCommands;
        private readonly MixCommands mixCommands;
        private readonly ViewState viewState = new();

        private readonly DispatcherTimer volumeCloseTimer = new() {
            Interval = TimeSpan.FromMilliseconds(125)
        };
        private bool renderingTimeline;

        private readonly BitmapImage playIcon = new(new Uri("pack://application:,,,/Assets/Icons/Play_32x.png"));
        private readonly BitmapImage pauseIcon = new(new Uri("pack://application:,,,/Assets/Icons/Pause_32x.png"));

        private bool updatingQueueSelection;
        private bool updatingTimeline;
        private bool queueOpen;
        private const double QueueCollapsedWidth = 0;
        private const double QueueExpandedWidth = 320;
        #endregion

        public MainWindow() {
            songCommands = new(player, ShowEditSongs, DeleteSongs, ShowMoveSongsToAlbum, ShowAddSongsToMix);
            albumCommands = new(player, ShowEditAlbums, DeleteAlbums, ShowAddSongsToMix);
            mixCommands = new(library, player, ShowEditMix, DeleteMixes);

            discordRPC = new("1571592760929161338");
            CoverArt.Loader = new ArtworkLoader(database);
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

            foreach (Mix mix in database.LoadMixes()) {
                library.AddMix(mix);
            }
            library.RefreshMixes();
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

            player.StateChanged += (_, _) => {
                UpdatePlaybackUI();
                discordRPC.Update(player.GetCurrentSong(), player.IsPlaying, player.Position, player.Duration);
            };
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

        #region Window titlebar color
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        protected override void OnSourceInitialized(EventArgs e) {
            base.OnSourceInitialized(e);

            static int ToColorRef(Color color) {
                return color.R | (color.G << 8) | (color.B << 16);
            }

            IntPtr handle = new WindowInteropHelper(this).Handle;
            int background = ToColorRef(Color.FromRgb(43, 44, 59));
            int foreground = ToColorRef(Color.FromRgb(240, 240, 240));

            DwmSetWindowAttribute(handle, 35, ref background, sizeof(int));
            DwmSetWindowAttribute(handle, 36, ref foreground, sizeof(int));
        }
        #endregion

        protected override void OnClosed(EventArgs e) {
            SetTimelineRendering(false);
            volumeCloseTimer.Stop();
            SaveVolumeSetting();
            mediaKeys?.Dispose();
            player.Dispose();
            discordRPC.Dispose();
            base.OnClosed(e);
        }

        #region Playback UI
        private void UpdatePlaybackUI() {
            Song? song = player.GetCurrentSong();

            PlaybackBarRow.Height = new GridLength(player.QueueCount > 0 ? 100 : 0);

            SongTitleText.Text = song?.Title ?? "Nothing playing";
            SongArtistText.Text = song?.Artist ?? "";

            CoverArt.SetReference(CurrentSongArtwork, song?.Cover);

            List<Song> queueSongs = player.GetQueueSongs();
            bool queueChanged = !QueueList.Items.Cast<Song>().SequenceEqual(queueSongs);
            bool currentChanged = QueueList.SelectedIndex != player.CurrentQueueIndex;
            updatingQueueSelection = true;
            try {
                if (queueChanged) QueueList.ItemsSource = queueSongs;
                QueueList.SelectedIndex = player.CurrentQueueIndex;
            }
            finally {
                updatingQueueSelection = false;
            }

            if ((queueChanged || currentChanged) && QueueList.SelectedItem != null) {
                QueueList.ScrollIntoView(QueueList.SelectedItem);
            }

            LoopButtonImage.DataContext = new {
                LoopMode = player.Loop
            };
            ShuffleQueueButton.IsEnabled = player.QueueCount > 1;
            ShuffleQueueButton.Opacity = ShuffleQueueButton.IsEnabled ? 1 : 0.25;
            ToggleShuffleButton.Opacity = player.AutoReshuffle ? 1 : 0.7;

            PlayPauseImage.Source = player.IsPlaying ? pauseIcon : playIcon;
            SetTimelineRendering(player.IsPlaying);
            UpdateTimeline();
        }

        private static string FormatPlaybackTime(TimeSpan time) {
            if (time < TimeSpan.Zero)
                time = TimeSpan.Zero;

            return time.TotalHours >= 1
                ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
                : $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";
        }

        private void SetTimelineRendering(bool enabled) {
            if (renderingTimeline == enabled) return;
            renderingTimeline = enabled;

            if (enabled) CompositionTarget.Rendering += Timeline_Rendering;
            else CompositionTarget.Rendering -= Timeline_Rendering;
        }

        private void Timeline_Rendering(object? sender, EventArgs e) {
            UpdateTimeline();
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
                    SongsView songView = new SongsView(library, songCommands, viewState.SongsSort);
                    songView.ImportSongsRequested += ImportSongs;
                    songView.ImportFolderRequested += ImportFolder;
                    MainContent.Content = songView;
                    break;
                case "Albums":
                    AlbumsView albumView = new(library, albumCommands, viewState.AlbumsSort);
                    albumView.AlbumOpened += OpenAlbumDetails;
                    albumView.CreateAlbumRequested += ShowCreateAlbum;
                    MainContent.Content = albumView;
                    break;
                case "Mixes":
                    MixesView mixesView = new(library, mixCommands, viewState.MixesSort);
                    mixesView.MixOpened += OpenMixDetails;
                    mixesView.CreateMixRequested += ShowCreateMix;
                    MainContent.Content = mixesView;
                    break;
                case "Settings":
                    MainContent.Content = new SettingsView();
                    break;
            }
        }

        #region Song importing
        private bool importingSongs;

        private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase) {
            ".mp3", ".flac", ".ogg", ".wav"
        };

        private async void ImportSongs() => await SelectAndImportAsync(fromFolder: false);

        private async void ImportFolder() => await SelectAndImportAsync(fromFolder: true);

        private async Task SelectAndImportAsync(bool fromFolder) {
            if (importingSongs) return;
            importingSongs = true;
            try {
                IReadOnlyList<string> paths;
                List<string> scanErrors = [];

                if (fromFolder) {
                    OpenFolderDialog dialog = new() { Title = "Import Music Folder" };
                    if (dialog.ShowDialog(this) != true) return;

                    string folder = dialog.FolderName;
                    var scan = await Task.Run(() => FindAudioFiles(folder));
                    if (!IsLoaded) return;
                    paths = scan.Paths;
                    scanErrors = scan.Errors;
                }
                else {
                    OpenFileDialog dialog = new() {
                        Title = "Import Songs",
                        Filter = "Audio Files|" + string.Join(";", AudioExtensions.Select(extension => "*" + extension)),
                        Multiselect = true,
                        CheckFileExists = true
                    };
                    if (dialog.ShowDialog(this) != true) return;
                    paths = dialog.FileNames;
                }
                int imported = 0;
                int skipped = 0;
                List<string> errors = [];
                HashSet<string> existingPaths = new(library.Songs.Select(song => Path.GetFullPath(song.FilePath)), StringComparer.OrdinalIgnoreCase);

                foreach (string selectedPath in paths) {
                    try {
                        string filePath = Path.GetFullPath(selectedPath);
                        if (existingPaths.Contains(filePath)) {
                            skipped++;
                            continue;
                        }
                        Song song = await Task.Run(() => metadataReader.Read(filePath));
                        if (!IsLoaded) return;
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
                        database.SaveSong(song, writeArtwork: true);
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
                else RefreshMixViews();
                
                if (fromFolder || errors.Count > 0) {
                    string summary = $"Imported: {imported}\nAlready in library: {skipped}\nFailed files: {errors.Count}";
                    if (fromFolder) summary += $"\nFolders that could not be read: {scanErrors.Count}";
                    if (fromFolder && paths.Count == 0 && scanErrors.Count == 0) {
                        summary += "\n\nNo supported audio files were found.";
                    }

                    string[] issues = scanErrors.Concat(errors).Take(10).ToArray();
                    if (issues.Length > 0) summary += "\n\n" + string.Join("\n", issues);
                    if (errors.Count + scanErrors.Count > issues.Length) summary += "\nAdditional errors omitted.";

                    MessageBoxImage icon = issues.Length > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information;
                    MessageBox.Show(this, summary, fromFolder ? "Import Folder" : "Import Songs", MessageBoxButton.OK, icon);
                }
            }
            finally {
                importingSongs = false;
            }
        }

        private static (List<string> Paths, List<string> Errors) FindAudioFiles(string root) {
            List<string> paths = [];
            List<string> errors = [];
            Stack<DirectoryInfo> pending = new();
            pending.Push(new DirectoryInfo(root));

            while (pending.TryPop(out DirectoryInfo? directory)) {
                try {
                    foreach (FileSystemInfo entry in directory.EnumerateFileSystemInfos()) {
                        if (entry is DirectoryInfo child) {
                            if ((child.Attributes & FileAttributes.ReparsePoint) == 0) pending.Push(child);
                        }
                        else if (entry is FileInfo file && AudioExtensions.Contains(file.Extension)) {
                            paths.Add(file.FullName);
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                    errors.Add($"{directory.FullName}: {ex.Message}");
                }
            }
            paths.Sort(StringComparer.OrdinalIgnoreCase);
            return (paths, errors);
        }
        #endregion

        #region Popup menus
        private async void ShowEditSong(Song song) {
            object currentView = MainContent.Content;
            byte[]? originalArtwork;
            try {
                originalArtwork = song.Cover is { } cover ? await Task.Run(() => database.LoadArtwork(cover)) : null;
                if (song.Cover.HasValue && originalArtwork == null) throw new InvalidOperationException("The original cover is unavailable.");
            }
            catch (Exception ex) {
                MessageBox.Show(this, ex.Message, "Artwork Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!ReferenceEquals(MainContent.Content, currentView) || PopupContent.Content != null) return;

            EditSongView view = new(song, library, originalArtwork, database);

            view.CloseRequested += ClosePopup;
            view.SaveRequested += edited => {
                try {
                    if (edited.AlbumId != song.AlbumId) {
                        Album? destination = edited.AlbumId.HasValue ? library.GetAlbum(edited.AlbumId.Value) : null;

                        edited.AlbumPosIndex = destination == null ? 0 : destination.Songs.Select(existing => existing.AlbumPosIndex)
                                .DefaultIfEmpty(-1).Max() + 1;
                    }
                    database.SaveSong(edited, writeArtwork: !ReferenceEquals(edited.Artwork, originalArtwork));
                }
                catch (Exception ex) {
                    MessageBox.Show(this, $"Could not save the song:\n{ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                ApplySavedSongs([edited]);
                RefreshAfterSongEdit();
                ClosePopup();
            };
            OpenPopup(view);
        }

        private void ShowEditSongs(Song[] songs) {
            if (songs.Length == 1) {
                ShowEditSong(songs[0]);
                return;
            }
            MultiEditView view = new(songs.Length, false);
            view.CloseRequested += ClosePopup;
            view.SaveRequested += changes => {
                try {
                    Song[] drafts = songs.Select(song => song.Clone()).ToArray();
                    foreach (Song draft in drafts) {
                        if (changes.Artist != null) draft.Artist = changes.Artist;
                    }

                    if (changes.ArtworkMode == MultiArtworkMode.SameAsAlbum) {
                        database.SaveSongs(drafts, writeArtwork: true, copyArtworkFrom: draft =>
                            draft.AlbumId is Guid albumId ? library.GetAlbum(albumId)?.Cover : null);
                    }
                    else {
                        database.SaveSongs(drafts, writeArtwork: changes.ArtworkMode != MultiArtworkMode.Keep,
                            readArtwork: draft => changes.ArtworkMode == MultiArtworkMode.Automatic
                                ? metadataReader.Read(draft.FilePath).Artwork
                                : changes.Artwork);
                    }
                    ApplySavedSongs(drafts);
                }
                catch (Exception ex) {
                    MessageBox.Show(this, $"Could not save the songs:\n{ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                RefreshAfterSongEdit();
                ClosePopup();
            };
            OpenPopup(view);
        }

        private async void ShowEditAlbum(Album album) {
            object currentView = MainContent.Content;
            byte[]? originalArtwork;
            try {
                originalArtwork = album.CustomCover is { } cover ? await Task.Run(() => database.LoadArtwork(cover)) : null;
                if (album.CustomCover.HasValue && originalArtwork == null) throw new InvalidOperationException("The original cover is unavailable.");
            }
            catch (Exception ex) {
                MessageBox.Show(this, ex.Message, "Artwork Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!ReferenceEquals(MainContent.Content, currentView) || PopupContent.Content != null) return;

            EditAlbumView view = new(album, originalArtwork);

            view.CloseRequested += ClosePopup;
            view.SaveRequested += edited => {
                try {
                    database.SaveAlbum(edited, writeArtwork: !ReferenceEquals(edited.CustomArtwork, originalArtwork));
                }
                catch (Exception ex) {
                    MessageBox.Show(this, $"Could not save the album:\n{ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                album.Title = edited.Title;
                album.Artist = edited.Artist;
                album.CustomCover = edited.CustomCover;
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

        private void ShowEditAlbums(Album[] albums) {
            if (albums.Length == 1) {
                ShowEditAlbum(albums[0]);
                return;
            }
            MultiEditView view = new(albums.Length, true);
            view.CloseRequested += ClosePopup;
            view.SaveRequested += changes => {
                Album[] drafts = albums.Select(album => album.Clone()).ToArray();

                foreach (Album draft in drafts) {
                    if (changes.Artist != null) draft.Artist = changes.Artist;
                    if (changes.ArtworkMode != MultiArtworkMode.Keep) {
                        draft.CustomArtwork = changes.ArtworkMode == MultiArtworkMode.Custom ? changes.Artwork : null;
                        draft.ArtworkSongId = null;
                    }
                }

                try {
                    database.SaveAlbums(drafts, writeArtwork: changes.ArtworkMode != MultiArtworkMode.Keep);
                }
                catch (Exception ex) {
                    MessageBox.Show(this, $"Could not save the albums:\n{ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                foreach (Album draft in drafts) {
                    Album original = albums.First(album => album.Id == draft.Id);
                    original.Artist = draft.Artist;
                    original.CustomCover = draft.CustomCover;
                    original.ArtworkSongId = draft.ArtworkSongId;

                    foreach (Song song in library.Songs.Where(song => song.AlbumId == original.Id)) {
                        song.AlbumTitle = original.Title;
                        song.AlbumArtist = original.Artist;
                    }
                }
                RefreshAfterSongEdit();
                ClosePopup();
            };
            OpenPopup(view);
        }

        private void ShowCreateAlbum() {
            CreateAlbumView view = new();
            view.CloseRequested += ClosePopup;
            view.CreateRequested += album => {
                try {
                    database.SaveAlbum(album, writeArtwork: true);
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

        private void ShowMoveSongsToAlbum(Song[] songs) {
            MoveToAlbumView view = new(library, songs);
            view.CloseRequested += ClosePopup;
            view.MoveRequested += selectedAlbum => {
                Album? destination = selectedAlbum == null ? null : library.GetAlbum(selectedAlbum.Id);

                if (selectedAlbum != null && destination == null) {
                    MessageBox.Show(this, "That album is no longer in the library.", "Move Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                Guid? destinationId = destination?.Id;
                int nextPosition = destination == null ? 0 : destination.Songs.Select(song => song.AlbumPosIndex).DefaultIfEmpty(-1).Max() + 1;

                Song[] drafts = songs.Where(song => song.AlbumId != destinationId).Select(song => song.Clone()).ToArray();
                foreach (Song draft in drafts) {
                    draft.AlbumId = destinationId;
                    draft.AlbumTitle = destination?.Title ?? "Unknown Album";
                    draft.AlbumArtist = destination?.Artist ?? "Unknown Artist";
                    draft.AlbumPosIndex = destination == null ? 0 : nextPosition++;
                }

                try {
                    database.SaveSongs(drafts);
                }
                catch (Exception ex) {
                    MessageBox.Show(this, ex.Message, "Move Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                ApplySavedSongs(drafts);
                RefreshAfterSongEdit();
                ClosePopup();
            };
            OpenPopup(view);
        }

        private void ShowCreateMix() {
            CreateMixView view = new();
            view.CloseRequested += ClosePopup;
            view.CreateRequested += mix => {
                try {
                    database.SaveMix(mix, writeArtwork: true);
                }
                catch (Exception ex) {
                    MessageBox.Show(this, ex.Message, "Create Mix Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                library.AddMix(mix);
                RefreshMixViews();
                ClosePopup();
            };
            OpenPopup(view);
        }

        private async void ShowEditMix(Mix mix) {
            object currentView = MainContent.Content;
            byte[]? originalArtwork;
            try {
                originalArtwork = mix.CustomCover is { } cover ? await Task.Run(() => database.LoadArtwork(cover)) : null;
                if (mix.CustomCover.HasValue && originalArtwork == null) throw new InvalidOperationException("The original cover is unavailable.");
            }
            catch (Exception ex) {
                MessageBox.Show(this, ex.Message, "Artwork Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!ReferenceEquals(MainContent.Content, currentView) || PopupContent.Content != null) return;

            EditMixView view = new(library, mix, originalArtwork);
            view.CloseRequested += ClosePopup;
            view.SaveRequested += draft => {
                try {
                    database.SaveMix(draft, writeArtwork: !ReferenceEquals(draft.CustomArtwork, originalArtwork));
                }
                catch (Exception ex) {
                    MessageBox.Show(this, ex.Message, "Save Mix Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                ApplyMixDraft(mix, draft);
                RefreshMixViews();
                ClosePopup();
            };
            OpenPopup(view);
        }

        private void ShowAddSongsToMix(Song[] songs) {
            Guid[] songIds = songs.Select(song => song.Id).Distinct().ToArray();
            if (songIds.Length == 0) return;
            if (library.Mixes.Count == 0) {
                MessageBox.Show(this, "Create a mix first.", "Add to Mix", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            AddToMixView view = new(library.GetMixes(), songIds);
            view.CloseRequested += ClosePopup;
            view.AddRequested += selectedMixes => {
                List<(Mix Original, Mix Draft)> changes = [];

                try {
                    HashSet<Guid> availableIds = library.Songs.Select(song => song.Id).ToHashSet();

                    if (songIds.Any(id => !availableIds.Contains(id))) {
                        throw new InvalidOperationException("One or more selected songs are no longer in the library.");
                    }
                    foreach (Mix selected in selectedMixes) {
                        Mix original = library.GetMix(selected.Id) ?? throw new InvalidOperationException("A selected mix is no longer in the library.");
                        Mix draft = original.Clone();
                        if (draft.AddSongs(songIds) > 0) {
                            changes.Add((original, draft));
                        }
                    }
                    if (changes.Count > 0) {
                        database.SaveMixes(changes.Select(change => change.Draft));
                    }
                }
                catch (Exception ex) {
                    MessageBox.Show(this, ex.Message, "Add to Mix Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                foreach (var change in changes) {
                    ApplyMixDraft(change.Original, change.Draft);
                }
                RefreshMixViews();
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
            detailsView.SongOrderRequested += SaveAlbumSongOrder;

            MainContent.Content = detailsView;
        }

        private void OpenMixDetails(Mix mix) {
            MixDetailsView view = new(library, mix, songCommands);

            view.BackRequested += () => NavigateToPage("Mixes");
            view.PlayRequested += songs => player.PlayQueue(songs);
            view.AddToQueueRequested += player.AddToQueue;
            view.InsertNextRequested += player.InsertNext;
            view.EditMixRequested += ShowEditMix;

            view.SortRequested += (type, descending) => {
                if (mix.SortType == type && mix.SortDescending == descending) return;
                try {
                    database.SaveMixSorting(mix.Id, type, descending);
                }
                catch (Exception ex) {
                    MessageBox.Show(this, ex.Message, "Sort Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                mix.SortType = type;
                mix.SortDescending = descending;
            };

            view.SongOrderRequested += (selected, reordered) => {
                Guid[] ids = reordered.Select(song => song.Id).ToArray();
                if (selected.SortDescending) Array.Reverse(ids);

                Mix draft = selected.Clone();
                draft.SetOrder(ids);
                try {
                    database.SaveMix(draft);
                }
                catch (Exception ex) {
                    MessageBox.Show(this, ex.Message, "Reorder Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                ApplyMixDraft(selected, draft);
                view.RefreshView();
            };

            view.RemoveSongsRequested += songs => {
                Mix draft = mix.Clone();
                draft.RemoveSongs(songs.Select(song => song.Id));

                try {
                    database.SaveMix(draft);
                }
                catch (Exception ex) {
                    MessageBox.Show(this, ex.Message, "Remove Songs Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                ApplyMixDraft(mix, draft);
                view.RefreshView();
            };
            MainContent.Content = view;
        }

        #region Delete/Save methods
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

        private void DeleteSongs(Song[] songs) {
            if (MessageBox.Show(this, $"Remove {songs.Length} selected song(s) from the library?", "Delete Songs", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            try {
                database.DeleteSongs(songs.Select(song => song.Id));
            }
            catch (Exception ex) {
                MessageBox.Show(this, ex.Message, "Delete Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            HashSet<Guid> ids = songs.Select(song => song.Id).ToHashSet();

            foreach (Song song in songs) {
                library.RemoveSong(song.Id);
            }
            List<Song> queue = player.GetQueueSongs();
            for (int i = queue.Count - 1; i >= 0; i--) {
                if (ids.Contains(queue[i].Id)) player.RemoveQueueIndex(i);
            }
            RefreshAfterSongEdit();
        }

        private void DeleteAlbums(Album[] albums) {
            if (MessageBox.Show(this, $"Remove {albums.Length} selected album(s)? Their songs will stay in the library.", "Delete Albums", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            try {
                database.DeleteAlbums(albums.Select(album => album.Id));
            }
            catch (Exception ex) {
                MessageBox.Show(this, ex.Message, "Delete Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            foreach (Album album in albums) {
                library.RemoveAlbum(album.Id);
            }
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

        private void ApplySavedSongs(IEnumerable<Song> drafts) {
            Dictionary<Guid, Song> originals = library.Songs.ToDictionary(song => song.Id);

            foreach (Song draft in drafts) {
                originals[draft.Id].ApplyEdits(draft);
            }

            foreach (Album album in library.Albums) {
                album.Songs.Clear();
                album.Songs.AddRange(library.Songs.Where(song => song.AlbumId == album.Id).OrderBy(song => song.AlbumPosIndex));
            }
        }

        private void DeleteMixes(Mix[] mixes) {
            if (MessageBox.Show(this, $"Delete {mixes.Length} selected mix(es)?", "Delete Mixes", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            try {
                database.DeleteMixes(mixes.Select(mix => mix.Id));
            }
            catch (Exception ex) {
                MessageBox.Show(this, ex.Message, "Delete Mix Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            foreach (Mix mix in mixes) {
                library.RemoveMix(mix.Id);
            }
            RefreshMixViews();
        }

        private void ApplyMixDraft(Mix original, Mix draft) {
            original.Title = draft.Title;
            original.CustomCover = draft.CustomCover;
            original.SortType = draft.SortType;
            original.SortDescending = draft.SortDescending;
            original.Entries = [.. draft.Entries];
            original.RefreshSongs(library.Songs);
        }
        #endregion

        private void RefreshAfterSongEdit() {
            if (MainContent.Content is AlbumDetailsView details) {
                details.RefreshView();
            }
            else if (MainContent.Content is AlbumsView albumsView) {
                albumsView.RefreshAlbums();
            }
            CollectionViewSource.GetDefaultView(library.Songs).Refresh();
            UpdatePlaybackUI();
            RefreshMixViews();
        }

        private void RefreshMixViews() {
            if (MainContent.Content is MixesView mixesView) mixesView.RefreshMixes();
            else if (MainContent.Content is MixDetailsView detailsView) detailsView.RefreshView();
            else library.RefreshMixes();
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

        private void QueueSelectSong_Click(object sender, RoutedEventArgs e) {
            if (sender is not MenuItem { DataContext: Song queuedSong }) return;

            Song? song = library.Songs.FirstOrDefault(item => item.Id == queuedSong.Id);
            if (song == null) return;

            SelectLibraryPage("Songs");
            if (MainContent.Content is SongsView view) {
                _ = view.Dispatcher.InvokeAsync(() => {
                    if (ReferenceEquals(MainContent.Content, view) && view.IsLoaded) {
                        view.RevealSong(song);
                    }
                }, DispatcherPriority.Loaded);
            }
        }

        private void QueueSelectAlbum_Click(object sender, RoutedEventArgs e) {
            if (sender is not MenuItem { DataContext: Song { AlbumId: Guid albumId } }) return;

            Album? album = library.GetAlbum(albumId);
            if (album == null) return;

            SelectLibraryPage("Albums");
            OpenAlbumDetails(album);
        }

        private void SelectLibraryPage(string tag) {
            ListBoxItem? item = LibraryNavigation.Items.OfType<ListBoxItem>().FirstOrDefault(entry => Equals(entry.Tag, tag));
            if (item == null) return;

            if (!ReferenceEquals(LibraryNavigation.SelectedItem, item)) {
                LibraryNavigation.SelectedItem = item;
            }
            else if (tag == "Songs" && MainContent.Content is not SongsView) {
                NavigateToPage(tag);
            }
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

        private void ShuffleQueue_Click(object sender, RoutedEventArgs e) {
            player.ShuffleQueue();
        }

        private void ToggleShuffle_Click(object sender, RoutedEventArgs e) {
            player.ToggleAutoReshuffle();
            ToggleShuffleButton.Opacity = player.AutoReshuffle ? 1 : 0.7;
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