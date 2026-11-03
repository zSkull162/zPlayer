using System.Windows.Controls;
using zPlayer.Core;

namespace zPlayer.App.Views
{
    public partial class MoveToAlbumView : UserControl
    {
        private readonly Song draft;

        public MoveToAlbumView(MusicLibrary library, Song song) {
            draft = song.Clone();
            InitializeComponent();
        }
    }
}
