
namespace zPlayer.Core
{
    public enum MixSortType {
        Title = 0,
        Artist = 1,
        Album = 2,
        Length = 3,
        Manual = 4,
        TimeAdded = 5
    }

    public sealed class Mix
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = "New Mix";
        public MixSortType SortType { get; set; } = MixSortType.TimeAdded;
        public bool SortDescending { get; set; }
        public List<MixSong> Entries { get; set; } = [];

        public int SongCount => Entries.Count;

        public int AddSongs(IEnumerable<Guid> songIds) {
            HashSet<Guid> existing = Entries.Select(entry => entry.SongId).ToHashSet();
            int nextPosition = Entries.Select(entry => entry.Position).DefaultIfEmpty(-1).Max() + 1;

            DateTimeOffset addedAt = DateTimeOffset.UtcNow;
            int added = 0;

            foreach (Guid songId in songIds) {
                if (songId == Guid.Empty) throw new ArgumentException("A song ID cannot be empty.", nameof(songIds));
                if (!existing.Add(songId)) continue;

                Entries.Add(new MixSong(songId, nextPosition++, addedAt));
                added++;
            }
            return added;
        }

        public int RemoveSongs(IEnumerable<Guid> songIds) {
            HashSet<Guid> removedIds = songIds.ToHashSet();
            int removed = Entries.RemoveAll(entry => removedIds.Contains(entry.SongId));
            NormalizePositions();
            return removed;
        }

        public void SetOrder(IReadOnlyList<Guid> songIds) {
            Dictionary<Guid, MixSong> existing = Entries.ToDictionary(entry => entry.SongId);

            if (songIds.Count != existing.Count || songIds.Distinct().Count() != songIds.Count ||
                songIds.Any(id => !existing.ContainsKey(id))) {
                throw new ArgumentException("The order must contain every song in the mix exactly once.", nameof(songIds));
            }
            Entries = songIds.Select((id, index) => existing[id] with { Position = index }).ToList();
        }

        private void NormalizePositions() {
            Entries = Entries.OrderBy(entry => entry.Position).Select((entry, index) => entry with { Position = index }).ToList();
        }

        public Mix Clone() {
            return new Mix {
                Id = Id,
                Title = Title,
                SortType = SortType,
                SortDescending = SortDescending,
                Entries = [.. Entries]
            };
        }
    }
}
