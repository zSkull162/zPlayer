using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using zPlayer.Core;

namespace zPlayer.App.Views
{
    public partial class AlbumDetailsView : UserControl
    {
        private sealed class AlbumSongDragData
        {
            public Album Album { get; }
            public Song Song { get; }

            public AlbumSongDragData(Album album, Song song) {
                Album = album;
                Song = song;
            }
        }
        private Point dragStart;
        private Song? pendingDragSong;
        public event Action<Album, IReadOnlyList<Song>>? SongOrderRequested;

        private readonly MusicLibrary library;
        private readonly Album album;

        public event Action<Song[]>? PlayRequested;
        public event Action<Song[]>? AddToQueueRequested;
        public event Action<Song[]>? InsertNextRequested;
        public event Action? BackRequested;
        public event Action<Album>? EditAlbumRequested;
        public event Action<Song>? EditSongRequested;

        public AlbumDetailsView(MusicLibrary library, Album album) {
            InitializeComponent();
            this.library = library;
            this.album = album;
            DataContext = album;
            AlbumTitleText.Text = $"— {album.Title}";
            UpdateAlbumInfoText();
        }

        private Song[] GetPlaybackSongs() {
            return album.Songs.ToArray();
        }

        public void RefreshView() {
            DataContext = null;
            DataContext = album;
            AlbumSongsGrid.Items.Refresh();
            AlbumTitleText.Text = $"— {album.Title}";
            UpdateAlbumInfoText();
        }

        private void UpdateAlbumInfoText() {
            TimeSpan duration = album.Duration;
            string formatted = duration.TotalHours >= 1 ? duration.ToString(@"h\:mm\:ss") : duration.ToString(@"m\:ss");
            AlbumInfoText.Text = $"{album.SongCount} Songs — {formatted}";
        }

        private void BackButton_Click(object sender, RoutedEventArgs e) {
            BackRequested?.Invoke();
        }

        private void PlayAlbum_Click(object sender, RoutedEventArgs e) {
            PlayRequested?.Invoke(GetPlaybackSongs());
        }

        private void AddToQueue_Click(object sender, RoutedEventArgs e) {
            AddToQueueRequested?.Invoke(GetPlaybackSongs());
        }

        private void InsertInQueue_Click(object sender, RoutedEventArgs e) {
            InsertNextRequested?.Invoke(GetPlaybackSongs());
        }

        private void EditAlbum_Click(object sender, RoutedEventArgs e) {
            EditAlbumRequested?.Invoke(album);
        }

        #region Context menu methods
        private void EditSong_Click(object sender, RoutedEventArgs e) {
            if (AlbumSongsGrid.SelectedItem is not Song song) return;
            EditSongRequested?.Invoke(song);
        }

        private void MoveSongToAlbum_Click(object sender, RoutedEventArgs e) {
            return;
        }

        private void AddSongToMix_Click(object sender, RoutedEventArgs e) {
            return;
        }
        #endregion

        #region Song Reordering
        private void ReorderHandle_MouseDown(object sender, MouseButtonEventArgs e) {
            if (sender is not FrameworkElement element) return;
            if (element.DataContext is not Song song) return;

            pendingDragSong = song;
            dragStart = e.GetPosition(AlbumSongsGrid);
            e.Handled = true;
        }

        private void AlbumSongsGrid_PreviewMouseMove(object sender, MouseEventArgs e) {
            if (e.LeftButton != MouseButtonState.Pressed) {
                pendingDragSong = null;
                return;
            }
            if (pendingDragSong == null) return;

            Point current = e.GetPosition(AlbumSongsGrid);
            if (Math.Abs(current.X - dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
                    Math.Abs(current.Y - dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) {
                return;
            }

            Song song = pendingDragSong;
            pendingDragSong = null;
            DataObject data = new(typeof(AlbumSongDragData), new AlbumSongDragData(album, song));
            DragDrop.DoDragDrop(AlbumSongsGrid, data, DragDropEffects.Move);
        }

        private bool TryGetDraggedSong(DragEventArgs e, out Song? song) {
            song = null;

            if (e.Data.GetData(typeof(AlbumSongDragData)) is not AlbumSongDragData data) return false;
            if (!ReferenceEquals(data.Album, album)) return false;
            if (!album.Songs.Contains(data.Song)) return false;

            song = data.Song;
            return true;
        }

        private void AlbumSongsGrid_DragOver(object sender, DragEventArgs e) {
            e.Effects = TryGetDraggedSong(e, out _) ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }

        private void AlbumSongsGrid_Drop(object sender, DragEventArgs e) {
            e.Handled = true;
            e.Effects = DragDropEffects.None;

            if (!TryGetDraggedSong(e, out Song? draggedSong)) return;

            List<Song> reordered = album.Songs.ToList();

            int sourceIndex = reordered.IndexOf(draggedSong!);
            int insertIndex = reordered.Count;

            if (e.OriginalSource is DependencyObject source &&
                    ItemsControl.ContainerFromElement(AlbumSongsGrid, source) is DataGridRow { Item: Song targetSong } targetRow) {
                insertIndex = reordered.IndexOf(targetSong);
                if (e.GetPosition(targetRow).Y >= targetRow.ActualHeight / 2) insertIndex++;
            }

            reordered.RemoveAt(sourceIndex);
            if (sourceIndex < insertIndex) insertIndex--;
            reordered.Insert(insertIndex, draggedSong!);
            if (reordered.SequenceEqual(album.Songs)) return;

            SongOrderRequested?.Invoke(album, reordered);
            e.Effects = DragDropEffects.Move;
        }
        #endregion

        private void AlbumSongsGrid_LeftMouseDown(object sender, RoutedEventArgs e) {
            DependencyObject? element = e.OriginalSource as DependencyObject;
            while (element != null) {
                if (element is DataGridRow) return;
                element = VisualTreeHelper.GetParent(element);
            }
            AlbumSongsGrid.UnselectAll();
        }

        private void AlbumIndex_Loaded(object sender, RoutedEventArgs e) {
            if (sender is not TextBlock textBlock) return;
            DataGridRow? row = ItemsControl.ContainerFromElement(AlbumSongsGrid, textBlock) as DataGridRow;
            if (row != null) textBlock.Text = (row.GetIndex() + 1).ToString();
        }
    }
}
