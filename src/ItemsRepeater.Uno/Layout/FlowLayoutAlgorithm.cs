using System;
using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Logging;

namespace Avalonia.Layout
{
    internal class FlowLayoutAlgorithm
    {
        private readonly OrientationBasedMeasures _orientation = new();
        private readonly ElementManager _elementManager = new();
        private Size _lastAvailableSize;
        private double _lastItemSpacing;
        private bool _collectionChangePending;
        private VirtualizingLayoutContext? _context;
        private IFlowLayoutAlgorithmDelegates? _algorithmCallbacks;
        private Rect _lastExtent;
        private int _firstRealizedDataIndexInsideRealizationWindow = -1;
        private int _lastRealizedDataIndexInsideRealizationWindow = -1;
        private Rect _lastRealizationRect;
        private bool _hasValidRealizationRect;
        private bool _lastMeasureRealizationWindowJumped;
        private bool _scrollOrientationSameAsFlow;

        public Rect LastExtent => _lastExtent;

        internal bool LastMeasureRealizationWindowJumped => _lastMeasureRealizationWindowJumped;

        internal bool HasInvalidMeasure() => _elementManager.HasInvalidMeasure();

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

        private Rect RealizationRect => IsVirtualizingContext ? _context!.RealizationRect : new Rect(Size.Infinity);

        public void InitializeForContext(VirtualizingLayoutContext context, IFlowLayoutAlgorithmDelegates callbacks)
        {
            _algorithmCallbacks = callbacks;
            _context = context;
            _elementManager.SetContext(context);
        }

        public void UninitializeForContext(VirtualizingLayoutContext context)
        {
            if (IsVirtualizingContext)
            {
                _elementManager.ClearRealizedRange();
            }

            context.LayoutState = null;
        }

        public Size Measure(
            Size availableSize,
            VirtualizingLayoutContext context,
            bool isWrapping,
            double minItemSpacing,
            double lineSpacing,
            int maxItemsPerLine,
            ScrollOrientation orientation,
            bool disableVirtualization,
            string? layoutId)
        {
            _orientation.ScrollOrientation = orientation;
            _scrollOrientationSameAsFlow = double.IsInfinity(_orientation.Minor(availableSize));
            var realizationRect = RealizationRect;
            Logger.TryGet(LogEventLevel.Verbose, "Repeater")?.Log(this, "{LayoutId}: MeasureLayout Realization({Rect})", layoutId, realizationRect);

            var suggestedAnchorIndex = _context!.RecommendedAnchorIndex;
            if (_elementManager.IsIndexValidInData(suggestedAnchorIndex))
            {
                var anchorRealized = _elementManager.IsDataIndexRealized(suggestedAnchorIndex);
                if (!anchorRealized)
                {
                    var hasMadeAnchor = _context is RepeaterLayoutContext repeaterContext && repeaterContext.HasMadeAnchor;
                    if (!IsRealizationWindowJumped() || hasMadeAnchor)
                    {
                        MakeAnchor(_context, suggestedAnchorIndex, availableSize, layoutId);
                    }
                }
            }

            _elementManager.OnBeginMeasure(orientation);

            var anchorIndex = GetAnchorIndex(availableSize, isWrapping, minItemSpacing, layoutId);
            Generate(GenerateDirection.Forward, anchorIndex, availableSize, isWrapping, minItemSpacing, lineSpacing, maxItemsPerLine, disableVirtualization, layoutId);
            Generate(GenerateDirection.Backward, anchorIndex, availableSize, isWrapping, minItemSpacing, lineSpacing, maxItemsPerLine, disableVirtualization, layoutId);
            if (isWrapping && IsReflowRequired())
            {
                Logger.TryGet(LogEventLevel.Verbose, "Repeater")?.Log(this, "{LayoutId}: Reflow Pass", layoutId);
                var firstElementBounds = _elementManager.GetLayoutBoundsForRealizedIndex(0);
                _orientation.SetMinorStart(ref firstElementBounds, 0);
                _elementManager.SetLayoutBoundsForRealizedIndex(0, firstElementBounds);
                Generate(GenerateDirection.Forward, 0, availableSize, isWrapping, minItemSpacing, lineSpacing, maxItemsPerLine, disableVirtualization, layoutId);
            }

            RaiseLineArranged();
            _collectionChangePending = false;
            _lastExtent = EstimateExtent(availableSize, layoutId);
            SetLayoutOrigin();

            return new Size(_lastExtent.Width, _lastExtent.Height);
        }

