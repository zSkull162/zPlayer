using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using zPlayer.Core;
using zPlayer.App.Commands;

namespace zPlayer.App.Views
{
    public sealed class CreateMixTile { }

    public partial class MixesView : UserControl
    {
        private const int PageSize = 40;
        private int page;
        private readonly MusicLibrary library;
        private readonly SortState sortState;
        private bool initializing = true;

        public MixCommands Commands { get; }
        public Mix? SingleSelectedMix { get; private set; }
        public event Action? CreateMixRequested;
        public event Action<Mix>? MixOpened;

        public MixesView(MusicLibrary library, MixCommands commands, SortState sortState) {
            this.library = library;
            this.sortState = sortState;
            Commands = commands;

            InitializeComponent();

            SortTypeBox.SelectedIndex = sortState.TypeIndex;
            SortOrderBox.SelectedIndex = sortState.OrderIndex;
            if (!string.IsNullOrWhiteSpace(sortState.FilterString)) MixSearchBox.Text = sortState.FilterString;

            DataContext = library;
            initializing = false;
            RefreshMixes();
        }

        private void CreateMix_Click(object sender, RoutedEventArgs e) {
            CreateMixRequested?.Invoke();
        }

        #region Filtering and sorting
        public void RefreshMixes() {
            library.RefreshMixes();

            HashSet<Guid> selectedIds = MixesGrid.SelectedItems.OfType<Mix>().Select(mix => mix.Id).ToHashSet();
            string search = MixSearchBox.Text.Trim();
            bool descending = SortOrderBox.SelectedIndex == 1;
            bool numeric = SortTypeBox.SelectedIndex != 0;

            ((ComboBoxItem)SortOrderBox.Items[0]).Content = numeric ? "Low-High" : "A-Z";
            ((ComboBoxItem)SortOrderBox.Items[1]).Content = numeric ? "High-Low" : "Z-A";

            IEnumerable<Mix> filtered = library.Mixes.Where(mix =>
                string.IsNullOrWhiteSpace(search) || mix.Title.Contains(search, StringComparison.OrdinalIgnoreCase));

            IOrderedEnumerable<Mix> OrderBy<T>(Func<Mix, T> selector, IComparer<T>? comparer = null) {
                return descending ? filtered.OrderByDescending(selector, comparer) : filtered.OrderBy(selector, comparer);
            }
            IOrderedEnumerable<Mix> sorted = SortTypeBox.SelectedIndex switch {
                1 => OrderBy(mix => mix.SongCount),
                2 => OrderBy(mix => mix.Duration),
                _ => OrderBy(mix => mix.Title, StringComparer.OrdinalIgnoreCase)
            };

            List<object> items = [new CreateMixTile()];
            var ordered = sorted.ThenBy(mix => mix.Title, StringComparer.OrdinalIgnoreCase).ToList();
            int pageCount = Math.Max(1, (ordered.Count + PageSize - 1) / PageSize);
            page = Math.Clamp(page, 0, pageCount - 1);
            items.AddRange(ordered.Skip(page * PageSize).Take(PageSize));

            PageText.Text = $"Page {page + 1} of {pageCount}";
            PageControls.Visibility = pageCount > 1 ? Visibility.Visible : Visibility.Collapsed;
            PreviousPageButton.IsEnabled = page > 0;
            NextPageButton.IsEnabled = page + 1 < pageCount;

            MixesGrid.ItemsSource = items;
            foreach (Mix mix in items.OfType<Mix>()) {
                if (selectedIds.Contains(mix.Id)) MixesGrid.SelectedItems.Add(mix);
            }
            MixesInLibraryText.Text = $"— {library.Mixes.Count} in library";
        }

        private void PreviousPage_Click(object sender, RoutedEventArgs e) {
            page--;
            RefreshMixes();
        }

        private void NextPage_Click(object sender, RoutedEventArgs e) {
            page++;
            RefreshMixes();
        }

        private void SortBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (initializing) return;

            sortState.TypeIndex = SortTypeBox.SelectedIndex;
            sortState.OrderIndex = SortOrderBox.SelectedIndex;
            page = 0;
            RefreshMixes();
        }

        private void MixSearchBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (initializing) return;
            sortState.FilterString = MixSearchBox.Text;
            page = 0;
            RefreshMixes();
        }
        #endregion

        #region MixesGrid event handlers
        private void MixesGrid_LeftMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.OriginalSource is DependencyObject source &&
                ItemsControl.ContainerFromElement(MixesGrid, source) is ListBoxItem) {
                return;
            }
            MixesGrid.UnselectAll();
        }

        private void MixesGrid_RightMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.OriginalSource is DependencyObject source &&
                    ItemsControl.ContainerFromElement(MixesGrid, source) is ListBoxItem { DataContext: Mix mix }) {
                if (!MixesGrid.SelectedItems.Contains(mix)) {
                    MixesGrid.UnselectAll();
                    MixesGrid.SelectedItem = mix;
                }
                e.Handled = true;
            }
            else {
                MixesGrid.UnselectAll();
                e.Handled = true;
            }
        }

        private void MixesGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e) {
            Mix[] selected = MixesGrid.SelectedItems.OfType<Mix>().ToArray();
            SingleSelectedMix = selected.Length == 1 ? selected[0] : null;

            if (MixesGrid.ContextMenu is ContextMenu menu) {
                menu.DataContext = null;
                menu.DataContext = MixesGrid;
            }
            if (selected.Length == 0) e.Handled = true;
            CommandManager.InvalidateRequerySuggested();
        }

        private void MixesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
            if (e.OriginalSource is not DependencyObject source) return;
            if (ItemsControl.ContainerFromElement(MixesGrid, source) is ListBoxItem { DataContext: Mix mix }) {
                MixOpened?.Invoke(mix);
            }
        }
        #endregion
    }
}
