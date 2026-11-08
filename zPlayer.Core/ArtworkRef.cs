namespace zPlayer.Core
{
    public enum ArtworkOwnerKind
    {
        Song,
        Album,
        Mix
    }

    public readonly record struct ArtworkRef(ArtworkOwnerKind Kind, Guid OwnerId, long Revision);
}