        public Size Arrange(
            Size finalSize,
            VirtualizingLayoutContext context,
            bool isWrapping,
            LineAlignment lineAlignment,
            string? layoutId)
        {
            _ = context;
            Logger.TryGet(LogEventLevel.Verbose, "Repeater")?.Log(this, "{LayoutId}: ArrangeLayout", layoutId);
            ArrangeVirtualizingLayout(finalSize, lineAlignment, isWrapping, layoutId);

            return new Size(Math.Max(finalSize.Width, _lastExtent.Width), Math.Max(finalSize.Height, _lastExtent.Height));
        }

        public void OnItemsSourceChanged(object? source, NotifyCollectionChangedEventArgs args, VirtualizingLayoutContext context)
        {
            _elementManager.DataSourceChanged(source, args);
            _collectionChangePending = true;
            _ = context;
        }

        public Size MeasureElement(Layoutable element, int index, Size availableSize, VirtualizingLayoutContext context)
        {
            var measureSize = _algorithmCallbacks!.Algorithm_GetMeasureSize(index, availableSize, context);
            element.Measure(measureSize);
            var desiredSize = element.DesiredSize.ToAvalonia();
            var provisionalArrangeSize = _algorithmCallbacks.Algorithm_GetProvisionalArrangeSize(index, measureSize, desiredSize, context);
            _algorithmCallbacks.Algorithm_OnElementMeasured(element, index, availableSize, measureSize, desiredSize, provisionalArrangeSize, context);
            return provisionalArrangeSize;
        }

