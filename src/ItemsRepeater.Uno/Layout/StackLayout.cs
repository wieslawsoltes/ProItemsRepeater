using System;
using System.Collections.Specialized;
using Microsoft.UI.Xaml;

namespace Avalonia.Layout
{
    public class StackLayout : VirtualizingLayout, IFlowLayoutAlgorithmDelegates
    {
        public static readonly DependencyProperty DisableVirtualizationProperty =
            DependencyProperty.Register(
                nameof(DisableVirtualization),
                typeof(bool),
                typeof(StackLayout),
                new PropertyMetadata(false, OnLayoutPropertyChanged));

        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register(
                nameof(Orientation),
                typeof(Orientation),
                typeof(StackLayout),
                new PropertyMetadata(Orientation.Vertical, OnLayoutPropertyChanged));

        public static readonly DependencyProperty SpacingProperty =
            DependencyProperty.Register(
                nameof(Spacing),
                typeof(double),
                typeof(StackLayout),
                new PropertyMetadata(0d, OnLayoutPropertyChanged));

        private readonly OrientationBasedMeasures _orientation = new();

        public StackLayout()
        {
            LayoutId = "StackLayout";
            UpdateOrientationState(Orientation.Vertical);
        }

        public bool DisableVirtualization
        {
            get => (bool)GetValue(DisableVirtualizationProperty);
            set => SetValue(DisableVirtualizationProperty, value);
        }

