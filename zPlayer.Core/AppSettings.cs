using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace zPlayer.Core
{
    public sealed record zPlayerRpcSettings(
        bool Enabled = true,
        bool DisplayPausedState = true,
        bool DisplayAlbumName = true,
        int StatusType = 2
    );

    public sealed record AppSettings(
        bool ShowImportSummary = true,
        bool ShowSongDeleteConfirmation = true,
        bool ShowAlbumDeleteConfirmation = true,
        bool ShowMixDeleteConfirmation = true,
        bool AllowDuplicatePlaybackSongs = false
    );
}
