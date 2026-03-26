using System.Collections.Generic;
using System.Collections.Specialized;
using Avalonia.Layout.Utils;
using Avalonia.Logging;

namespace Avalonia.Layout
{
    internal class ElementManager
    {
        private readonly List<Layoutable?> _realizedElements = new();
        private readonly List<Rect> _realizedElementLayoutBounds = new();
        private int _firstRealizedDataIndex;
        private VirtualizingLayoutContext? _context;

        private bool IsVirtualizingContext
        {
            get
            {
                if (_context != null)
                {
                    var rect = _context.RealizationRect;
                    var hasInfiniteSize = double.IsInfinity(rect.Height) && double.IsInfinity(rect.Width);
                    return !hasInfiniteSize;
                }

                return false;
            }
        }

        public void SetContext(VirtualizingLayoutContext virtualContext) => _context = virtualContext;

        public void OnBeginMeasure(ScrollOrientation orientation)
        {
            if (_context != null)
            {
                if (IsVirtualizingContext)
                {
                    DiscardElementsOutsideWindow(_context.RealizationRect, orientation);
                }
                else
                {
                    var count = _context.ItemCount;
                    if (_realizedElementLayoutBounds.Count != count)
                    {
                        _realizedElementLayoutBounds.Resize(count, default);
                    }
                }
            }
        }

        public int GetRealizedElementCount()
        {
            return IsVirtualizingContext ? _realizedElements.Count : _context!.ItemCount;
        }

        public Layoutable GetAt(int realizedIndex)
        {
            Layoutable? element;

            if (IsVirtualizingContext)
            {
                element = _realizedElements[realizedIndex];
                if (element == null)
                {
                    var dataIndex = GetDataIndexFromRealizedRangeIndex(realizedIndex);
                    Logger.TryGet(LogEventLevel.Verbose, "Repeater")?.Log(this, "Creating element for sentinal with data index {Index}", dataIndex);
                    element = _context!.GetOrCreateElementAt(
                        dataIndex,
                        ElementRealizationOptions.ForceCreate | ElementRealizationOptions.SuppressAutoRecycle);
                    _realizedElements[realizedIndex] = element;
                }
            }
            else
            {
                element = _context!.GetOrCreateElementAt(
                    realizedIndex,
                    ElementRealizationOptions.ForceCreate | ElementRealizationOptions.SuppressAutoRecycle);
            }

            return element;
        }

        public void Add(Layoutable element, int dataIndex)
        {
            if (_realizedElements.Count == 0)
            {
                _firstRealizedDataIndex = dataIndex;
            }

            _realizedElements.Add(element);
            _realizedElementLayoutBounds.Add(default);
        }

        public void Insert(int realizedIndex, int dataIndex, Layoutable? element)
        {
            if (realizedIndex == 0)
            {
                _firstRealizedDataIndex = dataIndex;
            }

            _realizedElements.Insert(realizedIndex, element);
            _realizedElementLayoutBounds.Insert(realizedIndex, new Rect(-1, -1, -1, -1));
        }

        public void ClearRealizedRange(int realizedIndex, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var index = realizedIndex == 0 ? realizedIndex + i : (realizedIndex + count - 1) - i;
                var elementRef = _realizedElements[index];
                if (elementRef != null)
                {
                    _context!.RecycleElement(elementRef);
                }
            }

            var endIndex = realizedIndex + count;
            _realizedElements.RemoveRange(realizedIndex, endIndex - realizedIndex);
            _realizedElementLayoutBounds.RemoveRange(realizedIndex, endIndex - realizedIndex);

            if (realizedIndex == 0)
            {
                _firstRealizedDataIndex = _realizedElements.Count == 0 ? -1 : _firstRealizedDataIndex + count;
            }
        }

        public void DiscardElementsOutsideWindow(bool forward, int startIndex)
        {
            if (IsDataIndexRealized(startIndex))
            {
                var rangeIndex = GetRealizedRangeIndexFromDataIndex(startIndex);
                if (forward)
                {
                    ClearRealizedRange(rangeIndex, GetRealizedElementCount() - rangeIndex);
                }
                else
                {
                    ClearRealizedRange(0, rangeIndex + 1);
                }
            }
        }

        public void ClearRealizedRange() => ClearRealizedRange(0, GetRealizedElementCount());

        public bool HasInvalidMeasure()
        {
            if (!IsVirtualizingContext)
            {
                return false;
            }

            for (var realizedIndex = 0; realizedIndex < _realizedElements.Count; ++realizedIndex)
            {
                var element = _realizedElements[realizedIndex];
                if (element is null)
                {
                    continue;
                }

                var bounds = _realizedElementLayoutBounds[realizedIndex];
                if (bounds == Avalonia.Controls.ItemsRepeater.InvalidRect)
                {
                    return true;
                }

                if ((bounds.Width > 0 || bounds.Height > 0) &&
                    element.DesiredSize.Width == 0 &&
                    element.DesiredSize.Height == 0)
                {
                    return true;
                }
            }

            return false;
        }

