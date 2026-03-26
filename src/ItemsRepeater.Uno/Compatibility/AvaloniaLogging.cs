using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Avalonia.Logging
{
    public enum LogEventLevel
    {
        Verbose = 0,
    }

    public interface ILogSink
    {
        void Log(object source, string messageTemplate, params object?[] propertyValues);
    }

    public static class Logger
    {
        private sealed class TraceLogSink : ILogSink
        {
            private static readonly Regex PlaceholderRegex = new(@"\{[^{}]+\}", RegexOptions.Compiled);
            public static TraceLogSink Instance { get; } = new();

            public void Log(object source, string messageTemplate, params object?[] propertyValues)
            {
                var rendered = Render(messageTemplate, propertyValues);
                var message = $"[{source.GetType().Name}] {rendered}";
                Debug.WriteLine(message);
                Trace.WriteLine(message);
            }

            private static string Render(string messageTemplate, object?[] propertyValues)
            {
                if (propertyValues.Length == 0 || string.IsNullOrEmpty(messageTemplate))
                {
                    return messageTemplate;
                }

                var index = 0;
                return PlaceholderRegex.Replace(
                    messageTemplate,
                    _ => FormatValue(index < propertyValues.Length ? propertyValues[index++] : null));
            }

            private static string FormatValue(object? value)
            {
                if (value is null)
                {
                    return string.Empty;
                }

                if (value is Array array)
                {
                    var builder = new StringBuilder();
                    builder.Append('[');
                    for (var i = 0; i < array.Length; ++i)
                    {
                        if (i > 0)
                        {
                            builder.Append(", ");
                        }

                        builder.Append(array.GetValue(i));
                    }

                    builder.Append(']');
                    return builder.ToString();
                }

                return value.ToString() ?? string.Empty;
            }
        }

        public static ILogSink? TryGet(LogEventLevel level, string area)
        {
            _ = level;
            _ = area;
            return TraceLogSink.Instance;
        }
    }
}
