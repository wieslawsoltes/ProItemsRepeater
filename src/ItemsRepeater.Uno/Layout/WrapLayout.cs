using System;
using System.Collections.Specialized;
using Microsoft.UI.Xaml;

namespace Avalonia.Layout
{
    public class WrapLayout : VirtualizingLayout, IFlowLayoutAlgorithmDelegates
    {
        public static readonly DependencyProperty HorizontalSpacingProperty =
            DependencyProperty.Register(
                nameof(HorizontalSpacing),
                typeof(double),
                typeof(WrapLayout),
                new PropertyMetadata(0d, OnLayoutPropertyChanged));

        public static readonly DependencyProperty VerticalSpacingProperty =
            DependencyProperty.Register(
                nameof(VerticalSpacing),
                typeof(double),
                typeof(WrapLayout),
                new PropertyMetadata(0d, OnLayoutPropertyChanged));

        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register(
                nameof(Orientation),
                typeof(Orientation),
                typeof(WrapLayout),
                new PropertyMetadata(Orientation.Horizontal, OnLayoutPropertyChanged));

        private readonly OrientationBasedMeasures _orientation = new();

        public WrapLayout()
        {
            LayoutId = "WrapLayout";
            UpdateScrollOrientation();
        }

        public double HorizontalSpacing
        {
            get => (double)GetValue(HorizontalSpacingProperty);
            set => SetValue(HorizontalSpacingProperty, value);
        }

        public double VerticalSpacing
        {
            get => (double)GetValue(VerticalSpacingProperty);
            set => SetValue(VerticalSpacingProperty, value);
        }

