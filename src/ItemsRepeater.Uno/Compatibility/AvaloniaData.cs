using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Avalonia.Data
{
    public enum BindingMode
    {
        Default,
        OneWay,
        TwoWay,
        OneTime,
        OneWayToSource,
    }

    public enum BindingPriority
    {
        LocalValue = 0,
        Style = 1,
    }

    public abstract class BindingBase
    {
        internal object? Evaluate(object? dataContext) => EvaluateCore(dataContext);

        internal abstract Microsoft.UI.Xaml.Data.BindingBase CreateNativeBinding();

        protected abstract object? EvaluateCore(object? dataContext);
    }

    public sealed class Binding : BindingBase
    {
        public Binding()
        {
        }

        public Binding(string? path)
        {
            Path = path;
        }

        public string? Path { get; set; }
        public Avalonia.Data.Converters.IValueConverter? Converter { get; set; }
        public object? ConverterParameter { get; set; }
        public object? FallbackValue { get; set; }
        public object? TargetNullValue { get; set; }
        public BindingMode Mode { get; set; } = BindingMode.Default;

        internal override Microsoft.UI.Xaml.Data.BindingBase CreateNativeBinding()
        {
            return new Microsoft.UI.Xaml.Data.Binding
            {
                Path = string.IsNullOrWhiteSpace(Path) ? null : new PropertyPath(Path),
                Converter = Converter is null ? null : new NativeValueConverterAdapter(Converter),
                ConverterParameter = ConverterParameter,
                FallbackValue = FallbackValue,
                TargetNullValue = TargetNullValue,
                Mode = Mode switch
                {
                    BindingMode.OneWay => Microsoft.UI.Xaml.Data.BindingMode.OneWay,
                    BindingMode.TwoWay => Microsoft.UI.Xaml.Data.BindingMode.TwoWay,
                    BindingMode.OneTime => Microsoft.UI.Xaml.Data.BindingMode.OneTime,
                    BindingMode.OneWayToSource => Microsoft.UI.Xaml.Data.BindingMode.OneWay,
                    _ => Microsoft.UI.Xaml.Data.BindingMode.OneWay,
                },
            };
        }

        protected override object? EvaluateCore(object? dataContext)
        {
            if (dataContext is null || string.IsNullOrWhiteSpace(Path))
                return dataContext;

            return Avalonia.Controls.DataGrid.RepeaterDataGridBindingHelper.GetPropertyValue(dataContext, Path);
        }

        private sealed class NativeValueConverterAdapter : Microsoft.UI.Xaml.Data.IValueConverter
        {
            private readonly Avalonia.Data.Converters.IValueConverter _inner;

            public NativeValueConverterAdapter(Avalonia.Data.Converters.IValueConverter inner)
            {
                _inner = inner;
            }

            public object? Convert(object value, Type targetType, object parameter, string language)
            {
                var culture = string.IsNullOrWhiteSpace(language)
                    ? CultureInfo.InvariantCulture
                    : CultureInfo.GetCultureInfo(language);
                return _inner.Convert(value, targetType, parameter, culture);
            }

            public object? ConvertBack(object value, Type targetType, object parameter, string language)
            {
                var culture = string.IsNullOrWhiteSpace(language)
                    ? CultureInfo.InvariantCulture
                    : CultureInfo.GetCultureInfo(language);
                return _inner.ConvertBack(value, targetType, parameter, culture);
            }
        }
    }
}