        public Rect GetLayoutBoundsForDataIndex(int dataIndex)
        {
            var realizedIndex = GetRealizedRangeIndexFromDataIndex(dataIndex);
            return _realizedElementLayoutBounds[realizedIndex];
        }

        public void SetLayoutBoundsForDataIndex(int dataIndex, in Rect bounds)
        {
            var realizedIndex = GetRealizedRangeIndexFromDataIndex(dataIndex);
            _realizedElementLayoutBounds[realizedIndex] = bounds;
        }

        public Rect GetLayoutBoundsForRealizedIndex(int realizedIndex) => _realizedElementLayoutBounds[realizedIndex];

        public void SetLayoutBoundsForRealizedIndex(int realizedIndex, in Rect bounds)
        {
            _realizedElementLayoutBounds[realizedIndex] = bounds;
        }

        public bool IsDataIndexRealized(int index)
        {
            if (IsVirtualizingContext)
            {
                var realizedCount = GetRealizedElementCount();
                return realizedCount > 0 &&
                    GetDataIndexFromRealizedRangeIndex(0) <= index &&
                    GetDataIndexFromRealizedRangeIndex(realizedCount - 1) >= index;
            }

            return index >= 0 && index < _context!.ItemCount;
        }

        public bool IsIndexValidInData(int currentIndex) => (uint)currentIndex < _context!.ItemCount;

        public Layoutable? GetRealizedElement(int dataIndex)
        {
            return IsVirtualizingContext
                ? GetAt(GetRealizedRangeIndexFromDataIndex(dataIndex))
                : _context!.GetOrCreateElementAt(
                    dataIndex,
                    ElementRealizationOptions.ForceCreate | ElementRealizationOptions.SuppressAutoRecycle);
        }

        public void EnsureElementRealized(bool forward, int dataIndex, string? layoutId)
        {
            if (!IsDataIndexRealized(dataIndex))
            {
                var element = _context!.GetOrCreateElementAt(
                    dataIndex,
                    ElementRealizationOptions.ForceCreate | ElementRealizationOptions.SuppressAutoRecycle);

                if (forward)
                {
                    Add(element, dataIndex);
                }
                else
                {
                    Insert(0, dataIndex, element);
                }

                Logger.TryGet(LogEventLevel.Verbose, "Repeater")?.Log(this, "{LayoutId}: Created element for index {index}", layoutId, dataIndex);
            }
        }

        public bool IsWindowConnected(in Rect window, ScrollOrientation orientation, bool scrollOrientationSameAsFlow)
        {
            var intersects = false;

            if (_realizedElementLayoutBounds.Count > 0)
            {
                var firstElementBounds = GetLayoutBoundsForRealizedIndex(0);
                var lastElementBounds = GetLayoutBoundsForRealizedIndex(GetRealizedElementCount() - 1);

                var effectiveOrientation = scrollOrientationSameAsFlow
                    ? (orientation == ScrollOrientation.Vertical ? ScrollOrientation.Horizontal : ScrollOrientation.Vertical)
                    : orientation;

                var windowStart = effectiveOrientation == ScrollOrientation.Vertical ? window.Y : window.X;
                var windowEnd = effectiveOrientation == ScrollOrientation.Vertical ? window.Y + window.Height : window.X + window.Width;
                var firstElementStart = effectiveOrientation == ScrollOrientation.Vertical ? firstElementBounds.Y : firstElementBounds.X;
                var lastElementEnd = effectiveOrientation == ScrollOrientation.Vertical ? lastElementBounds.Y + lastElementBounds.Height : lastElementBounds.X + lastElementBounds.Width;

                intersects = firstElementStart <= windowEnd && lastElementEnd >= windowStart;
            }

            return intersects;
        }

