using System.Windows.Data;
using System.Globalization;

namespace zPlayer.App.Converters
{
    public class FillWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
            if (values.Length < 4 ||
                    values[0] is not double value ||
                    values[1] is not double minimum ||
                    values[2] is not double maximum ||
                    values[3] is not double width) {
                return 0.0;
            }
            if (maximum <= minimum || width <= 0) return 0.0;

            double normalized = (value - minimum) / (maximum - minimum);
            normalized = Math.Clamp(normalized, 0.0, 1.0);
            return normalized * width;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
