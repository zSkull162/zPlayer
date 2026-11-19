using System.Windows;
using System.Windows.Media;
using zPlayer.Core;

namespace zPlayer.App.Themes
{
    public static class ThemeManager
    {
        private static readonly string[] ColorKeys = [
            "BackgroundColor",
            "SurfaceColor",
            "BorderColor",
            "HighlightColor",
            "SelectedColor",
            "SelectedColorSecondary",
            "TextColor"
        ];

        private static readonly string[] BrushKeys = [
            "BackgroundBrush",
            "SurfaceBrush",
            "BorderBrush",
            "HighlightBrush",
            "SelectedBrush",
            "SelectedBrushSecondary",
            "TextBrush"
        ];

        public static void Apply(ThemeSettings settings) {
            string[] values = [
                settings.Background,
                settings.Surface,
                settings.Border,
                settings.Highlight,
                settings.Selected,
                settings.SelectedSecondary,
                settings.Text
            ];

            Color[] colors = values.Select(value => (Color)ColorConverter.ConvertFromString(value)).ToArray();
            for (int i = 0; i < ColorKeys.Length; i++) {
                SetColor(ColorKeys[i], colors[i]);
            }
        }

        public static void SetColor(string colorKey, Color color) {
            int index = Array.IndexOf(ColorKeys, colorKey);
            if (index < 0) throw new ArgumentException("Unknown theme color.", nameof(colorKey));

            SolidColorBrush brush = new(color);
            brush.Freeze();

            Application.Current.Resources[colorKey] = color;
            Application.Current.Resources[BrushKeys[index]] = brush;
        }

        public static ThemeSettings Capture() {
            string Read(string key) => ((Color)Application.Current.FindResource(key)).ToString();

            return new ThemeSettings(
                Read("BackgroundColor"),
                Read("SurfaceColor"),
                Read("BorderColor"),
                Read("HighlightColor"),
                Read("SelectedColor"),
                Read("SelectedColorSecondary"),
                Read("TextColor"));
        }
    }
}