// This source file is adapted from the WinUI project.
// (https://github.com/microsoft/microsoft-ui-xaml)
//
// Licensed to The Avalonia Project under MIT License, courtesy of The .NET Foundation.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Avalonia.Layout
{
    /// <summary>
    /// Represents the state of a StackLayout.
    /// </summary>
    public class StackLayoutState
    {
        private const int BufferSize = 100;
        private readonly List<double> _estimationBuffer = new List<double>();
        private readonly List<LineInfo> _lineCache = new List<LineInfo>();
        private Orientation _lineCacheOrientation;
        private double _lineCacheSpacing;
        private bool _lineCacheInitialized;
        private double[] _measuredSizes = Array.Empty<double>();
        private FenwickDouble _measuredSizeSum = new FenwickDouble(0);
        private FenwickInt _measuredCount = new FenwickInt(0);
        private int _itemCount;

        internal FlowLayoutAlgorithm FlowAlgorithm { get; } = new FlowLayoutAlgorithm();
        internal double MaxArrangeBounds { get; private set; }
        internal int TotalElementsMeasured { get; private set; }
        internal double TotalElementSize { get; private set; }

        internal void InitializeForContext(VirtualizingLayoutContext context, IFlowLayoutAlgorithmDelegates callbacks)
        {
            FlowAlgorithm.InitializeForContext(context, callbacks);

            if (_estimationBuffer.Count == 0)
            {
                _estimationBuffer.AddRange(Enumerable.Repeat(0.0, BufferSize));
            }

            context.LayoutState = this;
        }

        internal void UninitializeForContext(VirtualizingLayoutContext context)
        {
            FlowAlgorithm.UninitializeForContext(context);
        }

        internal void EnsureLineCacheParameters(Orientation orientation, double spacing)
        {
            if (!_lineCacheInitialized || _lineCacheOrientation != orientation || _lineCacheSpacing != spacing)
            {
                var orientationChanged = _lineCacheInitialized && _lineCacheOrientation != orientation;
                _lineCache.Clear();
                _lineCacheOrientation = orientation;
                _lineCacheSpacing = spacing;
                _lineCacheInitialized = true;
                if (orientationChanged)
                {
                    ClearSizeCache();
                }
            }
        }

        internal void ClearLineCache()
        {
            _lineCache.Clear();
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
                _measuredSizes = new double[itemCount];
                FillArray(_measuredSizes, double.NaN);
                _measuredSizeSum = new FenwickDouble(itemCount);
                _measuredCount = new FenwickInt(itemCount);
            }
        }

        internal void ClearSizeCache()
        {
            if (_measuredSizes.Length == 0)
            {
                return;
            }

            FillArray(_measuredSizes, double.NaN);
            _measuredSizeSum.Reset();
            _measuredCount.Reset();
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

        internal void UpdateMeasuredSize(int index, double majorSize)
        {
            if ((uint)index >= (uint)_itemCount)
            {
                return;
            }

            var previous = _measuredSizes[index];
            if (double.IsNaN(previous))
            {
                _measuredSizes[index] = majorSize;
                _measuredSizeSum.Add(index, majorSize);
                _measuredCount.Add(index, 1);
            }
            else if (Math.Abs(previous - majorSize) > 0.001)
            {
                _measuredSizes[index] = majorSize;
                _measuredSizeSum.Add(index, majorSize - previous);
            }
        }

        internal double GetEstimatedOffsetForIndex(int index, double estimatedSize, double spacing)
        {
            if (index <= 0)
            {
                return 0;
            }

            var clampedIndex = Math.Min(index, _itemCount);
            var measuredSum = _measuredSizeSum.Sum(clampedIndex);
            var measuredCount = _measuredCount.Sum(clampedIndex);
            var unknownCount = clampedIndex - measuredCount;
            return measuredSum + (unknownCount * estimatedSize) + (spacing * clampedIndex);
        }

        internal int EstimateIndexForOffset(double offset, double estimatedSize, double spacing, int itemCount)
        {
            if (itemCount <= 0)
            {
                return -1;
            }

            var clampedEstimate = Math.Max(1.0, estimatedSize);
            var low = 0;
            var high = itemCount - 1;
            var result = 0;

            while (low <= high)
            {
                var mid = (low + high) / 2;
                var midOffset = GetEstimatedOffsetForIndex(mid, clampedEstimate, spacing);

                if (midOffset <= offset)
                {
                    result = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return Math.Max(0, Math.Min(itemCount - 1, result));
        }

        internal double GetEstimatedTotalSize(double estimatedSize, double spacing, int itemCount)
        {
            if (itemCount <= 0)
            {
                return 0;
            }

            var measuredSum = _measuredSizeSum.Sum(itemCount);
            var measuredCount = _measuredCount.Sum(itemCount);
            var unknownCount = itemCount - measuredCount;
            return measuredSum + (unknownCount * estimatedSize) + (spacing * Math.Max(0, itemCount - 1));
        }

        internal bool TryGetMeasuredAverage(out double average)
        {
            if (_itemCount <= 0)
            {
                average = 0;
                return false;
            }

            var measuredCount = _measuredCount.Sum(_itemCount);
            if (measuredCount <= 0)
            {
                average = 0;
                return false;
            }

            var measuredSum = _measuredSizeSum.Sum(_itemCount);
            average = measuredSum / measuredCount;
            return true;
        }

        internal void UpdateLineCache(int startIndex, double position, double size)
        {
            if (size <= 0)
            {
                return;
            }

            var line = new LineInfo(startIndex, position, size);

            if (_lineCache.Count == 0 || startIndex > _lineCache[_lineCache.Count - 1].StartIndex)
            {
                _lineCache.Add(line);
                return;
            }

            int low = 0;
            int high = _lineCache.Count - 1;
            while (low <= high)
            {
                int mid = (low + high) / 2;
                var current = _lineCache[mid];
                if (current.StartIndex == startIndex)
                {
                    _lineCache[mid] = line;
                    return;
                }

                if (current.StartIndex < startIndex)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            _lineCache.Insert(low, line);
        }

        internal bool TryGetLineBeforeViewport(double viewportMajor, out int lineStartIndex, out double linePosition, out double lineSize)
        {
            if (_lineCache.Count == 0)
            {
                lineStartIndex = 0;
                linePosition = 0;
                lineSize = 0;
                return false;
            }

            int low = 0;
            int high = _lineCache.Count - 1;
            int resultIndex = -1;

            while (low <= high)
            {
                int mid = (low + high) / 2;
                if (_lineCache[mid].Position <= viewportMajor)
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
                var line = _lineCache[resultIndex];
                lineStartIndex = line.StartIndex;
                linePosition = line.Position;
                lineSize = line.Size;
                return true;
            }

            lineStartIndex = 0;
            linePosition = 0;
            lineSize = 0;
            return false;
        }

        internal bool TryGetCachedRange(out double minPosition, out double maxPosition)
        {
            if (_lineCache.Count == 0)
            {
                minPosition = 0;
                maxPosition = 0;
                return false;
            }

            var first = _lineCache[0];
            var last = _lineCache[_lineCache.Count - 1];
            minPosition = first.Position;
            maxPosition = last.Position + last.Size;
            return true;
        }

        internal bool TryGetLineForIndex(int index, out double linePosition, out double lineSize)
        {
            if (_lineCache.Count == 0)
            {
                linePosition = 0;
                lineSize = 0;
                return false;
            }

            int low = 0;
            int high = _lineCache.Count - 1;
            while (low <= high)
            {
                int mid = (low + high) / 2;
                var current = _lineCache[mid];
                if (current.StartIndex == index)
                {
                    linePosition = current.Position;
                    lineSize = current.Size;
                    return true;
                }

                if (current.StartIndex < index)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            linePosition = 0;
            lineSize = 0;
            return false;
        }

        internal void OnElementMeasured(int elementIndex, double majorSize, double minorSize)
        {
            int estimationBufferIndex = elementIndex % _estimationBuffer.Count;
            bool alreadyMeasured = _estimationBuffer[estimationBufferIndex] != 0;

            if (!alreadyMeasured)
            {
                TotalElementsMeasured++;
            }

            TotalElementSize -= _estimationBuffer[estimationBufferIndex];
            TotalElementSize += majorSize;
            _estimationBuffer[estimationBufferIndex] = majorSize;

            MaxArrangeBounds = Math.Max(MaxArrangeBounds, minorSize);
            UpdateMeasuredSize(elementIndex, majorSize);
        }

        internal void OnMeasureStart() => MaxArrangeBounds = 0;

        private readonly struct LineInfo
        {
            public LineInfo(int startIndex, double position, double size)
            {
                StartIndex = startIndex;
                Position = position;
                Size = size;
            }

            public int StartIndex { get; }
            public double Position { get; }
            public double Size { get; }
        }

        private sealed class FenwickDouble
        {
            private readonly double[] _tree;

            public FenwickDouble(int size)
            {
                _tree = new double[size + 1];
            }

            public void Reset()
            {
                Array.Clear(_tree, 0, _tree.Length);
            }

            public void Add(int index, double delta)
            {
                for (var i = index + 1; i < _tree.Length; i += i & -i)
                {
                    _tree[i] += delta;
                }
            }

            public double Sum(int index)
            {
                var sum = 0.0;
                for (var i = index; i > 0; i -= i & -i)
                {
                    sum += _tree[i];
                }
                return sum;
            }
        }

        private sealed class FenwickInt
        {
            private readonly int[] _tree;

            public FenwickInt(int size)
            {
                _tree = new int[size + 1];
            }

            public void Reset()
            {
                Array.Clear(_tree, 0, _tree.Length);
            }

            public void Add(int index, int delta)
            {
                for (var i = index + 1; i < _tree.Length; i += i & -i)
                {
                    _tree[i] += delta;
                }
            }

            public int Sum(int index)
            {
                var sum = 0;
                for (var i = index; i > 0; i -= i & -i)
                {
                    sum += _tree[i];
                }
                return sum;
            }
        }
    }
}
