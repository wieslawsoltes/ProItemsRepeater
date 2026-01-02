// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Avalonia.Layout
{
    internal class WrapLayoutState
    {
        private const int MaxLineCacheEntries = 2048;
        private Orientation _orientation;
        private UvMeasure _spacing;
        private double _availableU;
        private int _totalLineCount;
        private int _totalLineItemCount;
        private double _totalLineSize;
        private int _totalItemsMeasured;
        private double _totalItemMinor;
        private double _totalItemMajor;
        private int _itemCount;
        private double[] _itemMinorSizes = Array.Empty<double>();
        private double[] _itemMajorSizes = Array.Empty<double>();
        private bool _lineCacheDirty;
        private bool _hasFirstItemSize;
        private bool _hasVariableItemSizes;
        private double _firstItemMinor;
        private double _firstItemMajor;
        private readonly System.Collections.Generic.List<LineInfo> _lines = new System.Collections.Generic.List<LineInfo>();

        internal FlowLayoutAlgorithm FlowAlgorithm { get; } = new FlowLayoutAlgorithm();

        internal void InitializeForContext(VirtualizingLayoutContext context, IFlowLayoutAlgorithmDelegates callbacks)
        {
            FlowAlgorithm.InitializeForContext(context, callbacks);
            context.LayoutState = this;
        }

        internal void UninitializeForContext(VirtualizingLayoutContext context)
        {
            FlowAlgorithm.UninitializeForContext(context);
        }

        internal void EnsureParameters(Orientation orientation, UvMeasure spacing, double availableU)
        {
            var availableUDelta = Math.Abs(_availableU - availableU);
            var availableUChanged = availableUDelta > 0.5;
            if (_orientation != orientation || !_spacing.Equals(spacing) || availableUChanged)
            {
                _orientation = orientation;
                _spacing = spacing;
                _availableU = availableU;
                ClearLineStats();
                ClearItemStats();
            }
        }

        internal void EnsureItemCount(int itemCount)
        {
            if (itemCount < 0)
            {
                itemCount = 0;
            }

            if (_itemCount != itemCount)
            {
                _itemCount = itemCount;
                _itemMinorSizes = new double[itemCount];
                _itemMajorSizes = new double[itemCount];
                FillArray(_itemMinorSizes, double.NaN);
                FillArray(_itemMajorSizes, double.NaN);
                ClearLineStats();
                ClearItemStats();
            }
        }

        internal void BeginMeasure()
        {
            if (_lineCacheDirty)
            {
                ClearLineStats();
                _lineCacheDirty = false;
            }
        }

        internal void ClearLineStats()
        {
            _totalLineCount = 0;
            _totalLineItemCount = 0;
            _totalLineSize = 0;
            _lines.Clear();
            _lineCacheDirty = false;
        }

        internal void ClearItemStats()
        {
            _totalItemsMeasured = 0;
            _totalItemMinor = 0;
            _totalItemMajor = 0;
            _hasFirstItemSize = false;
            _hasVariableItemSizes = false;
            if (_itemMinorSizes.Length > 0)
            {
                FillArray(_itemMinorSizes, double.NaN);
                FillArray(_itemMajorSizes, double.NaN);
            }
        }

        private static void FillArray(double[] array, double value)
        {
#if NETSTANDARD2_0
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = value;
            }
#else
            Array.Fill(array, value);
#endif
        }

        internal void UpdateLineCache(int lineStartIndex, int itemCount, double linePosition, double lineSize)
        {
            if (lineSize <= 0 || itemCount <= 0)
            {
                return;
            }

            var line = new LineInfo(lineStartIndex, itemCount, linePosition, lineSize);

            if (_lines.Count == 0 || lineStartIndex > _lines[_lines.Count - 1].StartIndex)
            {
                _lines.Add(line);
                _totalLineCount++;
                _totalLineItemCount += itemCount;
                _totalLineSize += lineSize;
                TrimLineCache(lineStartIndex);
                return;
            }

            int low = 0;
            int high = _lines.Count - 1;
            while (low <= high)
            {
                int mid = (low + high) / 2;
                var current = _lines[mid];
                if (current.StartIndex == lineStartIndex)
                {
                    var allowUpdate = true;
                    if (itemCount < current.ItemCount || lineSize < current.Size - 0.001)
                    {
                        allowUpdate = false;
                    }

                    if (!allowUpdate)
                    {
                        return;
                    }

                    if (current.ItemCount != itemCount || Math.Abs(current.Size - lineSize) > 0.001)
                    {
                        _totalLineItemCount += itemCount - current.ItemCount;
                        _totalLineSize += lineSize - current.Size;
                    }
                    _lines[mid] = line;
                    return;
                }

                if (current.StartIndex < lineStartIndex)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            _lines.Insert(low, line);
            _totalLineCount++;
            _totalLineItemCount += itemCount;
            _totalLineSize += lineSize;
            TrimLineCache(lineStartIndex);
        }

        private void TrimLineCache(int lineStartIndex)
        {
            while (_lines.Count > MaxLineCacheEntries)
            {
                var distanceToStart = Math.Abs(lineStartIndex - _lines[0].StartIndex);
                var distanceToEnd = Math.Abs(lineStartIndex - _lines[_lines.Count - 1].StartIndex);
                LineInfo removed;
                if (distanceToStart > distanceToEnd)
                {
                    removed = _lines[0];
                    _lines.RemoveAt(0);
                }
                else
                {
                    var lastIndex = _lines.Count - 1;
                    removed = _lines[lastIndex];
                    _lines.RemoveAt(lastIndex);
                }

                _totalLineCount = Math.Max(0, _totalLineCount - 1);
                _totalLineItemCount -= removed.ItemCount;
                _totalLineSize -= removed.Size;
            }
        }

        internal bool TryGetLineBeforeViewport(double viewportMin, out int lineStartIndex, out int itemCount, out double linePosition, out double lineSize)
        {
            if (_lines.Count == 0)
            {
                lineStartIndex = 0;
                itemCount = 0;
                linePosition = 0;
                lineSize = 0;
                return false;
            }

            int low = 0;
            int high = _lines.Count - 1;
            int resultIndex = -1;

            while (low <= high)
            {
                int mid = (low + high) / 2;
                if (_lines[mid].Position <= viewportMin)
                {
                    resultIndex = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            if (resultIndex >= 0)
            {
                var line = _lines[resultIndex];
                lineStartIndex = line.StartIndex;
                itemCount = line.ItemCount;
                linePosition = line.Position;
                lineSize = line.Size;
                return true;
            }

            lineStartIndex = 0;
            itemCount = 0;
            linePosition = 0;
            lineSize = 0;
            return false;
        }

        internal bool TryGetLineNearViewport(double viewportMin, out int lineStartIndex, out int itemCount, out double linePosition, out double lineSize)
        {
            if (_lines.Count == 0)
            {
                lineStartIndex = 0;
                itemCount = 0;
                linePosition = 0;
                lineSize = 0;
                return false;
            }

            var first = _lines[0];
            var last = _lines[_lines.Count - 1];
            if (viewportMin < first.Position || viewportMin > last.Position + last.Size)
            {
                lineStartIndex = 0;
                itemCount = 0;
                linePosition = 0;
                lineSize = 0;
                return false;
            }

            return TryGetLineBeforeViewport(viewportMin, out lineStartIndex, out itemCount, out linePosition, out lineSize);
        }

        internal bool TryGetLineForItemIndex(int index, out int lineStartIndex, out int itemCount, out double linePosition, out double lineSize)
        {
            if (_lines.Count == 0 || index < 0)
            {
                lineStartIndex = 0;
                itemCount = 0;
                linePosition = 0;
                lineSize = 0;
                return false;
            }

            int low = 0;
            int high = _lines.Count - 1;
            int resultIndex = -1;

            while (low <= high)
            {
                int mid = (low + high) / 2;
                if (_lines[mid].StartIndex <= index)
                {
                    resultIndex = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            if (resultIndex >= 0)
            {
                var line = _lines[resultIndex];
                if (index < line.StartIndex + line.ItemCount)
                {
                    lineStartIndex = line.StartIndex;
                    itemCount = line.ItemCount;
                    linePosition = line.Position;
                    lineSize = line.Size;
                    return true;
                }
            }

            lineStartIndex = 0;
            itemCount = 0;
            linePosition = 0;
            lineSize = 0;
            return false;
        }

        internal bool TryGetNearestLine(double viewportMin, out int lineStartIndex, out int itemCount, out double linePosition, out double lineSize)
        {
            if (_lines.Count == 0)
            {
                lineStartIndex = 0;
                itemCount = 0;
                linePosition = 0;
                lineSize = 0;
                return false;
            }

            int low = 0;
            int high = _lines.Count - 1;
            int resultIndex = _lines.Count;

            while (low <= high)
            {
                int mid = (low + high) / 2;
                if (_lines[mid].Position >= viewportMin)
                {
                    resultIndex = mid;
                    high = mid - 1;
                }
                else
                {
                    low = mid + 1;
                }
            }

            if (resultIndex <= 0)
            {
                var line = _lines[0];
                lineStartIndex = line.StartIndex;
                itemCount = line.ItemCount;
                linePosition = line.Position;
                lineSize = line.Size;
                return true;
            }

            if (resultIndex >= _lines.Count)
            {
                var line = _lines[_lines.Count - 1];
                lineStartIndex = line.StartIndex;
                itemCount = line.ItemCount;
                linePosition = line.Position;
                lineSize = line.Size;
                return true;
            }

            var next = _lines[resultIndex];
            var prev = _lines[resultIndex - 1];
            var nextDistance = Math.Abs(next.Position - viewportMin);
            var prevDistance = Math.Abs(prev.Position - viewportMin);

            var chosen = nextDistance < prevDistance ? next : prev;
            lineStartIndex = chosen.StartIndex;
            itemCount = chosen.ItemCount;
            linePosition = chosen.Position;
            lineSize = chosen.Size;
            return true;
        }

        private readonly struct LineInfo
        {
            public LineInfo(int startIndex, int itemCount, double position, double size)
            {
                StartIndex = startIndex;
                ItemCount = itemCount;
                Position = position;
                Size = size;
            }

            public int StartIndex { get; }
            public int ItemCount { get; }
            public double Position { get; }
            public double Size { get; }
        }

        internal void RecordItemSize(int index, double minor, double major)
        {
            if (minor <= 0 || major <= 0 || (uint)index >= (uint)_itemCount)
            {
                return;
            }

            if (!_hasFirstItemSize)
            {
                _hasFirstItemSize = true;
                _firstItemMinor = minor;
                _firstItemMajor = major;
            }
            else if (!_hasVariableItemSizes &&
                (Math.Abs(_firstItemMinor - minor) > 0.001 || Math.Abs(_firstItemMajor - major) > 0.001))
            {
                _hasVariableItemSizes = true;
            }

            var previousMinor = _itemMinorSizes[index];
            var previousMajor = _itemMajorSizes[index];
            if (double.IsNaN(previousMinor) || double.IsNaN(previousMajor))
            {
                _itemMinorSizes[index] = minor;
                _itemMajorSizes[index] = major;
                _totalItemsMeasured++;
                _totalItemMinor += minor;
                _totalItemMajor += major;
                return;
            }

            if (Math.Abs(previousMinor - minor) > 0.001 || Math.Abs(previousMajor - major) > 0.001)
            {
                _lineCacheDirty = true;
            }

            if (Math.Abs(previousMinor - minor) > 0.001)
            {
                _itemMinorSizes[index] = minor;
                _totalItemMinor += minor - previousMinor;
            }

            if (Math.Abs(previousMajor - major) > 0.001)
            {
                _itemMajorSizes[index] = major;
                _totalItemMajor += major - previousMajor;
            }
        }

        internal bool TryGetAverageLineMetrics(out double averageLineSize, out double averageItemsPerLine)
        {
            if (_totalLineCount > 0 && _totalLineItemCount > 0)
            {
                averageLineSize = _totalLineSize / _totalLineCount;
                averageItemsPerLine = (double)_totalLineItemCount / _totalLineCount;
                return averageLineSize > 0 && averageItemsPerLine > 0;
            }

            averageLineSize = 0;
            averageItemsPerLine = 0;
            return false;
        }

        internal bool TryGetAverageItemMetrics(out double averageMinor, out double averageMajor)
        {
            if (_totalItemsMeasured > 0)
            {
                averageMinor = _totalItemMinor / _totalItemsMeasured;
                averageMajor = _totalItemMajor / _totalItemsMeasured;
                return averageMinor > 0 && averageMajor > 0;
            }

            averageMinor = 0;
            averageMajor = 0;
            return false;
        }

        internal bool HasVariableItemSizes => _hasVariableItemSizes;
    }
}
