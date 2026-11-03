using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace zPlayer.App.Converters
{
    public class ArtworkConverter : IValueConverter
    {
        private static readonly BitmapImage defaultArtwork = CreateDefaultArtwork();

        private static BitmapImage CreateDefaultArtwork() {
            BitmapImage image = new(new Uri("pack://application:,,,/Assets/Icons/PlaceholderCover.png"));
            image.Freeze();
            return image;
        }

        public BitmapImage BmpFromBytes(byte[] bytes) {
            ArgumentNullException.ThrowIfNull(bytes);

            using MemoryStream stream = new(bytes);
            BitmapImage image = new();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is not byte[] bytes || bytes.Length == 0) return defaultArtwork;
            try { return BmpFromBytes(bytes); }
            catch { return null; }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}