        private int GetAnchorIndex(Size availableSize, bool isWrapping, double minItemSpacing, string? layoutId)
        {
            var anchorIndex = -1;
            var anchorPosition = new Point();
            var context = _context;
            _lastMeasureRealizationWindowJumped = false;

            if (!IsVirtualizingContext)
            {
                anchorIndex = context!.ItemCount > 0 ? 0 : -1;
            }
            else
            {
                var isRealizationWindowJumped = IsRealizationWindowJumped();
                _lastMeasureRealizationWindowJumped = isRealizationWindowJumped;
                var isRealizationWindowConnected = _elementManager.IsWindowConnected(RealizationRect, _orientation.ScrollOrientation, _scrollOrientationSameAsFlow) && !isRealizationWindowJumped;
                var needAnchorColumnRevaluation = isWrapping &&
                    (_orientation.Minor(_lastAvailableSize) != _orientation.Minor(availableSize) ||
                     _lastItemSpacing != minItemSpacing ||
                     _collectionChangePending);

                var suggestedAnchorIndex = _context!.RecommendedAnchorIndex;
                var isAnchorSuggestionValid = suggestedAnchorIndex >= 0 && _elementManager.IsDataIndexRealized(suggestedAnchorIndex);
                var hasMadeAnchor = _context is RepeaterLayoutContext repeaterContext && repeaterContext.HasMadeAnchor;
                if (isRealizationWindowJumped && !hasMadeAnchor)
                {
                    isAnchorSuggestionValid = false;
                }

                if (isAnchorSuggestionValid)
                {
                    var anchorBounds = _elementManager.GetLayoutBoundsForDataIndex(suggestedAnchorIndex);
                    if (!anchorBounds.Intersects(RealizationRect))
                    {
                        Logger.TryGet(LogEventLevel.Verbose, "Repeater")?.Log(this, "{LayoutId}: Suggested anchor {Anchor} not in realization window", layoutId, suggestedAnchorIndex);
                        isAnchorSuggestionValid = false;
                    }
                }

                if (isAnchorSuggestionValid)
                {
                    Logger.TryGet(LogEventLevel.Verbose, "Repeater")?.Log(this, "{LayoutId}: Using suggested anchor {Anchor}", layoutId, suggestedAnchorIndex);
                    anchorIndex = _algorithmCallbacks!.Algorithm_GetAnchorForTargetElement(suggestedAnchorIndex, availableSize, context!).Index;

                    if (_elementManager.IsDataIndexRealized(anchorIndex))
                    {
                        var anchorBounds = _elementManager.GetLayoutBoundsForDataIndex(anchorIndex);
                        anchorPosition = needAnchorColumnRevaluation
                            ? _orientation.MinorMajorPoint(0, _orientation.MajorStart(anchorBounds))
                            : new Point(anchorBounds.X, anchorBounds.Y);
                    }
                    else if (anchorIndex >= 0)
                    {
                        var firstRealizedDataIndex = _elementManager.GetDataIndexFromRealizedRangeIndex(0);
                        for (var i = firstRealizedDataIndex - 1; i >= anchorIndex; --i)
                        {
                            _elementManager.EnsureElementRealized(false, i, layoutId);
                        }

                        var anchorBounds = _elementManager.GetLayoutBoundsForDataIndex(suggestedAnchorIndex);
                        anchorPosition = _orientation.MinorMajorPoint(0, _orientation.MajorStart(anchorBounds));
                    }
                }
                else if (needAnchorColumnRevaluation || !isRealizationWindowConnected)
                {
                    if (needAnchorColumnRevaluation)
                    {
                        Logger.TryGet(LogEventLevel.Verbose, "Repeater")?.Log(this, "{LayoutId}: NeedAnchorColumnReevaluation", layoutId);
                    }

                    if (!isRealizationWindowConnected)
                    {
                        Logger.TryGet(LogEventLevel.Verbose, "Repeater")?.Log(this, "{LayoutId}: Disconnected Window", layoutId);
                    }

                    var anchorInfo = _algorithmCallbacks!.Algorithm_GetAnchorForRealizationRect(availableSize, context!);
                    anchorIndex = anchorInfo.Index;
                    anchorPosition = _orientation.MinorMajorPoint(0, anchorInfo.Offset);
                }
                else
                {
                    Logger.TryGet(LogEventLevel.Verbose, "Repeater")?.Log(this, "{LayoutId}: Connected Window - picking first realized element as anchor", layoutId);
                    anchorIndex = _elementManager.GetDataIndexFromRealizedRangeIndex(0);
                    var firstElementBounds = _elementManager.GetLayoutBoundsForRealizedIndex(0);
                    anchorPosition = new Point(firstElementBounds.X, firstElementBounds.Y);
                }
            }

            Logger.TryGet(LogEventLevel.Verbose, "Repeater")?.Log(this, "{LayoutId}: Picked anchor: {Anchor}", layoutId, anchorIndex);
            _firstRealizedDataIndexInsideRealizationWindow = _lastRealizedDataIndexInsideRealizationWindow = anchorIndex;
            if (_elementManager.IsIndexValidInData(anchorIndex))
            {
                if (!_elementManager.IsDataIndexRealized(anchorIndex))
                {
                    Logger.TryGet(LogEventLevel.Verbose, "Repeater")?.Log(this, "{LayoutId} Disconnected Window - throwing away all realized elements", layoutId);
                    _elementManager.ClearRealizedRange();
                    var anchor = _context!.GetOrCreateElementAt(anchorIndex, ElementRealizationOptions.ForceCreate | ElementRealizationOptions.SuppressAutoRecycle);
                    _elementManager.Add(anchor, anchorIndex);
                }

                var anchorElement = _elementManager.GetRealizedElement(anchorIndex);
                var desiredSize = MeasureElement(anchorElement!, anchorIndex, availableSize, _context!);
                var layoutBounds = new Rect(anchorPosition.X, anchorPosition.Y, desiredSize.Width, desiredSize.Height);
                _elementManager.SetLayoutBoundsForDataIndex(anchorIndex, layoutBounds);
                Logger.TryGet(LogEventLevel.Verbose, "Repeater")?.Log(this, "{LayoutId}: Layout bounds of anchor {anchor} are ({Bounds})", layoutId, anchorIndex, layoutBounds);
            }
            else
            {
                Logger.TryGet(LogEventLevel.Verbose, "Repeater")?.Log(this, "{LayoutId} Anchor index is not valid - throwing away all realized elements", layoutId);
                _elementManager.ClearRealizedRange();
            }

            _lastAvailableSize = availableSize;
            _lastItemSpacing = minItemSpacing;
            if (IsVirtualizingContext)
            {
                var realizationRect = RealizationRect;
                _lastRealizationRect = realizationRect;
                _hasValidRealizationRect = realizationRect.Width != 0 || realizationRect.Height != 0;
            }
            else
            {
                _hasValidRealizationRect = false;
                _lastMeasureRealizationWindowJumped = false;
            }

            return anchorIndex;
        }

