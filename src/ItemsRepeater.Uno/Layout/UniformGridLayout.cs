using System;
using System.Collections.Specialized;
using Microsoft.UI.Xaml;

namespace Avalonia.Layout
{
    public class UniformGridLayout : VirtualizingLayout, IFlowLayoutAlgorithmDelegates
    {
        public static readonly DependencyProperty ItemsJustificationProperty =
            DependencyProperty.Register(
                nameof(ItemsJustification),
                typeof(UniformGridLayoutItemsJustification),
                typeof(UniformGridLayout),
                new PropertyMetadata(UniformGridLayoutItemsJustification.Start, OnLayoutPropertyChanged));

        public static readonly DependencyProperty ItemsStretchProperty =
            DependencyProperty.Register(
                nameof(ItemsStretch),
                typeof(UniformGridLayoutItemsStretch),
                typeof(UniformGridLayout),
                new PropertyMetadata(UniformGridLayoutItemsStretch.None, OnLayoutPropertyChanged));

        public static readonly DependencyProperty MinColumnSpacingProperty =
            DependencyProperty.Register(
                nameof(MinColumnSpacing),
                typeof(double),
                typeof(UniformGridLayout),
                new PropertyMetadata(0d, OnLayoutPropertyChanged));

        public static readonly DependencyProperty MinItemHeightProperty =
            DependencyProperty.Register(
                nameof(MinItemHeight),
                typeof(double),
                typeof(UniformGridLayout),
                new PropertyMetadata(double.NaN, OnLayoutPropertyChanged));

        public static readonly DependencyProperty MinItemWidthProperty =
            DependencyProperty.Register(
                nameof(MinItemWidth),
                typeof(double),
                typeof(UniformGridLayout),
                new PropertyMetadata(double.NaN, OnLayoutPropertyChanged));

        public static readonly DependencyProperty MinRowSpacingProperty =
            DependencyProperty.Register(
                nameof(MinRowSpacing),
                typeof(double),
                typeof(UniformGridLayout),
                new PropertyMetadata(0d, OnLayoutPropertyChanged));

        public static readonly DependencyProperty MaximumRowsOrColumnsProperty =
            DependencyProperty.Register(
                nameof(MaximumRowsOrColumns),
                typeof(int),
                typeof(UniformGridLayout),
                new PropertyMetadata(int.MaxValue, OnLayoutPropertyChanged));

        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register(
                nameof(Orientation),
                typeof(Orientation),
                typeof(UniformGridLayout),
                new PropertyMetadata(Orientation.Horizontal, OnLayoutPropertyChanged));

        private readonly OrientationBasedMeasures _orientation = new();
        private double _minItemWidth = double.NaN;
        private double _minItemHeight = double.NaN;
        private double _minRowSpacing;
        private double _minColumnSpacing;
        private UniformGridLayoutItemsJustification _itemsJustification;
        private UniformGridLayoutItemsStretch _itemsStretch;
        private int _maximumRowsOrColumns = int.MaxValue;

        public UniformGridLayout()
        {
            LayoutId = "UniformGridLayout";
            UpdateOrientationState(Orientation.Horizontal);
        }

        public UniformGridLayoutItemsJustification ItemsJustification
        {
            get => (UniformGridLayoutItemsJustification)GetValue(ItemsJustificationProperty);
            set => SetValue(ItemsJustificationProperty, value);
        }

        public UniformGridLayoutItemsStretch ItemsStretch
        {
            get => (UniformGridLayoutItemsStretch)GetValue(ItemsStretchProperty);
            set => SetValue(ItemsStretchProperty, value);
        }

        public double MinColumnSpacing
        {
            get => (double)GetValue(MinColumnSpacingProperty);
            set => SetValue(MinColumnSpacingProperty, value);
        }

        public double MinItemHeight
        {
            get => (double)GetValue(MinItemHeightProperty);
            set => SetValue(MinItemHeightProperty, value);
        }

        public double MinItemWidth
        {
            get => (double)GetValue(MinItemWidthProperty);
            set => SetValue(MinItemWidthProperty, value);
        }

        public double MinRowSpacing
        {
            get => (double)GetValue(MinRowSpacingProperty);
            set => SetValue(MinRowSpacingProperty, value);
        }

