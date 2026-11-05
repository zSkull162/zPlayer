
namespace zPlayer.App
{
    public sealed class SortState
    {
        public int TypeIndex { get; set; }
        public int OrderIndex { get; set; }
    }
    public sealed class ViewState
    {
        public SortState SongsSort { get; } = new();
        public SortState AlbumsSort { get; } = new();
        public SortState MixesSort { get; } = new();
    }
}
