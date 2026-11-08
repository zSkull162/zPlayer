using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace zPlayer.App.Converters
{
    public class ArtworkConverter : IValueConverter
    {
        private static readonly ConditionalWeakTable<byte[], Dictionary<int, BitmapImage>> cache = new();
        public static readonly BitmapImage DefaultArtwork = CreateDefaultArtwork();

        private static BitmapImage CreateDefaultArtwork() {
            BitmapImage image = new();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 384;
            image.UriSource = new Uri("pack://application:,,,/Assets/Icons/PlaceholderCover.png");
            image.EndInit();
            image.Freeze();
            return image;
        }

        public BitmapImage BmpFromBytes(byte[] bytes, int decodePixelWidth = 0) {
            ArgumentNullException.ThrowIfNull(bytes);

            using MemoryStream stream = new(bytes);
            BitmapImage image = new();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;

            if (decodePixelWidth > 0) image.DecodePixelWidth = decodePixelWidth;

            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is not byte[] bytes || bytes.Length == 0) return DefaultArtwork;
            int size = int.TryParse(parameter?.ToString(), out int requestedSize)
                ? Math.Clamp(requestedSize, 16, 2048) : 384;

            try {
                Dictionary<int, BitmapImage> sizes = cache.GetValue(bytes, _ => new Dictionary<int, BitmapImage>());
                lock (sizes) {
                    if (!sizes.TryGetValue(size, out BitmapImage? image)) {
                        image = BmpFromBytes(bytes, size);
                        sizes.Add(size, image);
                    }
                    return image;
                }
            }
            catch {
                return DefaultArtwork;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}