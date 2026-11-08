using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using zPlayer.App.Commands;
using zPlayer.Core;

namespace zPlayer.App.Views
{
    public partial class MixDetailsView : UserControl
    {
        private sealed class MixSongDragData
        {
            public Mix Mix { get; }
            public Song Song { get; }

            public MixSongDragData(Mix mix, Song song) {
                Mix = mix;
                Song = song;
            }
        }
        private Point dragStart;
        private Song? pendingDragSong;
        public event Action<Mix, IReadOnlyList<Song>>? SongOrderRequested;

        private readonly Mix mix;
        private readonly MusicLibrary library;
        private bool initializing = true;

        public SongCommands Commands { get; }
        public ICommand RemoveFromMixCommand { get; }

        public event Action<Song[]>? PlayRequested;
        public event Action<Song[]>? AddToQueueRequested;
        public event Action<Song[]>? InsertNextRequested;
        public event Action? BackRequested;
        public event Action<Mix>? EditMixRequested;
        public event Action<Song[]>? RemoveSongsRequested;
        public event Action<MixSortType, bool>? SortRequested;

        public MixDetailsView(MusicLibrary library, Mix mix, SongCommands songCommands) {
            this.library = library;
            this.mix = mix;
            Commands = songCommands;
            RemoveFromMixCommand = new SelectionCommand<Song>(songs => RemoveSongsRequested?.Invoke(songs));

            InitializeComponent();
            RefreshView();
        }

        private Song[] GetPlaybackSongs() {
            return library.GetMixSongs(mix.Id);
        }

        public void RefreshView() {
            initializing = true;
            try {
                library.RefreshMixes();
                HashSet<Guid> selectedIds = MixSongsGrid.SelectedItems.OfType<Song>().Select(song => song.Id).ToHashSet();
                DataContext = null;
                DataContext = mix;

                MixTitleText.Text = $"— {mix.Title}";
                SortTypeBox.SelectedIndex = (int)mix.SortType;
                SortOrderBox.SelectedIndex = mix.SortDescending ? 1 : 0;
                UpdateSortLabels();

                MixSongsGrid.ItemsSource = GetPlaybackSongs();
                foreach (Song song in MixSongsGrid.Items.OfType<Song>()) {
                    if (selectedIds.Contains(song.Id)) MixSongsGrid.SelectedItems.Add(song);
                }
                UpdateMixInfoText();
            }
            finally {
                initializing = false;
            }
        }

        private void UpdateMixInfoText() {
            TimeSpan duration = mix.Duration;
            string formatted = duration.TotalHours >= 1 ? duration.ToString(@"h\:mm\:ss") : duration.ToString(@"m\:ss");
            MixInfoText.Text = $"{mix.SongCount} Songs — {formatted}";
        }

        private void UpdateSortLabels() {
            string[] labels = mix.SortType switch {
                MixSortType.Length => ["Low-High", "High-Low"],
                MixSortType.TimeAdded => ["Oldest First", "Newest First"],
                MixSortType.Manual => ["First-Last", "Last-First"],
                _ => ["A-Z", "Z-A"]
            };
            ((ComboBoxItem)SortOrderBox.Items[0]).Content = labels[0];
            ((ComboBoxItem)SortOrderBox.Items[1]).Content = labels[1];
        }

        private void SortBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (initializing || SortTypeBox.SelectedIndex < 0 || SortOrderBox.SelectedIndex < 0) return;

            SortRequested?.Invoke((MixSortType)SortTypeBox.SelectedIndex, SortOrderBox.SelectedIndex == 1);
            RefreshView();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e) {
            BackRequested?.Invoke();
        }

        private void PlayMix_Click(object sender, RoutedEventArgs e) {
            PlayRequested?.Invoke(GetPlaybackSongs());
        }

        private void AddToQueue_Click(object sender, RoutedEventArgs e) {
            AddToQueueRequested?.Invoke(GetPlaybackSongs());
        }

        private void InsertInQueue_Click(object sender, RoutedEventArgs e) {
            InsertNextRequested?.Invoke(GetPlaybackSongs());
        }

