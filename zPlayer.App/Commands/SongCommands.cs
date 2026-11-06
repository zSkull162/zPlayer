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
        public ICommand AddSongsToMixCommand { get; }

        public SongCommands(PlaybackController player, Action<Song[]> editSongs, Action<Song[]> deleteSongs, Action<Song[]> moveSongs, Action<Song[]> addSongsToMix) {
            PlaySongCommand = new SelectionCommand<Song>(songs => player.PlayQueue(songs));
            PlaySongNextCommand = new SelectionCommand<Song>(songs => player.InsertNext(songs));
            AddSongToQueueCommand = new SelectionCommand<Song>(songs => player.AddToQueue(songs));
            EditSongCommand = new SelectionCommand<Song>(editSongs);
            DeleteSongCommand = new SelectionCommand<Song>(deleteSongs);
            MoveSongToAlbumCommand = new SelectionCommand<Song>(moveSongs);
            AddSongsToMixCommand = new SelectionCommand<Song>(addSongsToMix);
        }
    }
}
