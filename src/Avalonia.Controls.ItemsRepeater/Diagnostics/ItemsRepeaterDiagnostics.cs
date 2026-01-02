// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
        private static readonly ActivitySource s_activitySource = new ActivitySource(ActivitySourceName);
        private static readonly Meter s_meter = new Meter(MeterName);

        private static readonly Counter<long> s_measureCount =
            s_meter.CreateCounter<long>("itemsrepeater.measure.count");
        private static readonly Histogram<double> s_measureDurationMs =
            s_meter.CreateHistogram<double>("itemsrepeater.measure.duration.ms");
        private static readonly Counter<long> s_arrangeCount =
            s_meter.CreateCounter<long>("itemsrepeater.arrange.count");
        private static readonly Histogram<double> s_arrangeDurationMs =
            s_meter.CreateHistogram<double>("itemsrepeater.arrange.duration.ms");
        private static readonly Counter<long> s_generateCount =
            s_meter.CreateCounter<long>("itemsrepeater.generate.pass");
        private static readonly Histogram<double> s_generateDurationMs =
            s_meter.CreateHistogram<double>("itemsrepeater.generate.duration.ms");
        private static readonly Histogram<long> s_generateMeasuredCount =
            s_meter.CreateHistogram<long>("itemsrepeater.generate.measured.count");
        private static readonly Counter<long> s_elementRealized =
            s_meter.CreateCounter<long>("itemsrepeater.element.realized");
        private static readonly Counter<long> s_elementRecycled =
            s_meter.CreateCounter<long>("itemsrepeater.element.recycled");
        private static readonly Counter<long> s_makeAnchor =
            s_meter.CreateCounter<long>("itemsrepeater.anchor.make");
#endif

        internal static long GetTimestamp() => Stopwatch.GetTimestamp();

        internal static double GetElapsedMilliseconds(long startTimestamp)
        {
            return (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
        }

        internal static IDisposable? StartMeasure(
            string? layoutId,
            int itemCount,
            Size availableSize,
            Rect realizationRect,
            Rect visibleRect)
        {
#if NET6_0_OR_GREATER
            if (!s_activitySource.HasListeners())
            {
                return null;
            }

            var activity = s_activitySource.StartActivity("ItemsRepeater.Measure", ActivityKind.Internal);
            if (activity != null)
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

        internal static IDisposable? StartArrange(
            string? layoutId,
            int itemCount,
            Size finalSize)
        {
#if NET6_0_OR_GREATER
            if (!s_activitySource.HasListeners())
            {
                return null;
            }

            var activity = s_activitySource.StartActivity("ItemsRepeater.Arrange", ActivityKind.Internal);
            if (activity != null)
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

        internal static IDisposable? StartGenerate(
            string? layoutId,
            string direction,
            bool isWrapping,
            int anchorIndex)
        {
#if NET6_0_OR_GREATER
            if (!s_activitySource.HasListeners())
            {
                return null;
            }

            var activity = s_activitySource.StartActivity("ItemsRepeater.Generate", ActivityKind.Internal);
            if (activity != null)
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

        internal static void RecordMeasure(
            double durationMs,
            string? layoutId,
            int itemCount,
            int realizedCount)
        {
#if NET6_0_OR_GREATER
            var tags = new TagList
            {
                { "layout.id", layoutId ?? string.Empty },
                { "items.count", itemCount },
                { "realized.count", realizedCount }
            };
            s_measureCount.Add(1, tags);
            s_measureDurationMs.Record(durationMs, tags);
#endif
        }

        internal static void RecordArrange(
            double durationMs,
            string? layoutId,
            int itemCount,
            int realizedCount)
        {
#if NET6_0_OR_GREATER
            var tags = new TagList
            {
                { "layout.id", layoutId ?? string.Empty },
                { "items.count", itemCount },
                { "realized.count", realizedCount }
            };
            s_arrangeCount.Add(1, tags);
            s_arrangeDurationMs.Record(durationMs, tags);
#endif
        }

        internal static void RecordGenerate(
            double durationMs,
            string? layoutId,
            string direction,
            int measuredCount,
            int realizedCount,
            bool isWrapping,
            bool disableVirtualization)
        {
#if NET6_0_OR_GREATER
            var tags = new TagList
            {
                { "layout.id", layoutId ?? string.Empty },
                { "direction", direction },
                { "wrapping", isWrapping },
                { "disable.virtualization", disableVirtualization }
            };
            s_generateCount.Add(1, tags);
            s_generateDurationMs.Record(durationMs, tags);
            s_generateMeasuredCount.Record(measuredCount, tags);
#endif
        }

        internal static void RecordElementRealized(string? layoutId, int count)
        {
#if NET6_0_OR_GREATER
            var tags = new TagList
            {
                { "layout.id", layoutId ?? string.Empty }
            };
            s_elementRealized.Add(count, tags);
#endif
        }

        internal static void RecordElementRecycled(int count)
        {
#if NET6_0_OR_GREATER
            s_elementRecycled.Add(count);
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
            s_makeAnchor.Add(1, tags);
#endif
        }
    }
}
