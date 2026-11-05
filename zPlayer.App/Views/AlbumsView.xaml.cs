using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using zPlayer.Core;

namespace zPlayer.App.Views
{
    public sealed class CreateAlbumTile { }

    public partial class AlbumsView : UserControl
    {
        private readonly MusicLibrary library;
        private readonly SortState sortState;
        private bool restoringSort;

        public event Action<Album>? AlbumOpened;
        public event Action? CreateAlbumRequested;
        public event Action<Album>? EditAlbumRequested;

        public AlbumsView(MusicLibrary library, SortState sortState) {
            this.library = library;
            this.sortState = sortState;
            restoringSort = true;
            InitializeComponent();
            
            SortTypeBox.SelectedIndex = sortState.TypeIndex;
            SortOrderBox.SelectedIndex = sortState.OrderIndex;

            restoringSort = false;
            DataContext = library;
            RefreshAlbums();
        }

        public void RefreshAlbums() {
            AlbumsGrid.ItemsSource = ApplySorting();
            AlbumsInLibraryText.Text = $"— {library.Albums.Count} in library";
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
            static IOrderedEnumerable<Album> OrderedList<T>(IEnumerable<Album> albums, Func<Album, T> keySelector, bool descending, IComparer<T>? comparer = null) {
                return descending ? albums.OrderByDescending(keySelector, comparer) : albums.OrderBy(keySelector, comparer);
            }

            IEnumerable<Album> filtered = library.Albums.Where(album =>
                string.IsNullOrWhiteSpace(search) || Contains(album.Title, search) || Contains(album.Artist, search));

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
            items.AddRange(sorted);
            return items;
        }

        private void SortBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (restoringSort || !IsInitialized) return;
            sortState.TypeIndex = SortTypeBox.SelectedIndex;
            sortState.OrderIndex = SortOrderBox.SelectedIndex;
            RefreshAlbums();
        }

        private void AlbumSearchBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (!IsInitialized) return;
            RefreshAlbums();
        }
        #endregion

        private void CreateAlbum_Click(object sender, RoutedEventArgs e) {
            CreateAlbumRequested?.Invoke();
        }

        #region Context menu methods
        private void PlayAlbum_Click(object sender, RoutedEventArgs e) {
            return;
        }

        private void PlayAlbumNext_Click(object sender, RoutedEventArgs e) {
            return;
        }

        private void AddAlbumToQueue_Click(object sender, RoutedEventArgs e) {
            return;
        }

        private void EditAlbum_Click(object sender, RoutedEventArgs e) {
            if (AlbumsGrid.SelectedItem is Album album) EditAlbumRequested?.Invoke(album);
        }

        private void DeleteAlbum_Click(object sender, RoutedEventArgs e) {
            return;
        }

        private void AddAlbumSongsToMix_Click(object sender, RoutedEventArgs e) {
            return;
        }
        #endregion

        private void AlbumsGrid_LeftMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.OriginalSource is DependencyObject source &&
                ItemsControl.ContainerFromElement(AlbumsGrid, source) is ListBoxItem) {
                return;
            }
            AlbumsGrid.UnselectAll();
        }

        private void AlbumsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
            if (e.OriginalSource is not DependencyObject source) return;
            if (ItemsControl.ContainerFromElement(AlbumsGrid, source) is ListBoxItem { DataContext: Album album }) {
                AlbumOpened?.Invoke(album);
            }
        }
    }
}