        public Orientation Orientation
        {
            get => (Orientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        public double Spacing
        {
            get => (double)GetValue(SpacingProperty);
            set => SetValue(SpacingProperty, value);
        }

        internal Rect GetExtent(
            Size availableSize,
            VirtualizingLayoutContext context,
            Layoutable? firstRealized,
            int firstRealizedItemIndex,
            Rect firstRealizedLayoutBounds,
            Layoutable? lastRealized,
            int lastRealizedItemIndex,
            Rect lastRealizedLayoutBounds)
        {
            var extent = new Rect();
            var itemsCount = context.ItemCount;
            var stackState = (StackLayoutState)context.LayoutState!;
            var estimatedSize = Math.Max(1.0, GetAverageElementSize(availableSize, context, stackState));
            var estimatedElementWithSpacing = estimatedSize + Spacing;

            _orientation.SetMinorSize(ref extent, stackState.MaxArrangeBounds);
            _orientation.SetMajorSize(ref extent, Math.Max(0.0f, stackState.GetEstimatedTotalSize(estimatedSize, Spacing, itemsCount)));
            if (itemsCount > 0)
            {
                if (firstRealized != null)
                {
                    var estimatedOffset = stackState.GetEstimatedOffsetForIndex(firstRealizedItemIndex, estimatedSize, Spacing);
                    _orientation.SetMajorStart(ref extent, _orientation.MajorStart(firstRealizedLayoutBounds) - estimatedOffset);
                    _orientation.SetMajorSize(
                        ref extent,
                        Math.Max(
                            _orientation.MajorSize(extent),
                            _orientation.MajorEnd(lastRealizedLayoutBounds) - _orientation.MajorStart(extent)));
                }
            }

            _ = estimatedElementWithSpacing;
            return extent;
        }

        internal void OnElementMeasured(Layoutable element, int index, Size availableSize, Size measureSize, Size desiredSize, Size provisionalArrangeSize, VirtualizingLayoutContext context)
        {
            _ = element;
            _ = availableSize;
            _ = measureSize;
            _ = desiredSize;
            var stackState = (StackLayoutState)context.LayoutState!;
            stackState.OnElementMeasured(index, _orientation.Major(provisionalArrangeSize), _orientation.Minor(provisionalArrangeSize));
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
            _ = context;
            var measureSizeMinor = _orientation.Minor(measureSize);
            return _orientation.MinorMajorSize(
                !double.IsInfinity(measureSizeMinor) ? Math.Max(measureSizeMinor, _orientation.Minor(desiredSize)) : _orientation.Minor(desiredSize),
                _orientation.Major(desiredSize));
        }

        bool IFlowLayoutAlgorithmDelegates.Algorithm_ShouldBreakLine(int index, double remainingSpace)
        {
            _ = index;
            _ = remainingSpace;
            return true;
        }

        FlowLayoutAnchorInfo IFlowLayoutAlgorithmDelegates.Algorithm_GetAnchorForRealizationRect(Size availableSize, VirtualizingLayoutContext context)
            => GetAnchorForRealizationRect(availableSize, context);

        FlowLayoutAnchorInfo IFlowLayoutAlgorithmDelegates.Algorithm_GetAnchorForTargetElement(int targetIndex, Size availableSize, VirtualizingLayoutContext context)
        {
            var offset = double.NaN;
            var index = -1;
            var itemsCount = context.ItemCount;

            if (targetIndex >= 0 && targetIndex < itemsCount)
            {
                index = targetIndex;
                var state = (StackLayoutState)context.LayoutState!;
                if (state.FlowAlgorithm.TryGetLayoutBoundsForDataIndex(targetIndex, out var bounds))
                {
                    offset = _orientation.MajorStart(bounds);
                }
                else
                {
                    var estimatedSize = GetAverageElementSize(availableSize, context, state);
                    var estimatedOffset = state.GetEstimatedOffsetForIndex(index, Math.Max(1.0, estimatedSize), Spacing);
                    offset = estimatedOffset + _orientation.MajorStart(state.FlowAlgorithm.LastExtent);
                }
            }

            return new FlowLayoutAnchorInfo { Index = index, Offset = offset };
        }

        Rect IFlowLayoutAlgorithmDelegates.Algorithm_GetExtent(Size availableSize, VirtualizingLayoutContext context, Layoutable? firstRealized, int firstRealizedItemIndex, Rect firstRealizedLayoutBounds, Layoutable? lastRealized, int lastRealizedItemIndex, Rect lastRealizedLayoutBounds)
            => GetExtent(availableSize, context, firstRealized, firstRealizedItemIndex, firstRealizedLayoutBounds, lastRealized, lastRealizedItemIndex, lastRealizedLayoutBounds);

        void IFlowLayoutAlgorithmDelegates.Algorithm_OnElementMeasured(Layoutable element, int index, Size availableSize, Size measureSize, Size desiredSize, Size provisionalArrangeSize, VirtualizingLayoutContext context)
            => OnElementMeasured(element, index, availableSize, measureSize, desiredSize, provisionalArrangeSize, context);

        void IFlowLayoutAlgorithmDelegates.Algorithm_OnLineArranged(int startIndex, int countInLine, double lineSize, VirtualizingLayoutContext context)
        {
            _ = startIndex;
            _ = countInLine;
            _ = lineSize;
            _ = context;
        }

        internal FlowLayoutAnchorInfo GetAnchorForRealizationRect(Size availableSize, VirtualizingLayoutContext context)
        {
            var anchorIndex = -1;
            var offset = double.NaN;
            var itemsCount = context.ItemCount;
            if (itemsCount > 0)
            {
                var realizationRect = context.RealizationRect;
                var state = (StackLayoutState)context.LayoutState!;
                var lastExtent = state.FlowAlgorithm.LastExtent;

                var estimatedSize = GetAverageElementSize(availableSize, context, state);
                var estimatedItemSize = Math.Max(1.0, estimatedSize);
                var spacing = Spacing;
                var realizationWindowOffsetInExtent = _orientation.MajorStart(realizationRect) - _orientation.MajorStart(lastExtent);
                var estimatedTotalSize = state.GetEstimatedTotalSize(estimatedItemSize, spacing, itemsCount);
                var clampedOffset = estimatedTotalSize > 0 ? Math.Max(0.0, Math.Min(estimatedTotalSize, realizationWindowOffsetInExtent)) : 0.0;

                anchorIndex = state.EstimateIndexForOffset(clampedOffset, estimatedItemSize, spacing, itemsCount);
                anchorIndex = Math.Max(0, Math.Min(itemsCount - 1, anchorIndex));
                offset = _orientation.MajorStart(realizationRect);
            }

            return new FlowLayoutAnchorInfo { Index = anchorIndex, Offset = offset };
        }

        protected internal override void InitializeForContextCore(VirtualizingLayoutContext context)
        {
            var stackState = context.LayoutState as StackLayoutState;
            if (stackState == null)
            {
                if (context.LayoutState != null)
                {
                    throw new InvalidOperationException("LayoutState must derive from StackLayoutState.");
                }

                stackState = new StackLayoutState();
            }

            stackState.InitializeForContext(context, this);
        }

        protected internal override void UninitializeForContextCore(VirtualizingLayoutContext context)
        {
            ((StackLayoutState)context.LayoutState!).UninitializeForContext(context);
        }

        protected internal override Size MeasureOverride(VirtualizingLayoutContext context, Size availableSize)
        {
            var stackState = (StackLayoutState)context.LayoutState!;
            stackState.EnsureLineCacheParameters(Orientation, Spacing);
            stackState.EnsureItemCount(context.ItemCount);
            stackState.ClearLineCache();
            stackState.OnMeasureStart();

            return GetFlowAlgorithm(context).Measure(availableSize, context, false, 0, Spacing, int.MaxValue, _orientation.ScrollOrientation, DisableVirtualization, LayoutId);
        }

        protected internal override Size ArrangeOverride(VirtualizingLayoutContext context, Size finalSize)
        {
            return GetFlowAlgorithm(context).Arrange(finalSize, context, false, FlowLayoutAlgorithm.LineAlignment.Start, LayoutId);
        }

        protected internal override void OnItemsChangedCore(VirtualizingLayoutContext context, object? source, NotifyCollectionChangedEventArgs args)
        {
            GetFlowAlgorithm(context).OnItemsSourceChanged(source, args, context);
            ((StackLayoutState)context.LayoutState!).ClearLineCache();
            ((StackLayoutState)context.LayoutState!).ClearSizeCache();
            InvalidateLayout();
        }

        private static double GetAverageElementSize(Size availableSize, VirtualizingLayoutContext context, StackLayoutState stackLayoutState)
        {
            var averageElementSize = 0.0;

            if (context.ItemCount > 0)
            {
                if (stackLayoutState.TryGetMeasuredAverage(out var measuredAverage))
                {
                    return measuredAverage;
                }

                if (stackLayoutState.TotalElementsMeasured == 0)
                {
                    var tmpElement = context.GetOrCreateElementAt(0, ElementRealizationOptions.ForceCreate | ElementRealizationOptions.SuppressAutoRecycle);
                    stackLayoutState.FlowAlgorithm.MeasureElement(tmpElement, 0, availableSize, context);
                    context.RecycleElement(tmpElement);
                }

                averageElementSize = stackLayoutState.TotalElementSize / stackLayoutState.TotalElementsMeasured;
            }

            return averageElementSize;
        }

        private void InvalidateLayout() => InvalidateMeasure();

        private void UpdateOrientationState(Orientation orientation)
        {
            _orientation.ScrollOrientation = orientation == Orientation.Horizontal ? ScrollOrientation.Horizontal : ScrollOrientation.Vertical;
        }

        private void OnLayoutPropertyChanged(DependencyProperty property, object? newValue)
        {
            if (property == OrientationProperty && newValue is Orientation orientation)
            {
                UpdateOrientationState(orientation);
            }

            InvalidateLayout();
        }

        private static void OnLayoutPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            ((StackLayout)sender).OnLayoutPropertyChanged(args.Property, args.NewValue);
        }

        private static FlowLayoutAlgorithm GetFlowAlgorithm(VirtualizingLayoutContext context) => ((StackLayoutState)context.LayoutState!).FlowAlgorithm;
    }
}
