using System.Windows.Input;
using zPlayer.App.Playback;
using zPlayer.Core;

namespace zPlayer.App.Commands
{
    public sealed class MixCommands
    {
        public ICommand PlayMixCommand { get; }
        public ICommand PlayMixNextCommand { get; }
        public ICommand AddMixToQueueCommand { get; }
        public ICommand EditMixCommand { get; }
        public ICommand DeleteMixCommand { get; }

        public MixCommands(MusicLibrary library, PlaybackController player, Action<Mix> editMix, Action<Mix[]> deleteMixes) {
            Song[] GetSongs(Mix[] mixes) {
                return mixes.SelectMany(mix => library.GetMixSongs(mix.Id)).DistinctBy(song => song.Id).ToArray();
            }
            PlayMixCommand = new SelectionCommand<Mix>(mixes => player.PlayQueue(GetSongs(mixes)));
            PlayMixNextCommand = new SelectionCommand<Mix>(mixes => player.InsertNext(GetSongs(mixes)));
            AddMixToQueueCommand = new SelectionCommand<Mix>(mixes => player.AddToQueue(GetSongs(mixes)));
            EditMixCommand = new RelayCommand<Mix>(editMix);
            DeleteMixCommand = new SelectionCommand<Mix>(deleteMixes);
        }
    }
}