        private void Generate(GenerateDirection direction, int anchorIndex, Size availableSize, bool isWrapping, double minItemSpacing, double lineSpacing, int maxItemsPerLine, bool disableVirtualization, string? layoutId)
        {
            var directionLabel = direction == GenerateDirection.Forward ? "forward" : "backward";
            using var activity = ItemsRepeaterDiagnostics.StartGenerate(layoutId, directionLabel, isWrapping, anchorIndex);
            var generateTimestamp = ItemsRepeaterDiagnostics.GetTimestamp();
            var measuredCount = 0;

            if (anchorIndex != -1)
            {
                var step = direction == GenerateDirection.Forward ? 1 : -1;

                Logger.TryGet(LogEventLevel.Verbose, "Repeater")?.Log(this, "{LayoutId}: Generating {Direction} from anchor {Anchor}", layoutId, direction, anchorIndex);

                var previousIndex = anchorIndex;
                var currentIndex = anchorIndex + step;
                var anchorBounds = _elementManager.GetLayoutBoundsForDataIndex(anchorIndex);
                var lineOffset = _orientation.MajorStart(anchorBounds);
                var lineMajorSize = _orientation.MajorSize(anchorBounds);
                var countInLine = 1;
                var lineNeedsReposition = false;

                while (_elementManager.IsIndexValidInData(currentIndex) && (disableVirtualization || ShouldContinueFillingUpSpace(previousIndex, direction)))
                {
                    _elementManager.EnsureElementRealized(direction == GenerateDirection.Forward, currentIndex, layoutId);
                    var currentElement = _elementManager.GetRealizedElement(currentIndex);
                    var desiredSize = MeasureElement(currentElement!, currentIndex, availableSize, _context!);
                    measuredCount++;

                    var currentBounds = new Rect(0, 0, desiredSize.Width, desiredSize.Height);
                    var previousElementBounds = _elementManager.GetLayoutBoundsForDataIndex(previousIndex);

                    if (direction == GenerateDirection.Forward)
                    {
                        var remainingSpace = _orientation.Minor(availableSize) - (_orientation.MinorStart(previousElementBounds) + _orientation.MinorSize(previousElementBounds) + minItemSpacing + _orientation.Minor(desiredSize));
                        if (countInLine >= maxItemsPerLine || _algorithmCallbacks!.Algorithm_ShouldBreakLine(currentIndex, remainingSpace))
                        {
                            _orientation.SetMinorStart(ref currentBounds, 0);
                            _orientation.SetMajorStart(ref currentBounds, _orientation.MajorStart(previousElementBounds) + lineMajorSize + lineSpacing);

                            if (lineNeedsReposition)
                            {
                                for (var i = 0; i < countInLine; i++)
                                {
                                    var dataIndex = currentIndex - 1 - i;
                                    var bounds = _elementManager.GetLayoutBoundsForDataIndex(dataIndex);
                                    _orientation.SetMajorSize(ref bounds, lineMajorSize);
                                    _elementManager.SetLayoutBoundsForDataIndex(dataIndex, bounds);
                                }
                            }

                            lineMajorSize = _orientation.MajorSize(currentBounds);
                            lineOffset = _orientation.MajorStart(currentBounds);
                            lineNeedsReposition = false;
                            countInLine = 1;
                        }
                        else
                        {
                            _orientation.SetMinorStart(ref currentBounds, _orientation.MinorStart(previousElementBounds) + _orientation.MinorSize(previousElementBounds) + minItemSpacing);
                            _orientation.SetMajorStart(ref currentBounds, lineOffset);
                            lineMajorSize = Math.Max(lineMajorSize, _orientation.MajorSize(currentBounds));
                            lineNeedsReposition = _orientation.MajorSize(previousElementBounds) != _orientation.MajorSize(currentBounds);
                            countInLine++;
                        }
                    }
                    else
                    {
                        var remainingSpace = _orientation.MinorStart(previousElementBounds) - (_orientation.Minor(desiredSize) + minItemSpacing);
                        if (countInLine >= maxItemsPerLine || _algorithmCallbacks!.Algorithm_ShouldBreakLine(currentIndex, remainingSpace))
                        {
                            var availableSizeMinor = _orientation.Minor(availableSize);
                            _orientation.SetMinorStart(ref currentBounds, !double.IsInfinity(availableSizeMinor) ? availableSizeMinor - _orientation.Minor(desiredSize) : 0);
                            _orientation.SetMajorStart(ref currentBounds, lineOffset - _orientation.Major(desiredSize) - lineSpacing);

                            if (lineNeedsReposition)
                            {
                                var previousLineOffset = _orientation.MajorStart(_elementManager.GetLayoutBoundsForDataIndex(currentIndex + countInLine + 1));
                                for (var i = 0; i < countInLine; i++)
                                {
                                    var dataIndex = currentIndex + 1 + i;
                                    if (dataIndex != anchorIndex)
                                    {
                                        var bounds = _elementManager.GetLayoutBoundsForDataIndex(dataIndex);
                                        _orientation.SetMajorStart(ref bounds, previousLineOffset - lineMajorSize - lineSpacing);
                                        _orientation.SetMajorSize(ref bounds, lineMajorSize);
                                        _elementManager.SetLayoutBoundsForDataIndex(dataIndex, bounds);
                                        Logger.TryGet(LogEventLevel.Verbose, "Repeater")?.Log(this, "{LayoutId}: Corrected Layout bounds of element {Index} are ({Bounds})", layoutId, dataIndex, bounds);
                                    }
                                }
                            }

                            lineMajorSize = _orientation.MajorSize(currentBounds);
                            lineOffset = _orientation.MajorStart(currentBounds);
                            lineNeedsReposition = false;
                            countInLine = 1;
                        }
                        else
                        {
                            _orientation.SetMinorStart(ref currentBounds, _orientation.MinorStart(previousElementBounds) - _orientation.Minor(desiredSize) - minItemSpacing);
                            _orientation.SetMajorStart(ref currentBounds, lineOffset);
                            lineMajorSize = Math.Max(lineMajorSize, _orientation.MajorSize(currentBounds));
                            lineNeedsReposition = _orientation.MajorSize(previousElementBounds) != _orientation.MajorSize(currentBounds);
                            countInLine++;
                        }
                    }

                    _elementManager.SetLayoutBoundsForDataIndex(currentIndex, currentBounds);
                    Logger.TryGet(LogEventLevel.Verbose, "Repeater")?.Log(this, "{LayoutId}: Layout bounds of element {Index} are ({Bounds}).", layoutId, currentIndex, currentBounds);
                    previousIndex = currentIndex;
                    currentIndex += step;
                }

                if (direction == GenerateDirection.Forward)
                {
                    var dataCount = _context!.ItemCount;
                    _lastRealizedDataIndexInsideRealizationWindow = previousIndex == dataCount - 1 ? dataCount - 1 : previousIndex - 1;
                    _lastRealizedDataIndexInsideRealizationWindow = Math.Max(0, _lastRealizedDataIndexInsideRealizationWindow);
                }
                else
                {
                    var dataCount = _context!.ItemCount;
                    _firstRealizedDataIndexInsideRealizationWindow = previousIndex == 0 ? 0 : previousIndex + 1;
                    _firstRealizedDataIndexInsideRealizationWindow = Math.Min(dataCount - 1, _firstRealizedDataIndexInsideRealizationWindow);
                }

                _elementManager.DiscardElementsOutsideWindow(direction == GenerateDirection.Forward, currentIndex);
            }

            ItemsRepeaterDiagnostics.RecordGenerate(ItemsRepeaterDiagnostics.GetElapsedMilliseconds(generateTimestamp), layoutId, directionLabel, measuredCount, _elementManager.GetRealizedElementCount(), isWrapping, disableVirtualization);
        }

