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
        public static ILogSink? TryGet(LogEventLevel level, string area)
        {
            _ = level;
            _ = area;
            return null;
        }
    }
}