        public Orientation Orientation
        {
            get => (Orientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        protected internal override void InitializeForContextCore(VirtualizingLayoutContext context)
        {
            var state = context.LayoutState as WrapLayoutState;
            if (state == null)
            {
                if (context.LayoutState != null)
                {
                    throw new InvalidOperationException("LayoutState must derive from WrapLayoutState.");
                }

                state = new WrapLayoutState();
            }

            state.InitializeForContext(context, this);
        }

        protected internal override void UninitializeForContextCore(VirtualizingLayoutContext context)
        {
            ((WrapLayoutState)context.LayoutState!).UninitializeForContext(context);
        }

        protected internal override void OnItemsChangedCore(VirtualizingLayoutContext context, object? source, NotifyCollectionChangedEventArgs args)
        {
            var state = (WrapLayoutState)context.LayoutState!;
            state.FlowAlgorithm.OnItemsSourceChanged(source, args, context);
            state.ClearLineStats();
            state.ClearItemStats();
            InvalidateLayout();
        }

        protected internal override Size MeasureOverride(VirtualizingLayoutContext context, Size availableSize)
        {
            var state = (WrapLayoutState)context.LayoutState!;
            var spacing = new UvMeasure(Orientation, HorizontalSpacing, VerticalSpacing);
            var parentMeasure = new UvMeasure(Orientation, availableSize.Width, availableSize.Height);
            var realizationBounds = new UvBounds(Orientation, context.RealizationRect);
            if (double.IsInfinity(parentMeasure.U))
            {
                var viewportU = realizationBounds.UMax - realizationBounds.UMin;
                if (!double.IsInfinity(viewportU) && viewportU > 0)
                {
                    parentMeasure.U = viewportU;
                }
            }

            if (state.FlowAlgorithm.HasInvalidMeasure())
            {
                state.ClearLineStats();
            }

            state.EnsureParameters(Orientation, spacing, parentMeasure.U);
            state.EnsureItemCount(context.ItemCount);
            state.BeginMeasure();

            var adjustedMeasure = new UvMeasure(Orientation, availableSize.Width, availableSize.Height);
            if (double.IsInfinity(adjustedMeasure.U))
            {
                adjustedMeasure.U = parentMeasure.U;
            }

            var layoutAvailableSize = Orientation == Orientation.Horizontal
                ? new Size(adjustedMeasure.U, adjustedMeasure.V)
                : new Size(adjustedMeasure.V, adjustedMeasure.U);

            var desiredSize = state.FlowAlgorithm.Measure(layoutAvailableSize, context, true, spacing.U, spacing.V, int.MaxValue, _orientation.ScrollOrientation, false, LayoutId);
            return new Size(desiredSize.Width, desiredSize.Height);
        }

        protected internal override Size ArrangeOverride(VirtualizingLayoutContext context, Size finalSize)
        {
            var value = ((WrapLayoutState)context.LayoutState!).FlowAlgorithm.Arrange(finalSize, context, true, FlowLayoutAlgorithm.LineAlignment.Start, LayoutId);
            return new Size(value.Width, value.Height);
        }

        Size IFlowLayoutAlgorithmDelegates.Algorithm_GetMeasureSize(int index, Size availableSize, VirtualizingLayoutContext context)
        {
            _ = index;
            _ = context;
            return availableSize;
        }

        Size IFlowLayoutAlgorithmDelegates.Algorithm_GetProvisionalArrangeSize(int index, Size measureSize, Size desiredSize, VirtualizingLayoutContext context)
        {
            _ = index;
            _ = measureSize;
            _ = context;
            return desiredSize;
        }

        bool IFlowLayoutAlgorithmDelegates.Algorithm_ShouldBreakLine(int index, double remainingSpace)
        {
            _ = index;
            return remainingSpace < 0;
        }

        FlowLayoutAnchorInfo IFlowLayoutAlgorithmDelegates.Algorithm_GetAnchorForRealizationRect(Size availableSize, VirtualizingLayoutContext context)
            => GetAnchorForRealizationRect(availableSize, context);

        FlowLayoutAnchorInfo IFlowLayoutAlgorithmDelegates.Algorithm_GetAnchorForTargetElement(int targetIndex, Size availableSize, VirtualizingLayoutContext context)
            => GetAnchorForTargetElement(targetIndex, availableSize, context);

        Rect IFlowLayoutAlgorithmDelegates.Algorithm_GetExtent(Size availableSize, VirtualizingLayoutContext context, Layoutable? firstRealized, int firstRealizedItemIndex, Rect firstRealizedLayoutBounds, Layoutable? lastRealized, int lastRealizedItemIndex, Rect lastRealizedLayoutBounds)
            => GetExtent(availableSize, context, firstRealized, firstRealizedItemIndex, firstRealizedLayoutBounds, lastRealized, lastRealizedItemIndex, lastRealizedLayoutBounds);

        void IFlowLayoutAlgorithmDelegates.Algorithm_OnElementMeasured(Layoutable element, int index, Size availableSize, Size measureSize, Size desiredSize, Size provisionalArrangeSize, VirtualizingLayoutContext context)
        {
            _ = element;
            _ = availableSize;
            _ = measureSize;
            _ = provisionalArrangeSize;
            ((WrapLayoutState)context.LayoutState!).RecordItemSize(index, _orientation.Minor(desiredSize), _orientation.Major(desiredSize));
        }

        void IFlowLayoutAlgorithmDelegates.Algorithm_OnLineArranged(int startIndex, int countInLine, double lineSize, VirtualizingLayoutContext context)
        {
            var state = (WrapLayoutState)context.LayoutState!;
            if (state.FlowAlgorithm.TryGetLayoutBoundsForDataIndex(startIndex, out var bounds))
            {
                state.UpdateLineCache(startIndex, countInLine, _orientation.MajorStart(bounds), lineSize);
            }
        }

        private FlowLayoutAnchorInfo GetAnchorForRealizationRect(Size availableSize, VirtualizingLayoutContext context)
        {
            var anchorIndex = -1;
            var offset = double.NaN;
            var itemsCount = context.ItemCount;

            if (itemsCount > 0)
            {
                var state = (WrapLayoutState)context.LayoutState!;
                var lastExtent = state.FlowAlgorithm.LastExtent;
                var viewportMin = _orientation.MajorStart(context.RealizationRect);
                var realizationOffsetInExtent = viewportMin - _orientation.MajorStart(lastExtent);
                if (realizationOffsetInExtent <= 1)
                {
                    return new FlowLayoutAnchorInfo
                    {
                        Index = 0,
                        Offset = _orientation.MajorStart(lastExtent),
                    };
                }

                var allowLineCache = !state.FlowAlgorithm.LastMeasureRealizationWindowJumped;
                if (allowLineCache && state.TryGetLineNearViewport(viewportMin, out var cachedStartIndex, out _, out var cachedLinePosition, out _))
                {
                    anchorIndex = Math.Max(0, Math.Min(itemsCount - 1, cachedStartIndex));
                    offset = cachedLinePosition;
                }
                else if (allowLineCache && state.TryGetNearestLine(viewportMin, out var nearestStartIndex, out var nearestLineItems, out var nearestLinePosition, out var nearestLineSize))
                {
                    var lineSpacing = GetLineSpacing();
                    var lineAdvance = nearestLineSize + lineSpacing;
                    var itemsPerLine = Math.Max(1, Math.Min(nearestLineItems, itemsCount));
                    if (lineAdvance > 0 && itemsPerLine > 0)
                    {
                        var delta = viewportMin - nearestLinePosition;
                        var nearLineThreshold = lineAdvance * 2;
                        if (Math.Abs(delta) <= nearLineThreshold)
                        {
                            anchorIndex = Math.Max(0, Math.Min(itemsCount - 1, nearestStartIndex));
                            offset = nearestLinePosition;
                        }
                        else
                        {
                            var deltaLines = delta >= 0 ? (int)Math.Floor(delta / lineAdvance) : (int)Math.Ceiling(delta / lineAdvance);
                            anchorIndex = Math.Max(0, Math.Min(itemsCount - 1, nearestStartIndex + (deltaLines * itemsPerLine)));
                            offset = nearestLinePosition + (deltaLines * lineAdvance);
                        }
                    }
                }
                else if (TryGetLineMetrics(availableSize, context, state, out var lineAdvance, out var itemsPerLine))
                {
                    var realizationRect = context.RealizationRect;
                    var realizationOffset = _orientation.MajorStart(realizationRect) - _orientation.MajorStart(lastExtent);
                    var estimatedTotalSize = GetEstimatedTotalSize(itemsCount, itemsPerLine, lineAdvance);
                    var clampedOffset = estimatedTotalSize > 0 ? Math.Max(0.0, Math.Min(estimatedTotalSize, realizationOffset)) : 0.0;

                    var lineIndex = lineAdvance > 0 ? (int)Math.Floor(clampedOffset / lineAdvance) : 0;
                    lineIndex = Math.Max(0, lineIndex);
                    anchorIndex = Math.Max(0, Math.Min(itemsCount - 1, lineIndex * itemsPerLine));
                    offset = _orientation.MajorStart(lastExtent) + (lineIndex * lineAdvance);
                }
            }

            return new FlowLayoutAnchorInfo { Index = anchorIndex, Offset = offset };
        }

        private FlowLayoutAnchorInfo GetAnchorForTargetElement(int targetIndex, Size availableSize, VirtualizingLayoutContext context)
        {
            var index = -1;
            var offset = double.NaN;
            var itemsCount = context.ItemCount;

            if (targetIndex >= 0 && targetIndex < itemsCount)
            {
                var state = (WrapLayoutState)context.LayoutState!;
                if (state.FlowAlgorithm.TryGetLayoutBoundsForDataIndex(targetIndex, out var bounds))
                {
                    var lineOffset = _orientation.MajorStart(bounds);
                    var startIndex = targetIndex;
                    while (startIndex > 0 &&
                        state.FlowAlgorithm.TryGetLayoutBoundsForDataIndex(startIndex - 1, out var prevBounds) &&
                        _orientation.MajorStart(prevBounds) == lineOffset)
                    {
                        startIndex--;
                    }

                    index = startIndex;
                    offset = lineOffset;
                }
                else if (state.TryGetLineForItemIndex(targetIndex, out var lineStartIndex, out _, out var linePosition, out _))
                {
                    index = lineStartIndex;
                    offset = linePosition;
                }
                else if (TryGetLineMetrics(availableSize, context, state, out var lineAdvance, out var itemsPerLine))
                {
                    var lineIndex = targetIndex / itemsPerLine;
                    index = Math.Max(0, Math.Min(itemsCount - 1, lineIndex * itemsPerLine));
                    offset = _orientation.MajorStart(state.FlowAlgorithm.LastExtent) + (lineIndex * lineAdvance);
                }
            }

            return new FlowLayoutAnchorInfo { Index = index, Offset = offset };
        }

        private Rect GetExtent(Size availableSize, VirtualizingLayoutContext context, Layoutable? firstRealized, int firstRealizedItemIndex, Rect firstRealizedLayoutBounds, Layoutable? lastRealized, int lastRealizedItemIndex, Rect lastRealizedLayoutBounds)
        {
            _ = firstRealized;
            _ = firstRealizedItemIndex;
            _ = firstRealizedLayoutBounds;
            _ = lastRealized;
            _ = lastRealizedItemIndex;
            _ = lastRealizedLayoutBounds;

            var extent = new Rect();
            var itemsCount = context.ItemCount;
            if (itemsCount == 0)
            {
                return extent;
            }

            var state = (WrapLayoutState)context.LayoutState!;
            if (!TryGetLineMetrics(availableSize, context, state, out var lineAdvance, out var itemsPerLine))
            {
                return extent;
            }

            var lineSpacing = GetLineSpacing();
            var lineCount = (int)Math.Ceiling((double)itemsCount / itemsPerLine);
            var majorSize = Math.Max(0.0, lineCount * lineAdvance - lineSpacing);
            var availableMinor = _orientation.Minor(availableSize);
            var minorSize = double.IsInfinity(availableMinor) ? 0.0 : availableMinor;

            _orientation.SetMinorSize(ref extent, minorSize);
            _orientation.SetMajorSize(ref extent, majorSize);
            return extent;
        }

        private bool TryGetLineMetrics(Size availableSize, VirtualizingLayoutContext context, WrapLayoutState state, out double lineAdvance, out int itemsPerLine)
        {
            var spacing = new UvMeasure(Orientation, HorizontalSpacing, VerticalSpacing);
            var minItemSpacing = spacing.U;
            var lineSpacing = spacing.V;

            if (state.TryGetAverageLineMetrics(out var averageLineSize, out var averageItemsPerLine))
            {
                lineAdvance = averageLineSize + lineSpacing;
                itemsPerLine = Math.Max(1, (int)Math.Round(averageItemsPerLine));
                itemsPerLine = Math.Min(itemsPerLine, Math.Max(1, context.ItemCount));
                return lineAdvance > 0;
            }

            if (TryEnsureAverageItemMetrics(availableSize, context, state, out var averageMinor, out var averageMajor))
            {
                itemsPerLine = EstimateItemsPerLine(availableSize, averageMinor, minItemSpacing, context.ItemCount);
                lineAdvance = averageMajor + lineSpacing;
                return lineAdvance > 0;
            }

            itemsPerLine = 1;
            lineAdvance = 0;
            return false;
        }

        private bool TryEnsureAverageItemMetrics(Size availableSize, VirtualizingLayoutContext context, WrapLayoutState state, out double averageMinor, out double averageMajor)
        {
            if (state.TryGetAverageItemMetrics(out averageMinor, out averageMajor))
            {
                return true;
            }

            if (context.ItemCount <= 0)
            {
                return false;
            }

            var element = state.FlowAlgorithm.GetElementIfRealized(0);
            var created = false;
            if (element == null)
            {
                element = context.GetOrCreateElementAt(0, ElementRealizationOptions.ForceCreate | ElementRealizationOptions.SuppressAutoRecycle);
                created = true;
            }

            element.Measure(availableSize);
            var desiredSize = element.DesiredSize.ToAvalonia();
            state.RecordItemSize(0, _orientation.Minor(desiredSize), _orientation.Major(desiredSize));

            if (created)
            {
                context.RecycleElement(element);
            }

            return state.TryGetAverageItemMetrics(out averageMinor, out averageMajor);
        }

        private int EstimateItemsPerLine(Size availableSize, double averageMinor, double minItemSpacing, int itemCount)
        {
            if (itemCount <= 0)
            {
                return 0;
            }

            var availableMinor = _orientation.Minor(availableSize);
            if (double.IsInfinity(availableMinor))
            {
                return itemCount;
            }

            if (averageMinor <= 0)
            {
                return 1;
            }

            var itemSpace = averageMinor + minItemSpacing;
            var itemsPerLine = (int)Math.Max(1.0, Math.Floor((availableMinor + minItemSpacing) / itemSpace));
            return Math.Min(itemsPerLine, itemCount);
        }

        private double GetEstimatedTotalSize(int itemCount, int itemsPerLine, double lineAdvance)
        {
            if (itemCount <= 0 || itemsPerLine <= 0 || lineAdvance <= 0)
            {
                return 0;
            }

            var lineCount = (int)Math.Ceiling((double)itemCount / itemsPerLine);
            if (lineCount <= 0)
            {
                return 0;
            }

            return Math.Max(0.0, lineCount * lineAdvance - GetLineSpacing());
        }

        private double GetLineSpacing()
        {
            return Orientation == Orientation.Horizontal ? VerticalSpacing : HorizontalSpacing;
        }

        private void UpdateScrollOrientation()
        {
            _orientation.ScrollOrientation = Orientation == Orientation.Horizontal ? ScrollOrientation.Vertical : ScrollOrientation.Horizontal;
        }

        private void InvalidateLayout() => InvalidateMeasure();

        private void OnLayoutPropertyChanged(DependencyProperty property)
        {
            if (property == OrientationProperty)
            {
                UpdateScrollOrientation();
            }

            InvalidateLayout();
        }

        private static void OnLayoutPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            ((WrapLayout)sender).OnLayoutPropertyChanged(args.Property);
        }
    }
}