        private void MakeAnchor(VirtualizingLayoutContext context, int index, Size availableSize, string? layoutId)
        {
            ItemsRepeaterDiagnostics.RecordMakeAnchor(layoutId, index);
            _elementManager.ClearRealizedRange();
            var internalAnchor = _algorithmCallbacks!.Algorithm_GetAnchorForTargetElement(index, availableSize, context);

            for (var dataIndex = internalAnchor.Index; dataIndex < index + 1; ++dataIndex)
            {
                var element = context.GetOrCreateElementAt(dataIndex, ElementRealizationOptions.ForceCreate | ElementRealizationOptions.SuppressAutoRecycle);
                element.Measure(_algorithmCallbacks.Algorithm_GetMeasureSize(dataIndex, availableSize, context));
                _elementManager.Add(element, dataIndex);
            }
        }

        private bool IsReflowRequired()
        {
            return _elementManager.GetRealizedElementCount() > 0 &&
                   _elementManager.GetDataIndexFromRealizedRangeIndex(0) == 0 &&
                   _orientation.MinorStart(_elementManager.GetLayoutBoundsForRealizedIndex(0)) != 0;
        }

        private bool ShouldContinueFillingUpSpace(int index, GenerateDirection direction)
        {
            if (!IsVirtualizingContext)
            {
                return true;
            }

            var realizationRect = _context!.RealizationRect;
            var elementBounds = _elementManager.GetLayoutBoundsForDataIndex(index);
            var elementMajorStart = _orientation.MajorStart(elementBounds);
            var elementMajorEnd = _orientation.MajorEnd(elementBounds);
            var rectMajorStart = _orientation.MajorStart(realizationRect);
            var rectMajorEnd = _orientation.MajorEnd(realizationRect);
            var elementMinorStart = _orientation.MinorStart(elementBounds);
            var elementMinorEnd = _orientation.MinorEnd(elementBounds);
            var rectMinorStart = _orientation.MinorStart(realizationRect);
            var rectMinorEnd = _orientation.MinorEnd(realizationRect);

            return (direction == GenerateDirection.Forward && elementMajorStart < rectMajorEnd && elementMinorStart < rectMinorEnd) ||
                   (direction == GenerateDirection.Backward && elementMajorEnd > rectMajorStart && elementMinorEnd > rectMinorStart);
        }

