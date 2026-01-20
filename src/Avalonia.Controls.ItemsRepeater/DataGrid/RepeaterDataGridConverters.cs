using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using Avalonia;
using Avalonia.Controls.Templates;
using Avalonia.Data.Converters;

namespace Avalonia.Controls.DataGrid;

public sealed class RepeaterDataGridCellTextConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var item = values.Count > 0 ? values[0] : null;
        var path = values.Count > 1 ? values[1] as string : null;

        if (item is null || item == AvaloniaProperty.UnsetValue)
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return FormatCellValue(item, culture);
        }

        var value = RepeaterDataGridBindingHelper.GetPropertyValue(item, path);
        if (value is null || value == AvaloniaProperty.UnsetValue)
        {
            return string.Empty;
        }

        return FormatCellValue(value, culture);
    }

    public object? ConvertBack(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static string FormatCellValue(object? value, CultureInfo culture)
    {
        if (value is null || value == AvaloniaProperty.UnsetValue)
        {
            return string.Empty;
        }

        if (value is string text)
        {
            return text;
        }

        if (value is IFormattable formattable)
        {
            return formattable.ToString(null, culture);
        }

        if (value is IConvertible convertible)
        {
            return convertible.ToString(culture) ?? string.Empty;
        }

        return string.Empty;
    }
}

public sealed class RepeaterDataGridRowHeightConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var item = values.Count > 0 ? values[0] : null;
        var path = values.Count > 1 ? values[1] as string : null;

        if (item is null || item == AvaloniaProperty.UnsetValue || string.IsNullOrWhiteSpace(path))
        {
            return double.NaN;
        }

        var value = RepeaterDataGridBindingHelper.GetPropertyValue(item, path);
        if (value is null || value == AvaloniaProperty.UnsetValue)
        {
            return double.NaN;
        }

        try
        {
            return ConvertToDouble(value, culture);
        }
        catch (Exception)
        {
            return double.NaN;
        }
    }

    public object? ConvertBack(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static double ConvertToDouble(object value, CultureInfo culture)
    {
        return value switch
        {
            double doubleValue => doubleValue,
            float floatValue => floatValue,
            decimal decimalValue => (double)decimalValue,
            _ => System.Convert.ToDouble(value, culture)
        };
    }
}

public sealed class IsNullConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is IDataTemplate)
        {
            return false;
        }

        return true;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public sealed class IsNotNullConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is IDataTemplate;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public sealed class RepeaterDataGridCellContentConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var template = values.Count > 0 ? values[0] : null;
        if (template is not IDataTemplate)
        {
            return null;
        }

        return values.Count > 1 ? values[1] : null;
    }

    public object? ConvertBack(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

internal static class RepeaterDataGridBindingHelper
{
    private readonly record struct AccessorKey(Type Type, string Path);

    private static readonly ConcurrentDictionary<AccessorKey, Func<object, object?>> AccessorCache = new();

    public static object? GetPropertyValue(object item, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return item;
        }

        if (item is IDictionary)
        {
            return GetPropertyValueSlow(item, path);
        }

        var type = item.GetType();
        var accessor = AccessorCache.GetOrAdd(new AccessorKey(type, path), key => BuildAccessor(key.Type, key.Path));
        return accessor(item);
    }

#if NET6_0_OR_GREATER
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Reflection used for dynamic column binding.")]
#endif
    private static Func<object, object?> BuildAccessor(Type type, string path)
    {
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var accessors = new List<Func<object, object?>>();

        foreach (var segment in segments)
        {
            if (typeof(IDictionary).IsAssignableFrom(type))
            {
                return item => GetPropertyValueSlow(item, path);
            }

            var property = type.GetProperty(segment, BindingFlags.Instance | BindingFlags.Public);
            if (property is not null)
            {
                accessors.Add(current => property.GetValue(current));
                type = property.PropertyType;
                continue;
            }

            var field = type.GetField(segment, BindingFlags.Instance | BindingFlags.Public);
            if (field is not null)
            {
                accessors.Add(current => field.GetValue(current));
                type = field.FieldType;
                continue;
            }

            return _ => null;
        }

        return item =>
        {
            object? current = item;
            foreach (var accessor in accessors)
            {
                if (current is null)
                {
                    return null;
                }

                current = accessor(current);
            }

            return current;
        };
    }

#if NET6_0_OR_GREATER
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Reflection used for dynamic column binding.")]
#endif
    private static object? GetPropertyValueSlow(object item, string path)
    {
        var current = item;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current is null)
            {
                return null;
            }

            if (current is IDictionary dictionary)
            {
                if (!dictionary.Contains(segment))
                {
                    return null;
                }

                current = dictionary[segment];
                continue;
            }

            var type = current.GetType();
            var property = type.GetProperty(segment, BindingFlags.Instance | BindingFlags.Public);
            if (property is not null)
            {
                current = property.GetValue(current);
                continue;
            }

            var field = type.GetField(segment, BindingFlags.Instance | BindingFlags.Public);
            if (field is not null)
            {
                current = field.GetValue(current);
                continue;
            }

            return null;
        }

        return current;
    }
}