        private void EditMix_Click(object sender, RoutedEventArgs e) {
            EditMixRequested?.Invoke(mix);
        }

        #region MixSongsGrid event handlers
        private void MixSongsGrid_LeftMouseDown(object sender, RoutedEventArgs e) {
            DependencyObject? element = e.OriginalSource as DependencyObject;
            while (element != null) {
                if (element is DataGridRow) return;
                element = VisualTreeHelper.GetParent(element);
            }
            MixSongsGrid.UnselectAll();
        }

        private void MixSongsGrid_RightMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.OriginalSource is DependencyObject source &&
                    ItemsControl.ContainerFromElement(MixSongsGrid, source) is DataGridRow { Item: Song song }) {
                if (!MixSongsGrid.SelectedItems.Contains(song)) {
                    MixSongsGrid.UnselectAll();
                    MixSongsGrid.SelectedItem = song;
                }
                e.Handled = true;
            }
            else MixSongsGrid.UnselectAll();
        }

        private void MixSongsGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e) {
            if (MixSongsGrid.SelectedItems.Count == 0) e.Handled = true;
            CommandManager.InvalidateRequerySuggested();
        }
        #endregion

        #region Song Reordering
        private void ReorderHandle_MouseDown(object sender, MouseButtonEventArgs e) {
            if (mix.SortType != MixSortType.Manual) return;
            if (sender is not FrameworkElement element) return;
            if (element.DataContext is not Song song) return;

            pendingDragSong = song;
            dragStart = e.GetPosition(MixSongsGrid);
            e.Handled = true;
        }

        private void MixSongsGrid_PreviewMouseMove(object sender, MouseEventArgs e) {
            if (e.LeftButton != MouseButtonState.Pressed) {
                pendingDragSong = null;
                return;
            }
            if (pendingDragSong == null) return;

            Point current = e.GetPosition(MixSongsGrid);
            if (Math.Abs(current.X - dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
                    Math.Abs(current.Y - dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) {
                return;
            }

            Song song = pendingDragSong;
            pendingDragSong = null;
            DataObject data = new(typeof(MixSongDragData), new MixSongDragData(mix, song));
            DragDrop.DoDragDrop(MixSongsGrid, data, DragDropEffects.Move);
        }

        private bool TryGetDraggedSong(DragEventArgs e, out Song? song) {
            song = null;

            if (mix.SortType != MixSortType.Manual) return false;
            if (e.Data.GetData(typeof(MixSongDragData)) is not MixSongDragData data) return false;
            if (!ReferenceEquals(data.Mix, mix)) return false;
            if (!mix.Entries.Any(entry => entry.SongId == data.Song.Id)) return false;

            song = data.Song;
            return true;
        }

        private void MixSongsGrid_DragOver(object sender, DragEventArgs e) {
            e.Effects = TryGetDraggedSong(e, out _) ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }

        private void MixSongsGrid_Drop(object sender, DragEventArgs e) {
            e.Handled = true;
            e.Effects = DragDropEffects.None;

            if (!TryGetDraggedSong(e, out Song? draggedSong)) return;

            List<Song> reordered = GetPlaybackSongs().ToList();

            int sourceIndex = reordered.IndexOf(draggedSong!);
            if (sourceIndex < 0) return;
            int insertIndex = reordered.Count;

            if (e.OriginalSource is DependencyObject source &&
                    ItemsControl.ContainerFromElement(MixSongsGrid, source) is DataGridRow { Item: Song targetSong } targetRow) {
                insertIndex = reordered.IndexOf(targetSong);
                if (e.GetPosition(targetRow).Y >= targetRow.ActualHeight / 2) insertIndex++;
            }

            reordered.RemoveAt(sourceIndex);
            if (sourceIndex < insertIndex) insertIndex--;
            reordered.Insert(insertIndex, draggedSong!);
            if (reordered.Select(song => song.Id).SequenceEqual(GetPlaybackSongs().Select(song => song.Id))) return;

            SongOrderRequested?.Invoke(mix, reordered);
            e.Effects = DragDropEffects.Move;
        }
        #endregion

    }
}