        private bool IsRealizationWindowJumped()
        {
            if (!_hasValidRealizationRect)
            {
                return false;
            }

            var realizationRect = RealizationRect;
            if (!realizationRect.Intersects(_lastRealizationRect))
            {
                return true;
            }

            var majorDelta = Math.Abs(_orientation.MajorStart(realizationRect) - _orientation.MajorStart(_lastRealizationRect));
            var majorSize = Math.Max(_orientation.MajorSize(realizationRect), _orientation.MajorSize(_lastRealizationRect));
            return majorSize > 0 && majorDelta > majorSize * 0.5;
        }

        private Rect EstimateExtent(Size availableSize, string? layoutId)
        {
            Layoutable? firstRealizedElement = null;
            var firstBounds = new Rect();
            Layoutable? lastRealizedElement = null;
            var lastBounds = new Rect();
            var firstDataIndex = -1;
            var lastDataIndex = -1;

            if (_elementManager.GetRealizedElementCount() > 0)
            {
                firstRealizedElement = _elementManager.GetAt(0);
                firstBounds = _elementManager.GetLayoutBoundsForRealizedIndex(0);
                firstDataIndex = _elementManager.GetDataIndexFromRealizedRangeIndex(0);

                var last = _elementManager.GetRealizedElementCount() - 1;
                lastRealizedElement = _elementManager.GetAt(last);
                lastDataIndex = _elementManager.GetDataIndexFromRealizedRangeIndex(last);
                lastBounds = _elementManager.GetLayoutBoundsForRealizedIndex(last);
            }

            var extent = _algorithmCallbacks!.Algorithm_GetExtent(availableSize, _context!, firstRealizedElement, firstDataIndex, firstBounds, lastRealizedElement, lastDataIndex, lastBounds);
            Logger.TryGet(LogEventLevel.Verbose, "Repeater")?.Log(this, "{LayoutId} Extent: ({Bounds})", layoutId, extent);
            return extent;
        }

