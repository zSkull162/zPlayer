using System.Windows.Input;
using zPlayer.App.Playback;
using zPlayer.Core;

namespace zPlayer.App.Commands
{
    public sealed class SongCommands
    {
        public ICommand PlaySongCommand { get; }
        public ICommand PlaySongNextCommand { get; }
        public ICommand AddSongToQueueCommand { get; }
        public ICommand EditSongCommand { get; }
        public ICommand DeleteSongCommand { get; }
        public ICommand MoveSongToAlbumCommand { get; }

        public SongCommands(PlaybackController player, Action<Song> editSong, Action<Song> deleteSong, Action<Song> moveSongToAlbum) {
            PlaySongCommand = new RelayCommand<Song>(song => player.PlayQueue([song]));
            PlaySongNextCommand = new RelayCommand<Song>(song => player.InsertNext([song]));
            AddSongToQueueCommand = new RelayCommand<Song>(song => player.AddToQueue([song]));
            EditSongCommand = new RelayCommand<Song>(editSong);
            DeleteSongCommand = new RelayCommand<Song>(deleteSong);
            MoveSongToAlbumCommand = new RelayCommand<Song>(moveSongToAlbum);
        }
    }
}
