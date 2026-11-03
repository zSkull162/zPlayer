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

        public event Action<Album>? AlbumOpened;
        public event Action? CreateAlbumRequested;
        public event Action<Album>? EditAlbumRequested;

        public AlbumsView(MusicLibrary library) {
            this.library = library;
            InitializeComponent();
            DataContext = library;
            RefreshAlbums();
        }

        public void RefreshAlbums() {
            string search = AlbumSearchBox.Text.Trim();
            static bool Contains(string originalString, string term) {
                return originalString.Contains(term, StringComparison.OrdinalIgnoreCase);
            }

            List<object> items = [new CreateAlbumTile()];
            items.AddRange(library.GetAlbums().Where(album =>
                string.IsNullOrWhiteSpace(search) ||
                Contains(album.Title, search) || Contains(album.Artist, search))
            );

            AlbumsGrid.ItemsSource = items;
            AlbumsInLibraryText.Text = $"— {library.Albums.Count} in library";
        }

        private void AlbumSearchBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (!IsInitialized) return;
            RefreshAlbums();
        }

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
