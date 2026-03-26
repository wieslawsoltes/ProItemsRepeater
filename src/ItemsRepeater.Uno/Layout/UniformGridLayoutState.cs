using System;
using System.Collections.Specialized;

namespace Avalonia.Layout
{
    public class UniformGridLayoutState
    {
        private Layoutable? _cachedFirstElement;

        internal FlowLayoutAlgorithm FlowAlgorithm { get; } = new();
        internal double EffectiveItemWidth { get; private set; }
        internal double EffectiveItemHeight { get; private set; }

        internal void InitializeForContext(VirtualizingLayoutContext context, IFlowLayoutAlgorithmDelegates callbacks)
        {
            FlowAlgorithm.InitializeForContext(context, callbacks);
            context.LayoutState = this;
        }

        internal void UninitializeForContext(VirtualizingLayoutContext context)
        {
            FlowAlgorithm.UninitializeForContext(context);

            if (_cachedFirstElement != null)
            {
                context.RecycleElement(_cachedFirstElement);
            }
        }

        internal void EnsureElementSize(Size availableSize, VirtualizingLayoutContext context, double layoutItemWidth, double layoutItemHeight, UniformGridLayoutItemsStretch stretch, Orientation orientation, double minRowSpacing, double minColumnSpacing, int maxItemsPerLine)
        {
            if (maxItemsPerLine == 0)
            {
                maxItemsPerLine = 1;
            }

            if (context.ItemCount > 0)
            {
                var realizedElement = FlowAlgorithm.GetElementIfRealized(0);
                if (realizedElement != null)
                {
                    realizedElement.Measure(availableSize);
                    SetSize(realizedElement, layoutItemWidth, layoutItemHeight, availableSize, stretch, orientation, minRowSpacing, minColumnSpacing, maxItemsPerLine);
                    _cachedFirstElement = null;
                }
                else
                {
                    if (_cachedFirstElement == null)
                    {
                        _cachedFirstElement = context.GetOrCreateElementAt(0, ElementRealizationOptions.ForceCreate | ElementRealizationOptions.SuppressAutoRecycle);
                    }

                    _cachedFirstElement.Measure(availableSize);
                    SetSize(_cachedFirstElement, layoutItemWidth, layoutItemHeight, availableSize, stretch, orientation, minRowSpacing, minColumnSpacing, maxItemsPerLine);

                    var added = FlowAlgorithm.TryAddElement0(_cachedFirstElement);
                    if (added)
                    {
                        _cachedFirstElement = null;
                    }
                }
            }
        }

        private void SetSize(Layoutable element, double layoutItemWidth, double layoutItemHeight, Size availableSize, UniformGridLayoutItemsStretch stretch, Orientation orientation, double minRowSpacing, double minColumnSpacing, int maxItemsPerLine)
        {
            if (maxItemsPerLine == 0)
            {
                maxItemsPerLine = 1;
            }

            var desiredSize = element.DesiredSize.ToAvalonia();
            EffectiveItemWidth = double.IsNaN(layoutItemWidth) ? desiredSize.Width : layoutItemWidth;
            EffectiveItemHeight = double.IsNaN(layoutItemHeight) ? desiredSize.Height : layoutItemHeight;

            var availableSizeMinor = orientation == Orientation.Horizontal ? availableSize.Width : availableSize.Height;
            var minorItemSpacing = orientation == Orientation.Vertical ? minRowSpacing : minColumnSpacing;
            var itemSizeMinor = orientation == Orientation.Horizontal ? EffectiveItemWidth : EffectiveItemHeight;

            var extraMinorPixelsForEachItem = 0.0;
            if (!double.IsInfinity(availableSizeMinor))
            {
                var numItemsPerColumn = (int)Math.Min(maxItemsPerLine, Math.Max(1.0, availableSizeMinor / (itemSizeMinor + minorItemSpacing)));
                var usedSpace = (numItemsPerColumn * (itemSizeMinor + minorItemSpacing)) - minorItemSpacing;
                var remainingSpace = availableSizeMinor - usedSpace;
                extraMinorPixelsForEachItem = (int)(remainingSpace / numItemsPerColumn);
            }

            if (stretch == UniformGridLayoutItemsStretch.Fill)
            {
                if (orientation == Orientation.Horizontal)
                {
                    EffectiveItemWidth += extraMinorPixelsForEachItem;
                }
                else
                {
                    EffectiveItemHeight += extraMinorPixelsForEachItem;
                }
            }
            else if (stretch == UniformGridLayoutItemsStretch.Uniform)
            {
                var itemSizeMajor = orientation == Orientation.Horizontal ? EffectiveItemHeight : EffectiveItemWidth;
                var extraMajorPixelsForEachItem = itemSizeMajor * (extraMinorPixelsForEachItem / itemSizeMinor);
                if (orientation == Orientation.Horizontal)
                {
                    EffectiveItemWidth += extraMinorPixelsForEachItem;
                    EffectiveItemHeight += extraMajorPixelsForEachItem;
                }
                else
                {
                    EffectiveItemHeight += extraMinorPixelsForEachItem;
                    EffectiveItemWidth += extraMajorPixelsForEachItem;
                }
            }
        }

        internal void EnsureFirstElementOwnership(VirtualizingLayoutContext context)
        {
            if (_cachedFirstElement != null && FlowAlgorithm.GetElementIfRealized(0) != null)
            {
                context.RecycleElement(_cachedFirstElement);
                _cachedFirstElement = null;
            }
        }

        internal void ClearElementOnDataSourceChange(VirtualizingLayoutContext context, NotifyCollectionChangedEventArgs args)
        {
            if (_cachedFirstElement == null)
            {
                return;
            }

            var shouldClear = false;
            switch (args.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    shouldClear = args.NewStartingIndex == 0;
                    break;
                case NotifyCollectionChangedAction.Replace:
                    shouldClear = args.NewStartingIndex == 0 || args.OldStartingIndex == 0;
                    break;
                case NotifyCollectionChangedAction.Remove:
                    shouldClear = args.OldStartingIndex == 0;
                    break;
                case NotifyCollectionChangedAction.Reset:
                    shouldClear = true;
                    break;
                case NotifyCollectionChangedAction.Move:
                {
                    var moveCount = args.OldItems?.Count ?? args.NewItems?.Count ?? 1;
                    var oldTouchesFirst = args.OldStartingIndex >= 0 && args.OldStartingIndex <= 0 && args.OldStartingIndex + moveCount - 1 >= 0;
                    var newTouchesFirst = args.NewStartingIndex >= 0 && args.NewStartingIndex <= 0 && args.NewStartingIndex + moveCount - 1 >= 0;
                    shouldClear = oldTouchesFirst || newTouchesFirst;
                    break;
                }
            }

            if (shouldClear)
            {
                context.RecycleElement(_cachedFirstElement);
                _cachedFirstElement = null;
            }
        }
    }
}
