using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using CraftStation.Core.Models;

namespace CraftStation.Converters;

public sealed class SeverityToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            HealthSeverity.Error => new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)),
            HealthSeverity.Warning => new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06)),
            _ => new SolidColorBrush(Color.FromRgb(0x4C, 0xC9, 0xF0))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
