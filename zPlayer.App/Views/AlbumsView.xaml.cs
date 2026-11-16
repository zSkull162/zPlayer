using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using zPlayer.Core;
using zPlayer.App.Commands;

namespace zPlayer.App.Views
{
    public sealed class CreateAlbumTile { }

    public partial class AlbumsView : UserControl
    {
        private const int PageSize = 40;
        private int page;
        private readonly MusicLibrary library;
        private readonly SortState sortState;
        private bool restoringSort;

        public AlbumCommands Commands { get; }
        public event Action<Album>? AlbumOpened;
        public event Action? CreateAlbumRequested;

        public AlbumsView(MusicLibrary library, AlbumCommands commands, SortState sortState) {
            Commands = commands;
            this.library = library;
            this.sortState = sortState;
            restoringSort = true;
            InitializeComponent();
            
            SortTypeBox.SelectedIndex = sortState.TypeIndex;
            SortOrderBox.SelectedIndex = sortState.OrderIndex;
            if (!string.IsNullOrWhiteSpace(sortState.FilterString)) AlbumSearchBox.Text = sortState.FilterString;

            restoringSort = false;
            DataContext = library;
            RefreshAlbums();
        }

        public void RefreshAlbums() {
            HashSet<Guid> selectedIds = AlbumsGrid.SelectedItems.OfType<Album>().Select(album => album.Id).ToHashSet();
            List<object> items = ApplySorting();
            AlbumsGrid.ItemsSource = items;

            foreach (Album album in items.OfType<Album>()) {
                if (selectedIds.Contains(album.Id)) AlbumsGrid.SelectedItems.Add(album);
            }
            AlbumsInLibraryText.Text = $"— {library.Albums.Count} in library";
        }

        private void UpdatePageUI(int pageCount) {
            PageText.Text = $"Page {page + 1} of {pageCount}";
            PageControls.Visibility = pageCount > 1 ? Visibility.Visible : Visibility.Collapsed;

            PreviousPageButton.IsEnabled = page > 0;
            NextPageButton.IsEnabled = page + 1 < pageCount;

            PreviousPageButton.Opacity = page > 0 ? 1 : 0.5;
            NextPageButton.Opacity = page + 1 < pageCount ? 1 : 0.5;
        }

        #region Filtering and sorting
        private List<object> ApplySorting() {
            string search = AlbumSearchBox.Text.Trim();
            bool descending = SortOrderBox.SelectedIndex == 1;
            bool numeric = SortTypeBox.SelectedIndex == 2;
            ((ComboBoxItem)SortOrderBox.Items[0]).Content = numeric ? "Low-High" : "A-Z";
            ((ComboBoxItem)SortOrderBox.Items[1]).Content = numeric ? "High-Low" : "Z-A";

            static bool Contains(string originalString, string term) {
                return originalString.Contains(term, StringComparison.OrdinalIgnoreCase);
            }
            static bool FilterBySearchType(Album album, string search, int typeIndex) {
                if (string.IsNullOrEmpty(search)) return true;
                return typeIndex switch {
                    1 => Contains(album.Artist, search),
                    _ => Contains(album.Title, search)
                };
            }
            static IOrderedEnumerable<Album> OrderedList<T>(IEnumerable<Album> albums, Func<Album, T> keySelector, bool descending, IComparer<T>? comparer = null) {
                return descending ? albums.OrderByDescending(keySelector, comparer) : albums.OrderBy(keySelector, comparer);
            }

            IEnumerable<Album> filtered = library.Albums.Where(album => FilterBySearchType(album, search, SortTypeBox.SelectedIndex));

            IOrderedEnumerable<Album> sorted = SortTypeBox.SelectedIndex switch {
                1 => OrderedList(filtered, album => album.Artist, descending, StringComparer.OrdinalIgnoreCase),
                2 => OrderedList(filtered, album => album.Duration, descending),
                _ => OrderedList(filtered, album => album.Title, descending, StringComparer.OrdinalIgnoreCase)
            };

            if (SortTypeBox.SelectedIndex != 0) {
                sorted = sorted.ThenBy(album => album.Title, StringComparer.OrdinalIgnoreCase);
            }
            object? selectedItem = AlbumsGrid.SelectedItem;
            List<object> items = [new CreateAlbumTile()];
            var ordered = sorted.ToList();
            int pageCount = Math.Max(1, (ordered.Count + PageSize - 1) / PageSize);
            page = Math.Clamp(page, 0, pageCount - 1);
            items.AddRange(ordered.Skip(page * PageSize).Take(PageSize));

            UpdatePageUI(pageCount);
            return items;
        }

        private void PreviousPage_Click(object sender, RoutedEventArgs e) {
            page--;
            RefreshAlbums();
        }

        private void NextPage_Click(object sender, RoutedEventArgs e) {
            page++;
            RefreshAlbums();
        }

        private void SortBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (restoringSort || !IsInitialized) return;
            sortState.TypeIndex = SortTypeBox.SelectedIndex;
            sortState.OrderIndex = SortOrderBox.SelectedIndex;
            page = 0;
            RefreshAlbums();
        }

        private void AlbumSearchBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (!IsInitialized) return;
            sortState.FilterString = AlbumSearchBox.Text;
            page = 0;
            RefreshAlbums();
        }
        #endregion

        private void CreateAlbum_Click(object sender, RoutedEventArgs e) {
            CreateAlbumRequested?.Invoke();
        }

        #region AlbumsGrid event handlers
        private void AlbumsGrid_LeftMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.OriginalSource is DependencyObject source &&
                ItemsControl.ContainerFromElement(AlbumsGrid, source) is ListBoxItem) {
                return;
            }
            AlbumsGrid.UnselectAll();
        }

        private void AlbumsGrid_RightMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.OriginalSource is DependencyObject source &&
                    ItemsControl.ContainerFromElement(AlbumsGrid, source) is ListBoxItem { DataContext: Album album }) {
                if (!AlbumsGrid.SelectedItems.Contains(album)) {
                    AlbumsGrid.UnselectAll();
                    AlbumsGrid.SelectedItem = album;
                }
                e.Handled = true;
            }
            else {
                AlbumsGrid.UnselectAll();
                e.Handled = true;
            }
        }

        private void AlbumsGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e) {
            if (!AlbumsGrid.SelectedItems.OfType<Album>().Any()) e.Handled = true;
            CommandManager.InvalidateRequerySuggested();
        }

        private void AlbumsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
            if (e.OriginalSource is not DependencyObject source) return;
            if (ItemsControl.ContainerFromElement(AlbumsGrid, source) is ListBoxItem { DataContext: Album album }) {
                AlbumOpened?.Invoke(album);
            }
        }
        #endregion
    }
}
