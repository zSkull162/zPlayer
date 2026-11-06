using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using zPlayer.Core;

namespace zPlayer.App.Views
{
    public sealed class CreateMixTile { }

    public partial class MixesView : UserControl
    {
        public event Action<Mix>? MixOpened;

        public MixesView() {
            InitializeComponent();
        }

        #region Filtering and sorting
        private void SortBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {

        }

        private void MixSearchBox_TextChanged(object sender, TextChangedEventArgs e) {

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
            if (!MixesGrid.SelectedItems.OfType<Mix>().Any()) e.Handled = true;
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
