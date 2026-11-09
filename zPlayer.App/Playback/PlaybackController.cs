using System.Diagnostics;
using NAudio.Vorbis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.IO;
using System.Windows;
using zPlayer.Core;

namespace zPlayer.App.Playback
{
    public enum LoopMode {
        Off, All, One
    }

    public sealed class PlaybackController : IDisposable
    {
        private readonly Random random = new();

        private readonly List<Song> queue = [];
        private readonly List<int> playOrder = [];
        private int orderPos = -1;
        public int QueueCount => queue.Count;
        public int CurrentQueueIndex => orderPos;

        private WaveStream? reader;
        private WaveOut? output;

        private VolumeSampleProvider? volumeProvider;
        private double volume = 0.5;

        public double Volume {
            get => volume;
            set {
                volume = Math.Clamp(value, 0, 1);
                if (volumeProvider != null) volumeProvider.Volume = (float)volume;
            }
        }

        public LoopMode Loop { get; private set; } = LoopMode.Off;
        public bool AutoReshuffle { get; private set; }
        public bool IsPlaying => output?.PlaybackState == PlaybackState.Playing;

        private readonly Stopwatch playbackClock = new();
        private TimeSpan playbackOffset;

        public TimeSpan Position {
            get {
                TimeSpan elapsed = playbackOffset + playbackClock.Elapsed;
                TimeSpan duration = Duration;
                return elapsed < duration ? elapsed : duration;
            }
        }
        public TimeSpan Duration => reader?.TotalTime ?? TimeSpan.Zero;

        public event EventHandler? StateChanged;
        public event Action<string>? PlaybackError;

        #region Playback queue
        public List<Song> GetQueueSongs() {
            return playOrder.Select(index => queue[index]).ToList();
        }

        public void PlayQueueIndex(int index) {
            if (index < 0 || index >= playOrder.Count) return;
            orderPos = index;
            StartCurrentSong();
        }

        public void RemoveQueueIndex(int index) {
            if (index < 0 || index >= playOrder.Count) return;
            if (playOrder.Count == 1) {
                ClearQueue();
                return;
            }

            bool removingCurrent = index == orderPos;
            bool wasPlaying = IsPlaying;
            int songIndex = playOrder[index];

            playOrder.RemoveAt(index);
            queue.RemoveAt(songIndex);
            for (int i = 0; i < playOrder.Count; i++) {
                if (playOrder[i] > songIndex) playOrder[i]--;
            }

            if (removingCurrent) {
                orderPos = Math.Min(index, playOrder.Count - 1);
                StartCurrentSong(wasPlaying);
            }
            else {
                if (index < orderPos) orderPos--;
                NotifyStateChanged();
            }
        }

        public void AddToQueue(IEnumerable<Song> songs) {
            HashSet<Guid> existingIds = queue.Select(song => song.Id).ToHashSet();
            List<Song> added = songs.Where(song => existingIds.Add(song.Id)).ToList();

            if (added.Count == 0) return;
            foreach (Song song in added) {
                int index = queue.Count;
                queue.Add(song);
                playOrder.Add(index);
            }
            NotifyStateChanged();
        }

        public void InsertNext(IEnumerable<Song> songs) {
            HashSet<Guid> existingIds = queue.Select(song => song.Id).ToHashSet();
            List<Song> added = songs.Where(song => existingIds.Add(song.Id)).ToList();

            if (added.Count == 0) return;
            int insertPosition = orderPos + 1;

            foreach (Song song in added) {
                int index = queue.Count;
                queue.Add(song);
                playOrder.Insert(insertPosition++, index);
            }
            NotifyStateChanged();
        }

        public void ClearQueue() {
            StopAudio();
            orderPos = -1;
            playOrder.Clear();
            queue.Clear();
            NotifyStateChanged();
        }

        public Song? GetCurrentSong() {
            if (orderPos >= 0 && orderPos < playOrder.Count) return queue[playOrder[orderPos]];
            return null;
        }

        public void PlayQueue(IEnumerable<Song> songs, Song? startingSong = null) {
            List<Song> newQueue = songs.DistinctBy(song => song.Id).ToList();

            StopAudio();
            queue.Clear();
            queue.AddRange(newQueue);
            playOrder.Clear();

            if (queue.Count == 0) {
                orderPos = -1;
                NotifyStateChanged();
                return;
            }

            int startIndex = startingSong == null ? 0 : queue.FindIndex(s => s.Id == startingSong.Id);
            startIndex = Math.Max(0, startIndex);
            BuildPlayOrder(startIndex);
            StartCurrentSong();
        }
        #endregion

        private void BuildPlayOrder(int startIndex) {
            playOrder.Clear();
            playOrder.AddRange(Enumerable.Range(0, queue.Count));
            orderPos = startIndex;
        }

        private void ShuffleIndices(List<int> indices) {
            for (int i = indices.Count - 1; i > 0; i--) {
                int j = random.Next(i + 1);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }
        }

        #region Playback
        public void Play() {
            if (playOrder.Count == 0) return;
            if (orderPos < 0) {
                orderPos = 0;
                StartCurrentSong();
                return;
            }
            if (reader == null || output == null) {
                StartCurrentSong();
                return;
            }

            if (output.PlaybackState == PlaybackState.Stopped && Position >= Duration) {
                reader.Position = 0;
                playbackOffset = TimeSpan.Zero;
                playbackClock.Reset();
            }
            output.Play();
            playbackClock.Start();
            NotifyStateChanged();
        }

