using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using zPlayer.Core;

namespace zPlayer.App.Views
{
    public sealed class MixChoice
    {
        public Mix Mix { get; }
        public string Title => Mix.Title;
        public bool IsChecked { get; set; }
        public int MissingCount { get; }

        public string StatusText => MissingCount == 0 ? "Contains all selected songs" : $"{MissingCount} song(s) will be added";

        public MixChoice(Mix mix, IReadOnlyCollection<Guid> songIds) {
            Mix = mix;
            HashSet<Guid> existingIds = mix.Entries.Select(entry => entry.SongId).ToHashSet();
            MissingCount = songIds.Count(id => !existingIds.Contains(id));
        }
    }

    public partial class AddToMixView : UserControl
    {
        private readonly List<MixChoice> choices;

        public event Action? CloseRequested;
        public event Action<Mix[]>? AddRequested;

        public AddToMixView(IEnumerable<Mix> mixes, IEnumerable<Guid> songIds) {
            Guid[] selectedIds = songIds.Distinct().ToArray();

            choices = mixes.OrderBy(mix => mix.Title, StringComparer.OrdinalIgnoreCase)
                .Select(mix => new MixChoice(mix, selectedIds)).ToList();

            InitializeComponent();
            MixChoicesList.ItemsSource = choices;
        }

        private void Done_Click(object sender, RoutedEventArgs e) {
            Mix[] selectedMixes = choices.Where(choice => choice.IsChecked).Select(choice => choice.Mix).ToArray();

            if (selectedMixes.Length == 0) {
                CloseRequested?.Invoke();
                return;
            }
            AddRequested?.Invoke(selectedMixes);
        }
    }
}