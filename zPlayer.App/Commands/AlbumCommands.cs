using System;
using System.Linq;
using System.Windows.Input;
using zPlayer.App.Playback;
using zPlayer.Core;

namespace zPlayer.App.Commands
{
    public sealed class AlbumCommands
    {
        public ICommand PlayAlbumsCommand { get; }
        public ICommand PlayAlbumsNextCommand { get; }
        public ICommand AddAlbumsToQueueCommand { get; }
        public ICommand EditAlbumsCommand { get; }
        public ICommand DeleteAlbumsCommand { get; }

        public AlbumCommands(PlaybackController player, Action<Album[]> editAlbums, Action<Album[]> deleteAlbums) {
            PlayAlbumsCommand = new SelectionCommand<Album>(albums => player.PlayQueue(GetSongs(albums)));
            PlayAlbumsNextCommand = new SelectionCommand<Album>(albums => player.InsertNext(GetSongs(albums)));
            AddAlbumsToQueueCommand = new SelectionCommand<Album>(albums => player.AddToQueue(GetSongs(albums)));
            EditAlbumsCommand = new SelectionCommand<Album>(editAlbums);
            DeleteAlbumsCommand = new SelectionCommand<Album>(deleteAlbums);
        }

        private static Song[] GetSongs(Album[] albums) {
            return albums.SelectMany(album => album.Songs).DistinctBy(song => song.Id).ToArray();
        }
    }
}