using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using zPlayer.Core;
using System.Windows.Input;
using zPlayer.App.Commands;

namespace zPlayer.App.Views
{
    public partial class SongsView : UserControl
    {
        public event Action? ImportSongsRequested;
        public event Action? ImportFolderRequested;
        public SongCommands Commands { get; }
        private readonly SortState sortState;
        bool restoringSort;

        private ICollectionView? songsView;

        public SongsView(MusicLibrary library, SongCommands commands, SortState sortState) {
            Commands = commands;
            this.sortState = sortState;
            restoringSort = true;
            InitializeComponent();
            DataContext = library;

            SortTypeBox.SelectedIndex = sortState.TypeIndex;
            SortOrderBox.SelectedIndex = sortState.OrderIndex;

            songsView = CollectionViewSource.GetDefaultView(library.Songs);
            songsView.Filter = FilterSong;

            restoringSort = false;
            ApplySorting();
            SongsInLibraryText.Text = $"— {library.Songs.Count} in library";
        }

        #region Filtering and sorting
        private bool FilterSong(object item) {
            if (item is not Song song) return true;
            string search = SongSearchBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(search)) return true;

            static bool Contains(string originalString, string term) {
                return originalString.Contains(term, StringComparison.OrdinalIgnoreCase);
            }

            return Contains(song.Title, search) || Contains(song.Artist, search) || Contains(song.AlbumTitle, search) || Contains(song.AlbumArtist, search);
        }

        private void ApplySorting() {
            if (songsView == null) return;

            string property = SortTypeBox.SelectedIndex switch {
                1 => nameof(Song.Artist),
                2 => nameof(Song.AlbumTitle),
                3 => nameof(Song.Duration),
                _ => nameof(Song.Title)
            };
            bool sortingLength = property == nameof(Song.Duration);
            ((ComboBoxItem)SortOrderBox.Items[0]).Content = sortingLength ? "Low-High" : "A-Z";
            ((ComboBoxItem)SortOrderBox.Items[1]).Content = sortingLength ? "High-Low" : "Z-A";

            ListSortDirection direction = SortOrderBox.SelectedIndex == 1 ? ListSortDirection.Descending : ListSortDirection.Ascending;
            using (songsView.DeferRefresh()) {
                songsView.SortDescriptions.Clear();
                songsView.SortDescriptions.Add(new SortDescription(property, direction));

                if (property != nameof(Song.Title)) {
                    songsView.SortDescriptions.Add(new SortDescription(nameof(Song.Title), ListSortDirection.Ascending));
                }
            }
        }

        private void SortBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (restoringSort || songsView == null) return;
            sortState.TypeIndex = SortTypeBox.SelectedIndex;
            sortState.OrderIndex = SortOrderBox.SelectedIndex;
            ApplySorting();
        }

        private void SongSearchBox_TextChanged(object sender, TextChangedEventArgs e) {
            songsView?.Refresh();
        }
        #endregion

        private void ImportSongs_Click(object sender, RoutedEventArgs e) {
            ImportSongsRequested?.Invoke();
        }

        private void ImportFolder_Click(object sender, RoutedEventArgs e) {
            ImportFolderRequested?.Invoke();
        }

        #region SongsGrid event handlers
        private void SongsGrid_LeftMouseDown(object sender, RoutedEventArgs e) {
            DependencyObject? element = e.OriginalSource as DependencyObject;
            while (element != null) {
                if (element is DataGridRow) return;
                element = VisualTreeHelper.GetParent(element);
            }
            SongsGrid.UnselectAll();
        }

        private void SongsGrid_RightMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.OriginalSource is DependencyObject source &&
                    ItemsControl.ContainerFromElement(SongsGrid, source) is DataGridRow { Item: Song song }) {
                if (!SongsGrid.SelectedItems.Contains(song)) {
                    SongsGrid.UnselectAll();
                    SongsGrid.SelectedItem = song;
                }
                e.Handled = true;
            }
            else SongsGrid.UnselectAll();
        }

        private void SongsGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e) {
            if (SongsGrid.SelectedItems.Count == 0) e.Handled = true;
            CommandManager.InvalidateRequerySuggested();
        }
        #endregion
    }
}