        private void RaiseLineArranged()
        {
            var realizationRect = RealizationRect;
            if (realizationRect.Width == 0.0f && realizationRect.Height == 0.0f)
            {
                return;
            }

            var realizedElementCount = _elementManager.GetRealizedElementCount();
            if (realizedElementCount <= 0)
            {
                return;
            }

            var countInLine = 0;
            var firstRealizedIndex = _elementManager.GetDataIndexFromRealizedRangeIndex(0);
            var lastRealizedIndex = _elementManager.GetDataIndexFromRealizedRangeIndex(realizedElementCount - 1);
            var startIndex = Math.Max(_firstRealizedDataIndexInsideRealizationWindow, firstRealizedIndex);
            var endIndex = Math.Min(_lastRealizedDataIndexInsideRealizationWindow, lastRealizedIndex);

            if (startIndex > endIndex)
            {
                return;
            }

            var previousElementBounds = _elementManager.GetLayoutBoundsForDataIndex(startIndex);
            var currentLineOffset = _orientation.MajorStart(previousElementBounds);
            var currentLineSize = _orientation.MajorSize(previousElementBounds);
            for (var currentDataIndex = startIndex; currentDataIndex <= endIndex; currentDataIndex++)
            {
                var currentBounds = _elementManager.GetLayoutBoundsForDataIndex(currentDataIndex);
                if (_orientation.MajorStart(currentBounds) != currentLineOffset)
                {
                    _algorithmCallbacks!.Algorithm_OnLineArranged(currentDataIndex - countInLine, countInLine, currentLineSize, _context!);
                    countInLine = 0;
                    currentLineOffset = _orientation.MajorStart(currentBounds);
                    currentLineSize = 0;
                }

                currentLineSize = Math.Max(currentLineSize, _orientation.MajorSize(currentBounds));
                countInLine++;
                previousElementBounds = currentBounds;
            }

            _algorithmCallbacks!.Algorithm_OnLineArranged(endIndex - countInLine + 1, countInLine, currentLineSize, _context!);
        }

        private void ArrangeVirtualizingLayout(Size finalSize, LineAlignment lineAlignment, bool isWrapping, string? layoutId)
        {
            var realizedElementCount = _elementManager.GetRealizedElementCount();
            if (realizedElementCount <= 0)
            {
                return;
            }

            var countInLine = 1;
            var previousElementBounds = _elementManager.GetLayoutBoundsForRealizedIndex(0);
            var currentLineOffset = _orientation.MajorStart(previousElementBounds);
            var spaceAtLineStart = _orientation.MinorStart(previousElementBounds);
            var currentLineSize = _orientation.MajorSize(previousElementBounds);
            for (var i = 1; i < realizedElementCount; i++)
            {
                var currentBounds = _elementManager.GetLayoutBoundsForRealizedIndex(i);
                if (_orientation.MajorStart(currentBounds) != currentLineOffset)
                {
                    var spaceAtLineEnd = _orientation.Minor(finalSize) - _orientation.MinorStart(previousElementBounds) - _orientation.MinorSize(previousElementBounds);
                    PerformLineAlignment(i - countInLine, countInLine, spaceAtLineStart, spaceAtLineEnd, currentLineSize, lineAlignment, isWrapping, finalSize, layoutId);
                    spaceAtLineStart = _orientation.MinorStart(currentBounds);
                    countInLine = 0;
                    currentLineOffset = _orientation.MajorStart(currentBounds);
                    currentLineSize = 0;
                }

                countInLine++;
                currentLineSize = Math.Max(currentLineSize, _orientation.MajorSize(currentBounds));
                previousElementBounds = currentBounds;
            }

            if (countInLine > 0)
            {
                var spaceAtEnd = _orientation.Minor(finalSize) - _orientation.MinorStart(previousElementBounds) - _orientation.MinorSize(previousElementBounds);
                PerformLineAlignment(realizedElementCount - countInLine, countInLine, spaceAtLineStart, spaceAtEnd, currentLineSize, lineAlignment, isWrapping, finalSize, layoutId);
            }
        }