        public void DataSourceChanged(object? source, NotifyCollectionChangedEventArgs args)
        {
            _ = source;
            if (_realizedElements.Count > 0)
            {
                switch (args.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        OnItemsAdded(args.NewStartingIndex, args.NewItems!.Count);
                        break;

                    case NotifyCollectionChangedAction.Replace:
                    {
                        var oldSize = args.OldItems!.Count;
                        var newSize = args.NewItems!.Count;
                        var oldStartIndex = args.OldStartingIndex;
                        var newStartIndex = args.NewStartingIndex;

                        if (oldSize == newSize &&
                            oldStartIndex == newStartIndex &&
                            IsDataIndexRealized(oldStartIndex) &&
                            IsDataIndexRealized(oldStartIndex + oldSize - 1))
                        {
                            var startRealizedIndex = GetRealizedRangeIndexFromDataIndex(oldStartIndex);
                            for (var realizedIndex = startRealizedIndex; realizedIndex < startRealizedIndex + oldSize; realizedIndex++)
                            {
                                var elementRef = _realizedElements[realizedIndex];
                                if (elementRef != null)
                                {
                                    _context!.RecycleElement(elementRef);
                                    _realizedElements[realizedIndex] = null;
                                }
                            }
                        }
                        else
                        {
                            OnItemsRemoved(oldStartIndex, oldSize);
                            OnItemsAdded(newStartIndex, newSize);
                        }

                        break;
                    }

                    case NotifyCollectionChangedAction.Remove:
                    case NotifyCollectionChangedAction.Reset:
                        ClearRealizedRange();
                        break;

                    case NotifyCollectionChangedAction.Move:
                    {
                        var size = args.OldItems != null ? args.OldItems.Count : 1;
                        OnItemsRemoved(args.OldStartingIndex, size);
                        OnItemsAdded(args.NewStartingIndex, size);
                        break;
                    }
                }
            }
        }

        public int GetElementDataIndex(Layoutable suggestedAnchor)
        {
            var it = _realizedElements.IndexOf(suggestedAnchor);
            return it != -1 ? GetDataIndexFromRealizedRangeIndex(it) : -1;
        }

        public int GetDataIndexFromRealizedRangeIndex(int rangeIndex)
        {
            return IsVirtualizingContext ? rangeIndex + _firstRealizedDataIndex : rangeIndex;
        }

        private int GetRealizedRangeIndexFromDataIndex(int dataIndex)
        {
            return IsVirtualizingContext ? dataIndex - _firstRealizedDataIndex : dataIndex;
        }

        private void DiscardElementsOutsideWindow(in Rect window, ScrollOrientation orientation)
        {
            var realizedRangeSize = GetRealizedElementCount();
            var frontCutoffIndex = -1;
            var backCutoffIndex = realizedRangeSize;

            for (var i = 0; i < realizedRangeSize && !Intersects(window, _realizedElementLayoutBounds[i], orientation); ++i)
            {
                ++frontCutoffIndex;
            }

            for (var i = realizedRangeSize - 1; i >= 0 && !Intersects(window, _realizedElementLayoutBounds[i], orientation); --i)
            {
                --backCutoffIndex;
            }

            if (backCutoffIndex < realizedRangeSize - 1)
            {
                ClearRealizedRange(backCutoffIndex + 1, realizedRangeSize - backCutoffIndex - 1);
            }

            if (frontCutoffIndex > 0)
            {
                ClearRealizedRange(0, System.Math.Min(frontCutoffIndex, GetRealizedElementCount()));
            }
        }

        private static bool Intersects(in Rect lhs, in Rect rhs, ScrollOrientation orientation)
        {
            var lhsStart = orientation == ScrollOrientation.Vertical ? lhs.Y : lhs.X;
            var lhsEnd = orientation == ScrollOrientation.Vertical ? lhs.Y + lhs.Height : lhs.X + lhs.Width;
            var rhsStart = orientation == ScrollOrientation.Vertical ? rhs.Y : rhs.X;
            var rhsEnd = orientation == ScrollOrientation.Vertical ? rhs.Y + rhs.Height : rhs.X + rhs.Width;
            return lhsEnd >= rhsStart && lhsStart <= rhsEnd;
        }

        private void OnItemsAdded(int index, int count)
        {
            var lastRealizedDataIndex = _firstRealizedDataIndex + GetRealizedElementCount() - 1;
            var newStartingIndex = index;
            if (newStartingIndex >= _firstRealizedDataIndex &&
                newStartingIndex <= lastRealizedDataIndex)
            {
                var insertRangeStartIndex = newStartingIndex - _firstRealizedDataIndex;
                for (var i = 0; i < count; i++)
                {
                    var insertRangeIndex = insertRangeStartIndex + i;
                    var dataIndex = newStartingIndex + i;
                    Insert(insertRangeIndex, dataIndex, null);
                }
            }
            else if (index <= _firstRealizedDataIndex)
            {
                _firstRealizedDataIndex += count;
            }
        }

        private void OnItemsRemoved(int index, int count)
        {
            var lastRealizedDataIndex = _firstRealizedDataIndex + _realizedElements.Count - 1;
            var startIndex = System.Math.Max(_firstRealizedDataIndex, index);
            var endIndex = System.Math.Min(lastRealizedDataIndex, index + count - 1);
            var removeAffectsFirstRealizedDataIndex = index <= _firstRealizedDataIndex;

            if (endIndex >= startIndex)
            {
                ClearRealizedRange(GetRealizedRangeIndexFromDataIndex(startIndex), endIndex - startIndex + 1);
            }

            if (removeAffectsFirstRealizedDataIndex && _firstRealizedDataIndex != -1)
            {
                _firstRealizedDataIndex -= count;
            }
        }
    }
}
