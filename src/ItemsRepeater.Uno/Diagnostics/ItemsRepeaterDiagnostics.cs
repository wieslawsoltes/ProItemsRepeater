using System;
using Avalonia;

namespace Avalonia.Controls
{
    internal static class ItemsRepeaterDiagnostics
    {
        internal static long GetTimestamp() => 0;

        internal static double GetElapsedMilliseconds(long startTimestamp)
        {
            _ = startTimestamp;
            return 0;
        }

        internal static IDisposable? StartMeasure(string? layoutId, int itemCount, Size availableSize, Rect realizationRect, Rect visibleRect)
        {
            _ = layoutId;
            _ = itemCount;
            _ = availableSize;
            _ = realizationRect;
            _ = visibleRect;
            return null;
        }

        internal static IDisposable? StartArrange(string? layoutId, int itemCount, Size finalSize)
        {
            _ = layoutId;
            _ = itemCount;
            _ = finalSize;
            return null;
        }

        internal static IDisposable? StartGenerate(string? layoutId, string direction, bool isWrapping, int anchorIndex)
        {
            _ = layoutId;
            _ = direction;
            _ = isWrapping;
            _ = anchorIndex;
            return null;
        }

        internal static void RecordMeasure(double durationMs, string? layoutId, int itemCount, int realizedCount)
        {
            _ = durationMs;
            _ = layoutId;
            _ = itemCount;
            _ = realizedCount;
        }

        internal static void RecordArrange(double durationMs, string? layoutId, int itemCount, int realizedCount)
        {
            _ = durationMs;
            _ = layoutId;
            _ = itemCount;
            _ = realizedCount;
        }

        internal static void RecordGenerate(double durationMs, string? layoutId, string direction, int measuredCount, int realizedCount, bool isWrapping, bool disableVirtualization)
        {
            _ = durationMs;
            _ = layoutId;
            _ = direction;
            _ = measuredCount;
            _ = realizedCount;
            _ = isWrapping;
            _ = disableVirtualization;
        }

        internal static void RecordElementRealized(string? layoutId, int count)
        {
            _ = layoutId;
            _ = count;
        }

        internal static void RecordElementRecycled(int count)
        {
            _ = count;
        }

        internal static void RecordMakeAnchor(string? layoutId, int anchorIndex)
        {
            _ = layoutId;
            _ = anchorIndex;
        }
    }
}