        private void PerformLineAlignment(int lineStartIndex, int countInLine, double spaceAtLineStart, double spaceAtLineEnd, double lineSize, LineAlignment lineAlignment, bool isWrapping, Size finalSize, string? layoutId)
        {
            for (var rangeIndex = lineStartIndex; rangeIndex < lineStartIndex + countInLine; ++rangeIndex)
            {
                var bounds = _elementManager.GetLayoutBoundsForRealizedIndex(rangeIndex);
                _orientation.SetMajorSize(ref bounds, lineSize);

                if (!_scrollOrientationSameAsFlow && (spaceAtLineStart != 0 || spaceAtLineEnd != 0))
                {
                    var totalSpace = spaceAtLineStart + spaceAtLineEnd;
                    var minorStart = _orientation.MinorStart(bounds);
                    switch (lineAlignment)
                    {
                        case LineAlignment.Start:
                            _orientation.SetMinorStart(ref bounds, minorStart - spaceAtLineStart);
                            break;
                        case LineAlignment.End:
                            _orientation.SetMinorStart(ref bounds, minorStart + spaceAtLineEnd);
                            break;
                        case LineAlignment.Center:
                            _orientation.SetMinorStart(ref bounds, (minorStart - spaceAtLineStart) + (totalSpace / 2));
                            break;
                        case LineAlignment.SpaceAround:
                        {
                            var interItemSpace = countInLine >= 1 ? totalSpace / (countInLine * 2) : 0;
                            _orientation.SetMinorStart(ref bounds, (minorStart - spaceAtLineStart) + (interItemSpace * ((rangeIndex - lineStartIndex + 1) * 2 - 1)));
                            break;
                        }
                        case LineAlignment.SpaceBetween:
                        {
                            var interItemSpace = countInLine > 1 ? totalSpace / (countInLine - 1) : 0;
                            _orientation.SetMinorStart(ref bounds, (minorStart - spaceAtLineStart) + (interItemSpace * (rangeIndex - lineStartIndex)));
                            break;
                        }
                        case LineAlignment.SpaceEvenly:
                        {
                            var interItemSpace = countInLine >= 1 ? totalSpace / (countInLine + 1) : 0;
                            _orientation.SetMinorStart(ref bounds, (minorStart - spaceAtLineStart) + (interItemSpace * (rangeIndex - lineStartIndex + 1)));
                            break;
                        }
                    }
                }

                bounds = bounds.Translate(-_lastExtent.Position);

                if (!isWrapping)
                {
                    _orientation.SetMinorSize(ref bounds, Math.Max(_orientation.MinorSize(bounds), _orientation.Minor(finalSize)));
                }

                var element = _elementManager.GetAt(rangeIndex);
                Logger.TryGet(LogEventLevel.Verbose, "Repeater")?.Log(this, "{LayoutId}: Arranging element {Index} at ({Bounds})", layoutId, _elementManager.GetDataIndexFromRealizedRangeIndex(rangeIndex), bounds);
                element.Arrange(bounds);
            }
        }

        private void SetLayoutOrigin()
        {
            if (IsVirtualizingContext)
            {
                var origin = new Point(_lastExtent.X, _lastExtent.Y);
                var current = _context!.LayoutOrigin;
                if (Math.Abs(current.X - origin.X) > 1 || Math.Abs(current.Y - origin.Y) > 1)
                {
                    _context.LayoutOrigin = origin;
                }
            }
        }

        public Layoutable? GetElementIfRealized(int dataIndex)
        {
            return _elementManager.IsDataIndexRealized(dataIndex) ? _elementManager.GetRealizedElement(dataIndex) : null;
        }

        public bool TryGetLayoutBoundsForDataIndex(int dataIndex, out Rect bounds)
        {
            if (_elementManager.IsDataIndexRealized(dataIndex))
            {
                bounds = _elementManager.GetLayoutBoundsForDataIndex(dataIndex);
                return true;
            }

            bounds = default;
            return false;
        }

        public bool TryAddElement0(Layoutable element)
        {
            if (_elementManager.GetRealizedElementCount() == 0)
            {
                _elementManager.Add(element, 0);
                return true;
            }

            return false;
        }

        public enum LineAlignment
        {
            Start,
            Center,
            End,
            SpaceAround,
            SpaceBetween,
            SpaceEvenly,
        }

        private enum GenerateDirection
        {
            Forward,
            Backward,
        }
    }
}
