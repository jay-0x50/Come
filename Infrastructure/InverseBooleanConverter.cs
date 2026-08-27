using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Come.Infrastructure;

public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var result = value is bool boolean && !boolean;
        return targetType == typeof(Visibility)
            ? result ? Visibility.Visible : Visibility.Collapsed
            : result;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
