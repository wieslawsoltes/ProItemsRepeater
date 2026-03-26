using System;
using System.Diagnostics;
#if NET6_0_OR_GREATER
using System.Diagnostics.Metrics;
#endif
using Avalonia;

namespace Avalonia.Controls
{
    internal static class ItemsRepeaterDiagnostics
    {
        internal const string ActivitySourceName = "Avalonia.ItemsRepeater";
        internal const string MeterName = "Avalonia.ItemsRepeater";

#if NET6_0_OR_GREATER
        private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
        private static readonly Meter Meter = new(MeterName);
        private static readonly Counter<long> MeasureCount = Meter.CreateCounter<long>("itemsrepeater.measure.count");
        private static readonly Histogram<double> MeasureDurationMs = Meter.CreateHistogram<double>("itemsrepeater.measure.duration.ms");
        private static readonly Counter<long> ArrangeCount = Meter.CreateCounter<long>("itemsrepeater.arrange.count");
        private static readonly Histogram<double> ArrangeDurationMs = Meter.CreateHistogram<double>("itemsrepeater.arrange.duration.ms");
        private static readonly Counter<long> GenerateCount = Meter.CreateCounter<long>("itemsrepeater.generate.pass");
        private static readonly Histogram<double> GenerateDurationMs = Meter.CreateHistogram<double>("itemsrepeater.generate.duration.ms");
        private static readonly Histogram<long> GenerateMeasuredCount = Meter.CreateHistogram<long>("itemsrepeater.generate.measured.count");
        private static readonly Counter<long> ElementRealized = Meter.CreateCounter<long>("itemsrepeater.element.realized");
        private static readonly Counter<long> ElementRecycled = Meter.CreateCounter<long>("itemsrepeater.element.recycled");
        private static readonly Counter<long> MakeAnchor = Meter.CreateCounter<long>("itemsrepeater.anchor.make");
#endif

        internal static long GetTimestamp() => Stopwatch.GetTimestamp();

        internal static double GetElapsedMilliseconds(long startTimestamp) =>
            (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;

        internal static IDisposable? StartMeasure(string? layoutId, int itemCount, Size availableSize, Rect realizationRect, Rect visibleRect)
        {
#if NET6_0_OR_GREATER
            if (!ActivitySource.HasListeners())
            {
                return null;
            }

            var activity = ActivitySource.StartActivity("ItemsRepeater.Measure", ActivityKind.Internal);
            if (activity is not null)
            {
                activity.SetTag("layout.id", layoutId ?? string.Empty);
                activity.SetTag("items.count", itemCount);
                activity.SetTag("available.width", availableSize.Width);
                activity.SetTag("available.height", availableSize.Height);
                activity.SetTag("realization.x", realizationRect.X);
                activity.SetTag("realization.y", realizationRect.Y);
                activity.SetTag("realization.width", realizationRect.Width);
                activity.SetTag("realization.height", realizationRect.Height);
                activity.SetTag("visible.x", visibleRect.X);
                activity.SetTag("visible.y", visibleRect.Y);
                activity.SetTag("visible.width", visibleRect.Width);
                activity.SetTag("visible.height", visibleRect.Height);
            }

            return activity;
#else
            return null;
#endif
        }

        internal static IDisposable? StartArrange(string? layoutId, int itemCount, Size finalSize)
        {
#if NET6_0_OR_GREATER
            if (!ActivitySource.HasListeners())
            {
                return null;
            }

            var activity = ActivitySource.StartActivity("ItemsRepeater.Arrange", ActivityKind.Internal);
            if (activity is not null)
            {
                activity.SetTag("layout.id", layoutId ?? string.Empty);
                activity.SetTag("items.count", itemCount);
                activity.SetTag("final.width", finalSize.Width);
                activity.SetTag("final.height", finalSize.Height);
            }

            return activity;
#else
            return null;
#endif
        }

        internal static IDisposable? StartGenerate(string? layoutId, string direction, bool isWrapping, int anchorIndex)
        {
#if NET6_0_OR_GREATER
            if (!ActivitySource.HasListeners())
            {
                return null;
            }

            var activity = ActivitySource.StartActivity("ItemsRepeater.Generate", ActivityKind.Internal);
            if (activity is not null)
            {
                activity.SetTag("layout.id", layoutId ?? string.Empty);
                activity.SetTag("direction", direction);
                activity.SetTag("wrapping", isWrapping);
                activity.SetTag("anchor.index", anchorIndex);
            }

            return activity;
#else
            return null;
#endif
        }

        internal static void RecordMeasure(double durationMs, string? layoutId, int itemCount, int realizedCount)
        {
#if NET6_0_OR_GREATER
            var tags = new TagList
            {
                { "layout.id", layoutId ?? string.Empty },
                { "items.count", itemCount },
                { "realized.count", realizedCount }
            };
            MeasureCount.Add(1, tags);
            MeasureDurationMs.Record(durationMs, tags);
#endif
        }

        internal static void RecordArrange(double durationMs, string? layoutId, int itemCount, int realizedCount)
        {
#if NET6_0_OR_GREATER
            var tags = new TagList
            {
                { "layout.id", layoutId ?? string.Empty },
                { "items.count", itemCount },
                { "realized.count", realizedCount }
            };
            ArrangeCount.Add(1, tags);
            ArrangeDurationMs.Record(durationMs, tags);
#endif
        }

        internal static void RecordGenerate(double durationMs, string? layoutId, string direction, int measuredCount, int realizedCount, bool isWrapping, bool disableVirtualization)
        {
#if NET6_0_OR_GREATER
            var tags = new TagList
            {
                { "layout.id", layoutId ?? string.Empty },
                { "direction", direction },
                { "wrapping", isWrapping },
                { "disable.virtualization", disableVirtualization },
                { "realized.count", realizedCount }
            };
            GenerateCount.Add(1, tags);
            GenerateDurationMs.Record(durationMs, tags);
            GenerateMeasuredCount.Record(measuredCount, tags);
#endif
        }

        internal static void RecordElementRealized(string? layoutId, int count)
        {
#if NET6_0_OR_GREATER
            var tags = new TagList
            {
                { "layout.id", layoutId ?? string.Empty }
            };
            ElementRealized.Add(count, tags);
#endif
        }

        internal static void RecordElementRecycled(int count)
        {
#if NET6_0_OR_GREATER
            ElementRecycled.Add(count);
#endif
        }

        internal static void RecordMakeAnchor(string? layoutId, int anchorIndex)
        {
#if NET6_0_OR_GREATER
            var tags = new TagList
            {
                { "layout.id", layoutId ?? string.Empty },
                { "anchor.index", anchorIndex }
            };
            MakeAnchor.Add(1, tags);
#endif
        }
    }
}
