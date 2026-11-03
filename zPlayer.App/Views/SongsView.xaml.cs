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
        public SongCommands Commands { get; }
        private ICollectionView? songsView;

        public SongsView(MusicLibrary library, SongCommands commands) {
            Commands = commands;
            InitializeComponent();
            DataContext = library;

            songsView = CollectionViewSource.GetDefaultView(library.Songs);
            songsView.Filter = FilterSong;
            SongsInLibraryText.Text = $"— {library.Songs.Count} in library";
        }

        private bool FilterSong(object item) {
            if (item is not Song song) return true;
            string search = SongSearchBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(search)) return true;

            static bool Contains(string originalString, string term) {
                return originalString.Contains(term, StringComparison.OrdinalIgnoreCase);
            }

            return Contains(song.Title, search) || Contains(song.Artist, search) || Contains(song.AlbumTitle, search) || Contains(song.AlbumArtist, search);
        }

        private void SongSearchBox_TextChanged(object sender, TextChangedEventArgs e) {
            songsView?.Refresh();
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

        private void SongsGrid_RightMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.OriginalSource is DependencyObject source &&
                ItemsControl.ContainerFromElement(SongsGrid, source) is DataGridRow { Item: Song song }) {
                SongsGrid.SelectedItem = song;
            }
            else {
                SongsGrid.UnselectAll();
            }
        }

        private void SongsGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e) {
            if (SongsGrid.SelectedItem is not Song) e.Handled = true;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
