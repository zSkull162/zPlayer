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
        bool AllowDuplicatePlaybackSongs = false,
        ThemeSettings? Theme = null
    );

    public sealed record ThemeSettings(
        string Background = "#FF1C1D29",
        string Surface = "#FF232431",
        string Border = "#FF2B2C3B",
        string Highlight = "#FF3B3C4C",
        string Selected = "#FF2680D9",
        string SelectedSecondary = "#FF27C0D9",
        string Text = "#FFF2F2F2"
    );
}