        public void Pause() {
            output?.Pause();
            playbackClock.Stop();
            NotifyStateChanged();
        }

        public void Next() => Advance(1, manual: true);

        public void Previous() => Advance(-1, manual: true);

        public void Seek(TimeSpan position) {
            if (reader == null) return;

            if (position < TimeSpan.Zero) position = TimeSpan.Zero;
            if (position > reader.TotalTime) position = reader.TotalTime;

            reader.CurrentTime = position;
            playbackOffset = reader.CurrentTime;
            playbackClock.Reset();
            if (IsPlaying) playbackClock.Start();
            NotifyStateChanged();
        }

        public void ToggleAutoReshuffle() {
            AutoReshuffle = !AutoReshuffle;
        }

        private void Advance(int direction, bool manual) {
            if (playOrder.Count == 0) return;
            if (orderPos < 0) {
                orderPos = 0;
                StartCurrentSong();
                return;
            }
            if (direction < 0 && orderPos == 0) {
                Seek(TimeSpan.Zero);
                return;
            }

            int nextPos = orderPos + direction;
            if (nextPos >= playOrder.Count) {
                if (Loop != LoopMode.All && !manual) {
                    StopAudio();
                    NotifyStateChanged();
                    return;
                }
                if (AutoReshuffle) ShuffleQueue();
                nextPos = 0;
            }
            orderPos = nextPos;
            StartCurrentSong();
        }
        #endregion

        #region Playback controls
        public void TogglePlayPause() {
            if (IsPlaying) Pause();
            else Play();
        }

        public void ShuffleQueue() {
            if (playOrder.Count < 2) return;
            int currentSongIndex = orderPos >= 0 ? playOrder[orderPos] : -1;

            List<int> shuffled = playOrder.ToList();
            if (currentSongIndex >= 0) shuffled.Remove(currentSongIndex);
            ShuffleIndices(shuffled);

            playOrder.Clear();
            if (currentSongIndex >= 0) playOrder.Add(currentSongIndex);
            playOrder.AddRange(shuffled);

            orderPos = currentSongIndex >= 0 ? 0 : -1;
            NotifyStateChanged();
        }

        public void CycleLoopMode() {
            Loop = Loop switch {
                LoopMode.Off => LoopMode.All,
                LoopMode.All => LoopMode.One,
                _ => LoopMode.Off
            };
            NotifyStateChanged();
        }
        #endregion

        private void StartCurrentSong(bool startPlaying = true) {
            StopAudio();

            Song? song = GetCurrentSong();
            if (song == null) {
                NotifyStateChanged();
                return;
            }
            try {
                reader = CreateReader(song.FilePath);
                output = new();

                volumeProvider = new(reader.ToSampleProvider()) {
                    Volume = (float)volume
                };
    
                output.Init(volumeProvider.ToWaveProvider());
                output.PlaybackStopped += Output_PlaybackStopped;
                if (startPlaying) {
                    output.Play();
                    playbackClock.Start();
                }
                NotifyStateChanged();
            }
            catch (Exception ex) {
                StopAudio();
                NotifyStateChanged();
                PlaybackError?.Invoke($"Could not play \"{song.Title}\":\n{ex.Message}");
            }
        }

        private void StopAudio() {
            playbackClock.Reset();
            playbackOffset = TimeSpan.Zero;
            WaveOut? previousOutput = output;
            WaveStream? previousReader = reader;
            output = null;
            reader = null;
            volumeProvider = null;

            if (previousOutput == null) {
                previousReader?.Dispose();
                return;
            }
            previousOutput.PlaybackStopped -= Output_PlaybackStopped;
            if (previousOutput.PlaybackState == PlaybackState.Stopped) {
                previousOutput.Dispose();
                previousReader?.Dispose();
                return;
            }
            EventHandler<StoppedEventArgs>? cleanup = null;
            cleanup = (_, _) => {
                previousOutput.PlaybackStopped -= cleanup;
                previousOutput.Dispose();
                previousReader?.Dispose();
            };
            previousOutput.PlaybackStopped += cleanup;
            previousOutput.Stop();
        }

        private void Output_PlaybackStopped(object? sender, StoppedEventArgs e) {
            Application.Current.Dispatcher.BeginInvoke(() => {
                if (!ReferenceEquals(sender, output)) return;
                if (e.Exception != null) {
                    string message = e.Exception.Message;
                    StopAudio();
                    NotifyStateChanged();
                    PlaybackError?.Invoke(message);
                    return;
                }
                if (reader == null) return;
                if (Loop == LoopMode.One) {
                    StartCurrentSong();
                    return;
                }
                Advance(1, manual: false);
            });
        }

        private WaveStream CreateReader(string path) {
            return Path.GetExtension(path).ToLowerInvariant() switch {
                ".ogg" => new VorbisWaveReader(path),
                ".mp3" or ".flac" or ".wav" => new MediaFoundationReader(path),
                _ => throw new NotSupportedException($"Unsupported audio format: {Path.GetExtension(path)}")
            };
        }

        private void NotifyStateChanged() {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose() => StopAudio();
    }
}