        public int MaximumRowsOrColumns
        {
            get => (int)GetValue(MaximumRowsOrColumnsProperty);
            set => SetValue(MaximumRowsOrColumnsProperty, value);
        }

        public Orientation Orientation
        {
            get => (Orientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        internal double LineSpacing => Orientation == Orientation.Horizontal ? _minRowSpacing : _minColumnSpacing;

        internal double MinItemSpacing => Orientation == Orientation.Horizontal ? _minColumnSpacing : _minRowSpacing;

        Size IFlowLayoutAlgorithmDelegates.Algorithm_GetMeasureSize(int index, Size availableSize, VirtualizingLayoutContext context)
        {
            _ = index;
            var gridState = (UniformGridLayoutState)context.LayoutState!;
            return new Size(gridState.EffectiveItemWidth, gridState.EffectiveItemHeight);
        }

        Size IFlowLayoutAlgorithmDelegates.Algorithm_GetProvisionalArrangeSize(int index, Size measureSize, Size desiredSize, VirtualizingLayoutContext context)
        {
            _ = index;
            _ = measureSize;
            _ = desiredSize;
            var gridState = (UniformGridLayoutState)context.LayoutState!;
            return new Size(gridState.EffectiveItemWidth, gridState.EffectiveItemHeight);
        }

        bool IFlowLayoutAlgorithmDelegates.Algorithm_ShouldBreakLine(int index, double remainingSpace)
        {
            _ = index;
            return remainingSpace < 0;
        }

        FlowLayoutAnchorInfo IFlowLayoutAlgorithmDelegates.Algorithm_GetAnchorForRealizationRect(Size availableSize, VirtualizingLayoutContext context)
        {
            var bounds = new Rect(double.NaN, double.NaN, double.NaN, double.NaN);
            var anchorIndex = -1;
            var itemsCount = context.ItemCount;
            var realizationRect = context.RealizationRect;
            if (itemsCount > 0 && _orientation.MajorSize(realizationRect) > 0)
            {
                var gridState = (UniformGridLayoutState)context.LayoutState!;
                var lastExtent = gridState.FlowAlgorithm.LastExtent;
                var itemsPerLine = Math.Min(Math.Max(1u, (uint)(_orientation.Minor(availableSize) / GetMinorSizeWithSpacing(context))), Math.Max(1u, (uint)_maximumRowsOrColumns));
                var majorSize = (itemsCount / itemsPerLine) * GetMajorSizeWithSpacing(context);
                var realizationWindowStartWithinExtent = _orientation.MajorStart(realizationRect) - _orientation.MajorStart(lastExtent);
                if ((realizationWindowStartWithinExtent + _orientation.MajorSize(realizationRect)) >= 0 && realizationWindowStartWithinExtent <= majorSize)
                {
                    var offset = Math.Max(0.0, _orientation.MajorStart(realizationRect) - _orientation.MajorStart(lastExtent));
                    var anchorRowIndex = (int)(offset / GetMajorSizeWithSpacing(context));

                    anchorIndex = (int)Math.Max(0, Math.Min(itemsCount - 1, anchorRowIndex * itemsPerLine));
                    bounds = GetLayoutRectForDataIndex(availableSize, anchorIndex, lastExtent, context);
                }
            }

            return new FlowLayoutAnchorInfo
            {
                Index = anchorIndex,
                Offset = _orientation.MajorStart(bounds),
            };
        }

        FlowLayoutAnchorInfo IFlowLayoutAlgorithmDelegates.Algorithm_GetAnchorForTargetElement(int targetIndex, Size availableSize, VirtualizingLayoutContext context)
        {
            var index = -1;
            var offset = double.NaN;
            var count = context.ItemCount;
            if (targetIndex >= 0 && targetIndex < count)
            {
                var itemsPerLine = (int)Math.Min(Math.Max(1u, (uint)(_orientation.Minor(availableSize) / GetMinorSizeWithSpacing(context))), Math.Max(1u, _maximumRowsOrColumns));
                var indexOfFirstInLine = (targetIndex / itemsPerLine) * itemsPerLine;
                index = indexOfFirstInLine;
                var state = (UniformGridLayoutState)context.LayoutState!;
                offset = _orientation.MajorStart(GetLayoutRectForDataIndex(availableSize, indexOfFirstInLine, state.FlowAlgorithm.LastExtent, context));
            }

            return new FlowLayoutAnchorInfo
            {
                Index = index,
                Offset = offset,
            };
        }

        Rect IFlowLayoutAlgorithmDelegates.Algorithm_GetExtent(Size availableSize, VirtualizingLayoutContext context, Layoutable? firstRealized, int firstRealizedItemIndex, Rect firstRealizedLayoutBounds, Layoutable? lastRealized, int lastRealizedItemIndex, Rect lastRealizedLayoutBounds)
        {
            var extent = new Rect();
            var itemsCount = context.ItemCount;
            var availableSizeMinor = _orientation.Minor(availableSize);
            var itemsPerLine = (int)Math.Min(Math.Max(1u, !double.IsInfinity(availableSizeMinor) ? (uint)(availableSizeMinor / GetMinorSizeWithSpacing(context)) : (uint)itemsCount), Math.Max(1u, _maximumRowsOrColumns));
            var lineSize = GetMajorSizeWithSpacing(context);

            if (itemsCount > 0)
            {
                _orientation.SetMinorSize(ref extent, !double.IsInfinity(availableSizeMinor) && _itemsStretch == UniformGridLayoutItemsStretch.Fill ? availableSizeMinor : Math.Max(0.0, itemsPerLine * GetMinorSizeWithSpacing(context) - MinItemSpacing));
                _orientation.SetMajorSize(ref extent, Math.Max(0.0, (itemsCount / itemsPerLine) * lineSize - LineSpacing));

                if (firstRealized != null)
                {
                    _orientation.SetMajorStart(ref extent, _orientation.MajorStart(firstRealizedLayoutBounds) - (firstRealizedItemIndex / itemsPerLine) * lineSize);
                    var remainingItems = itemsCount - lastRealizedItemIndex - 1;
                    _orientation.SetMajorSize(ref extent, _orientation.MajorEnd(lastRealizedLayoutBounds) - _orientation.MajorStart(extent) + (remainingItems / itemsPerLine) * lineSize);
                }
            }

            return extent;
        }

        void IFlowLayoutAlgorithmDelegates.Algorithm_OnElementMeasured(Layoutable element, int index, Size availableSize, Size measureSize, Size desiredSize, Size provisionalArrangeSize, VirtualizingLayoutContext context)
        {
            _ = element;
            _ = index;
            _ = availableSize;
            _ = measureSize;
            _ = desiredSize;
            _ = provisionalArrangeSize;
            _ = context;
        }

        void IFlowLayoutAlgorithmDelegates.Algorithm_OnLineArranged(int startIndex, int countInLine, double lineSize, VirtualizingLayoutContext context)
        {
            _ = startIndex;
            _ = countInLine;
            _ = lineSize;
            _ = context;
        }

        protected internal override void InitializeForContextCore(VirtualizingLayoutContext context)
        {
            var gridState = context.LayoutState as UniformGridLayoutState;
            if (gridState == null)
            {
                if (context.LayoutState != null)
                {
                    throw new InvalidOperationException("LayoutState must derive from UniformGridLayoutState.");
                }

                gridState = new UniformGridLayoutState();
            }

            gridState.InitializeForContext(context, this);
        }

        protected internal override void UninitializeForContextCore(VirtualizingLayoutContext context)
        {
            ((UniformGridLayoutState)context.LayoutState!).UninitializeForContext(context);
        }

        protected internal override Size MeasureOverride(VirtualizingLayoutContext context, Size availableSize)
        {
            var gridState = (UniformGridLayoutState)context.LayoutState!;
            gridState.EnsureElementSize(availableSize, context, _minItemWidth, _minItemHeight, _itemsStretch, Orientation, MinRowSpacing, MinColumnSpacing, _maximumRowsOrColumns);

            var desiredSize = GetFlowAlgorithm(context).Measure(availableSize, context, true, MinItemSpacing, LineSpacing, _maximumRowsOrColumns, _orientation.ScrollOrientation, false, LayoutId);
            gridState.EnsureFirstElementOwnership(context);
            return desiredSize;
        }

        protected internal override Size ArrangeOverride(VirtualizingLayoutContext context, Size finalSize)
        {
            return GetFlowAlgorithm(context).Arrange(finalSize, context, true, (FlowLayoutAlgorithm.LineAlignment)_itemsJustification, LayoutId);
        }

        protected internal override void OnItemsChangedCore(VirtualizingLayoutContext context, object? source, NotifyCollectionChangedEventArgs args)
        {
            GetFlowAlgorithm(context).OnItemsSourceChanged(source, args, context);
            InvalidateLayout();
            ((UniformGridLayoutState)context.LayoutState!).ClearElementOnDataSourceChange(context, args);
        }

        private double GetMinorSizeWithSpacing(VirtualizingLayoutContext context)
        {
            var minItemSpacing = MinItemSpacing;
            var gridState = (UniformGridLayoutState)context.LayoutState!;
            return _orientation.ScrollOrientation == ScrollOrientation.Vertical
                ? gridState.EffectiveItemWidth + minItemSpacing
                : gridState.EffectiveItemHeight + minItemSpacing;
        }

        private double GetMajorSizeWithSpacing(VirtualizingLayoutContext context)
        {
            var lineSpacing = LineSpacing;
            var gridState = (UniformGridLayoutState)context.LayoutState!;
            return _orientation.ScrollOrientation == ScrollOrientation.Vertical
                ? gridState.EffectiveItemHeight + lineSpacing
                : gridState.EffectiveItemWidth + lineSpacing;
        }

        private Rect GetLayoutRectForDataIndex(Size availableSize, int index, Rect lastExtent, VirtualizingLayoutContext context)
        {
            var itemsPerLine = (int)Math.Min(Math.Max(1u, (uint)(_orientation.Minor(availableSize) / GetMinorSizeWithSpacing(context))), Math.Max(1u, _maximumRowsOrColumns));
            var rowIndex = index / itemsPerLine;
            var indexInRow = index - (rowIndex * itemsPerLine);

            var gridState = (UniformGridLayoutState)context.LayoutState!;
            return _orientation.MinorMajorRect(
                indexInRow * GetMinorSizeWithSpacing(context) + _orientation.MinorStart(lastExtent),
                rowIndex * GetMajorSizeWithSpacing(context) + _orientation.MajorStart(lastExtent),
                _orientation.ScrollOrientation == ScrollOrientation.Vertical ? gridState.EffectiveItemWidth : gridState.EffectiveItemHeight,
                _orientation.ScrollOrientation == ScrollOrientation.Vertical ? gridState.EffectiveItemHeight : gridState.EffectiveItemWidth);
        }

        private void InvalidateLayout() => InvalidateMeasure();

        private void UpdateOrientationState(Orientation orientation)
        {
            _orientation.ScrollOrientation = orientation == Orientation.Horizontal ? ScrollOrientation.Vertical : ScrollOrientation.Horizontal;
        }

        private void OnLayoutPropertyChanged(DependencyProperty property, object? newValue)
        {
            if (property == OrientationProperty && newValue is Orientation orientation)
            {
                UpdateOrientationState(orientation);
            }
            else if (property == MinColumnSpacingProperty && newValue is double minColumnSpacing)
            {
                _minColumnSpacing = minColumnSpacing;
            }
            else if (property == MinRowSpacingProperty && newValue is double minRowSpacing)
            {
                _minRowSpacing = minRowSpacing;
            }
            else if (property == ItemsJustificationProperty && newValue is UniformGridLayoutItemsJustification itemsJustification)
            {
                _itemsJustification = itemsJustification;
            }
            else if (property == ItemsStretchProperty && newValue is UniformGridLayoutItemsStretch itemsStretch)
            {
                _itemsStretch = itemsStretch;
            }
            else if (property == MinItemWidthProperty && newValue is double minItemWidth)
            {
                _minItemWidth = minItemWidth;
            }
            else if (property == MinItemHeightProperty && newValue is double minItemHeight)
            {
                _minItemHeight = minItemHeight;
            }
            else if (property == MaximumRowsOrColumnsProperty && newValue is int maximumRowsOrColumns)
            {
                _maximumRowsOrColumns = maximumRowsOrColumns;
            }

            InvalidateLayout();
        }

        private static void OnLayoutPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            ((UniformGridLayout)sender).OnLayoutPropertyChanged(args.Property, args.NewValue);
        }

        private static FlowLayoutAlgorithm GetFlowAlgorithm(VirtualizingLayoutContext context) => ((UniformGridLayoutState)context.LayoutState!).FlowAlgorithm;
    }
}
