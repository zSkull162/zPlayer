using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using zPlayer.App.Artwork;
using zPlayer.Core;

namespace zPlayer.App.Views
{
    internal sealed class SongDragFeedback : IDisposable
    {
        private readonly DataGrid grid;
        private readonly Song song;
        private readonly Popup preview;
        private readonly AdornerLayer? layer;
        private readonly InsertionLine line;
        private DataGridRow? dimmedRow;

        public SongDragFeedback(DataGrid grid, Song song) {
            this.grid = grid;
            this.song = song;

            preview = new Popup {
                PlacementTarget = grid,
                Placement = PlacementMode.Relative,
                AllowsTransparency = true,
                StaysOpen = true,
                IsHitTestVisible = false,
                Child = CreatePreview(song)
            };

            line = new InsertionLine(grid);
            layer = AdornerLayer.GetAdornerLayer(grid);
            layer?.Add(line);

            grid.LoadingRow += Grid_LoadingRow;
            grid.UnloadingRow += Grid_UnloadingRow;

            if (grid.ItemContainerGenerator.ContainerFromItem(song) is DataGridRow row) Dim(row);
        }

        public static bool TryGetSlot(DataGrid grid, DragEventArgs e, out int index, out double lineY) {
            index = -1;
            lineY = 0;

            if (e.OriginalSource is not DependencyObject source ||
                ItemsControl.ContainerFromElement(grid, source) is not DataGridRow row ||
                row.Item is not Song) {
                return false;
            }
            int rowIndex = row.GetIndex();
            if (rowIndex < 0) return false;

            bool insertAfter = e.GetPosition(row).Y >= row.ActualHeight / 2;
            index = rowIndex + (insertAfter ? 1 : 0);
            lineY = row.TranslatePoint(new Point(0, insertAfter ? row.ActualHeight : 0), grid).Y;
            return true;
        }

        public void Show(DragEventArgs e, double lineY) {
            Point pointer = e.GetPosition(grid);
            preview.HorizontalOffset = pointer.X + 16;
            preview.VerticalOffset = pointer.Y + 16;
            preview.IsOpen = true;

            line.Y = lineY;
            line.InvalidateVisual();
        }

        public void ClearLine() {
            line.Y = null;
            line.InvalidateVisual();
        }

        public void Hide() {
            preview.IsOpen = false;
            ClearLine();
        }

        private void Grid_LoadingRow(object? sender, DataGridRowEventArgs e) {
            if (ReferenceEquals(e.Row.Item, song)) Dim(e.Row);
        }

        private void Grid_UnloadingRow(object? sender, DataGridRowEventArgs e) {
            if (ReferenceEquals(e.Row, dimmedRow)) RestoreRow();
        }

        private void Dim(DataGridRow row) {
            RestoreRow();
            dimmedRow = row;
            row.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(row.Opacity * 0.5, new Duration(TimeSpan.Zero)));
        }

        private void RestoreRow() {
            dimmedRow?.BeginAnimation(UIElement.OpacityProperty, null);
            dimmedRow = null;
        }

        public void Dispose() {
            Hide();
            RestoreRow();
            grid.LoadingRow -= Grid_LoadingRow;
            grid.UnloadingRow -= Grid_UnloadingRow;
            layer?.Remove(line);
        }

        private static Border CreatePreview(Song song) {
            Border art = new() {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(0, 0, 8, 0)
            };
            CoverArt.SetSmall(art, true);
            CoverArt.SetReference(art, song.Cover);
            CoverArt.SetEnabled(art, true);

            TextBlock title = new() {
                Text = song.Title,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            TextBlock artist = new() {
                Text = song.Artist,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            title.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            artist.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");

            StackPanel labels = new() { VerticalAlignment = VerticalAlignment.Center };
            labels.Children.Add(title);
            labels.Children.Add(artist);

            DockPanel content = new();
            DockPanel.SetDock(art, Dock.Left);
            content.Children.Add(art);
            content.Children.Add(labels);

            Border card = new() {
                Width = 260,
                Padding = new Thickness(6),
                CornerRadius = new CornerRadius(4),
                Opacity = 0.5,
                IsHitTestVisible = false,
                Child = content
            };
            card.SetResourceReference(Border.BackgroundProperty, "BackgroundBrush");
            return card;
        }

        private sealed class InsertionLine : Adorner
        {
            public double? Y { get; set; }

            public InsertionLine(DataGrid grid) : base(grid) {
                IsHitTestVisible = false;
                ClipToBounds = true;
            }

            protected override void OnRender(DrawingContext drawingContext) {
                if (Y is not double y || y < 0 || y > ActualHeight) return;
                Brush brush = (AdornedElement as FrameworkElement)?.TryFindResource("SelectedBrush") as Brush ?? Brushes.DeepSkyBlue;
                drawingContext.DrawLine(new Pen(brush, 2), new Point(2, y), new Point(Math.Max(2, ActualWidth - 2), y));
            }
        }
    }
}