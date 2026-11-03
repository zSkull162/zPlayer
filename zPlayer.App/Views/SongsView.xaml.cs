using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using zPlayer.Core;

namespace zPlayer.App.Views
{
    public partial class SongsView : UserControl
    {
        public event Action? ImportSongsRequested;
        public event Action<Song>? EditSongRequested;

        public SongsView(MusicLibrary library) {
            InitializeComponent();
            DataContext = library;
            SongsInLibraryText.Text = $"— {library.Songs.Count} in library";
        }

        private void ImportSongs_Click(object sender, RoutedEventArgs e) {
            ImportSongsRequested?.Invoke();
        }

        private void SongsGrid_LeftMouseDown(object sender, RoutedEventArgs e) {
            DependencyObject? element = e.OriginalSource as DependencyObject;
            while (element != null) {
                if (element is DataGridRow) return;
                element = VisualTreeHelper.GetParent(element);
            }
            SongsGrid.UnselectAll();
        }

        #region Context menu methods
        private void PlaySong_Click(object sender, RoutedEventArgs e) {
            return;
        }

        private void PlaySongNext_Click(object sender, RoutedEventArgs e) {
            return;
        }

        private void AddSongToQueue_Click(object sender, RoutedEventArgs e) {
            return;
        }

        private void EditSong_Click(object sender, RoutedEventArgs e) {
            if (SongsGrid.SelectedItem is not Song song) return;
            EditSongRequested?.Invoke(song);
        }

        private void DeleteSong_Click(object sender, RoutedEventArgs e) {
            return;
        }

        private void MoveSongToAlbum_Click(object sender, RoutedEventArgs e) {
            return;
        }

        private void AddSongToMix_Click(object sender, RoutedEventArgs e) {
            return;
        }
        #endregion
    }
}
