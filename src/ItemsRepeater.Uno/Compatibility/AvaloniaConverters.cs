using System;
using System.Collections.Generic;
using System.Globalization;

namespace Avalonia.Data.Converters
{
    public interface IValueConverter
    {
        object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture);
        object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture);
    }

    public interface IMultiValueConverter
    {
        object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture);
        object? ConvertBack(IList<object?> values, Type targetType, object? parameter, CultureInfo culture);
    }
}
