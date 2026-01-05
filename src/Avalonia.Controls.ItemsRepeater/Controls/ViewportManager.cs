// This source file is adapted from the WinUI project.
// (https://github.com/microsoft/microsoft-ui-xaml)
//
// Licensed to The Avalonia Project under MIT License, courtesy of The .NET Foundation.

using System;
using Avalonia.Layout;
using Avalonia.Logging;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Avalonia.Controls
{
    internal class ViewportManager
    {
        private const double CacheBufferPerSideInflationPixelDelta = 40.0;
        private readonly ItemsRepeater _owner;
        private bool _ensuredScroller;
        private IScrollAnchorProvider? _scroller;
        private Control? _makeAnchorElement;
        private bool _isAnchorOutsideRealizedRange;
        private Rect _visibleWindow;
        private Rect _layoutExtent;
        // This is the expected shift by the layout.
        private Point _expectedViewportShift;
        // This is what is pending and not been accounted for. 
        // Sometimes the scrolling surface cannot service a shift (for example
        // it is already at the top and cannot shift anymore.)
        private Point _pendingViewportShift;
        // Unshiftable shift amount that this view manager can
        // handle on its own to fake it to the layout as if the shift
        // actually happened. This can happen in cases where no scrollviewer
        // in the parent chain can scroll in the shift direction.
        private Point _unshiftableShift;
        private double _maximumHorizontalCacheLength = 2.0;
        private double _maximumVerticalCacheLength = 2.0;
        private double _horizontalCacheBufferPerSide;
        private double _verticalCacheBufferPerSide;
        private int _pendingShiftCount;
        private bool _invalidateMeasureScheduled;
        private bool _isBringIntoViewInProgress;
        private bool _disableScrollAnchoring;
        // For non-virtualizing layouts, we do not need to keep
        // updating viewports and invalidating measure often. So when
        // a non virtualizing layout is used, we stop doing all that work.
        private bool _managingViewportDisabled;
        private bool _effectiveViewportChangedSubscribed;
        private bool _layoutUpdatedSubscribed;

        private bool IsScrollAnchoringDisabled => _disableScrollAnchoring || _owner.Layout is Avalonia.Layout.WrapLayout;

        public ViewportManager(ItemsRepeater owner)
        {
            _owner = owner;
        }

        public Control? SuggestedAnchor
        {
            get
            {
                // The element generated during the ItemsRepeater.MakeAnchor call has precedence over the next tick.
                var suggestedAnchor = _makeAnchorElement;
                var owner = _owner;

                if (suggestedAnchor == null)
                {
                    if (IsScrollAnchoringDisabled)
                    {
                        return null;
                    }

                    var anchorElement = _scroller?.CurrentAnchor;

                    if (anchorElement != null)
                    {
                        // We can't simply return anchorElement because, in case of nested Repeaters, it may not
                        // be a direct child of ours, or even an indirect child. We need to walk up the tree starting
                        // from anchorElement to figure out what child of ours (if any) to use as the suggested element.
                        var child = anchorElement;
                        var parent = child.GetVisualParent() as Control;

                        while (parent != null)
                        {
                            if (parent == owner)
                            {
                                var virtInfo = ItemsRepeater.GetVirtualizationInfo(child);
                                if (virtInfo?.IsRegisteredAsAnchorCandidate == true)
                                {
                                    suggestedAnchor = child;
                                }
                                break;
                            }

                            child = parent;
                            parent = parent.GetVisualParent() as Control;
                        }
                    }
                }

                return suggestedAnchor;
            }
        }

        public bool HasScroller => _scroller != null;

        public Control? MadeAnchor => _makeAnchorElement;

        public double HorizontalCacheLength
        {
            get => _maximumHorizontalCacheLength;
            set
            {
                if (_maximumHorizontalCacheLength != value)
                {
                    ValidateCacheLength(value);
                    _maximumHorizontalCacheLength = value;
                }
            }
        }

        public double VerticalCacheLength
        {
            get => _maximumVerticalCacheLength;
            set
            {
                if (_maximumVerticalCacheLength != value)
                {
                    ValidateCacheLength(value);
                    _maximumVerticalCacheLength = value;
                }
            }
        }

        public Rect GetLayoutVisibleWindow()
        {
            var visibleWindow = _visibleWindow;

            if (_makeAnchorElement != null)
            {
                // The anchor is not necessarily laid out yet. Its position should default
                // to zero and the layout origin is expected to change once layout is done.
                // Until then, we need a window that's going to protect the anchor from
                // getting recycled.
                visibleWindow = visibleWindow.WithX(0).WithY(0);
            }
            else if (HasScroller)
            {
                var shiftX = _layoutExtent.X + _unshiftableShift.X;
                var shiftY = _layoutExtent.Y + _unshiftableShift.Y;

                if (!_owner.UsesLogicalScrolling)
                {
                    shiftX += _expectedViewportShift.X;
                    shiftY += _expectedViewportShift.Y;
                }

                visibleWindow = new Rect(
                    visibleWindow.X + shiftX,
                    visibleWindow.Y + shiftY,
                    visibleWindow.Width,
                    visibleWindow.Height);
            }

            return visibleWindow;
        }

        public Rect GetLayoutRealizationWindow()
        {
            var realizationWindow = GetLayoutVisibleWindow();
            if (HasScroller)
            {
                realizationWindow = new Rect(
                    realizationWindow.X - _horizontalCacheBufferPerSide,
                    realizationWindow.Y - _verticalCacheBufferPerSide,
                    realizationWindow.Width + _horizontalCacheBufferPerSide * 2.0,
                    realizationWindow.Height + _verticalCacheBufferPerSide * 2.0);
            }

            return realizationWindow;
        }

        public void SetLayoutExtent(Rect extent)
        {
            if (IsScrollAnchoringDisabled)
            {
                _layoutExtent = extent;
                _expectedViewportShift = default;
                _pendingViewportShift = default;
                _unshiftableShift = default;
                _pendingShiftCount = 0;
                if (!_owner.UsesLogicalScrolling)
                {
                    ((Control?)_scroller)?.InvalidateArrange();
                }
                return;
            }

            var deltaX = _layoutExtent.X - extent.X;
            var deltaY = _layoutExtent.Y - extent.Y;
            if (Math.Abs(deltaX) <= 1)
            {
                deltaX = 0;
            }
            if (Math.Abs(deltaY) <= 1)
            {
                deltaY = 0;
            }

            if (_owner.UsesLogicalScrolling)
            {
                var shift = new Vector(deltaX, deltaY);
                _layoutExtent = extent;
                _expectedViewportShift = default;
                _pendingViewportShift = default;
                _pendingShiftCount = 0;

                if (shift != default)
                {
                    var appliedShift = _owner.ApplyScrollOffsetShift(shift, raiseInvalidated: true);
                    var remainingShift = new Vector(shift.X - appliedShift.X, shift.Y - appliedShift.Y);
                    if (Math.Abs(remainingShift.X) > 1 || Math.Abs(remainingShift.Y) > 1)
                    {
                        _unshiftableShift = new Point(
                            _unshiftableShift.X + remainingShift.X,
                            _unshiftableShift.Y + remainingShift.Y);
                    }

                    TryInvalidateMeasure();
                }

                return;
            }

            _expectedViewportShift = new Point(
                _expectedViewportShift.X + deltaX,
                _expectedViewportShift.Y + deltaY);

            // We tolerate viewport imprecisions up to 1 pixel to avoid invalidating layout too much.
            var expectsShift = Math.Abs(_expectedViewportShift.X) > 1 || Math.Abs(_expectedViewportShift.Y) > 1;
            if (!expectsShift)
            {
                _pendingShiftCount = 0;
            }

            if (expectsShift)
            {
                Logger.TryGet(LogEventLevel.Verbose, "Repeater")?.Log(this, "{LayoutId}: Expecting viewport shift of ({Shift})",
                    _owner.Layout?.LayoutId, _expectedViewportShift);

                // There are cases where we might be expecting a shift but not get it. We will
                // be waiting for the effective viewport event but if the scroll viewer is not able
                // to perform the shift (perhaps because it cannot scroll in negative offset),
                // then we will end up not realizing elements in the visible 
                // window. To avoid this, we register to layout updated for this layout pass. If we 
                // get an effective viewport, we know we have a new viewport and we unregister from
                // layout updated. If we get the layout updated handler, then we know that the 
                // scroller was unable to perform the shift and we invalidate measure and unregister
                // from the layout updated event.
                if (!_layoutUpdatedSubscribed)
                {
                    _owner.LayoutUpdated += OnLayoutUpdated;
                    _layoutUpdatedSubscribed = true;
                }
            }

            _layoutExtent = extent;
            _pendingViewportShift = _expectedViewportShift;

            // We just finished a measure pass and have a new extent.
            // Let's make sure the scrollers will run its arrange so that they track the anchor.
            ((Control?)_scroller)?.InvalidateArrange();
        }

        public Point GetOrigin() => _layoutExtent.TopLeft;

        public void OnLayoutChanged(bool isVirtualizing)
        {
            _managingViewportDisabled = !isVirtualizing;

            _layoutExtent = default;
            _expectedViewportShift = default;
            _pendingViewportShift = default;
            _unshiftableShift = default;

            if (_effectiveViewportChangedSubscribed)
            {
                _owner.EffectiveViewportChanged -= OnEffectiveViewportChanged;
                _effectiveViewportChangedSubscribed = false;
            }

            if (!_owner.UsesLogicalScrolling && !_managingViewportDisabled)
            {
                _owner.EffectiveViewportChanged += OnEffectiveViewportChanged;
                _effectiveViewportChangedSubscribed = true;
            }
        }

        public void OnElementPrepared(Control element, VirtualizationInfo virtInfo)
        {
            // WinUI registers the element as an anchor candidate here, but I feel that's in error:
            // at this point the element has not yet been positioned by the arrange pass so it will
            // have its previous position, meaning that when the arrange pass moves it into its new
            // position, an incorrect scroll anchoring will occur. Instead signal that it's not yet
            // registered as a scroll anchor candidate.
            virtInfo.IsRegisteredAsAnchorCandidate = false;
        }

        public void OnElementCleared(Control element, VirtualizationInfo virtInfo)
        {
            _scroller?.UnregisterAnchorCandidate(element);
            virtInfo.IsRegisteredAsAnchorCandidate = false;
        }

        public void OnOwnerMeasuring()
        {
            // This is because of a bug that causes effective viewport to not
            // fire if you register during arrange.
            // Bug 17411076: EffectiveViewport: registering for effective viewport in arrange should invalidate viewport
            EnsureScroller();
        }

        public void OnOwnerArranged()
        {
            _expectedViewportShift = default;

            if (!_managingViewportDisabled)
            {
                // This is because of a bug that causes effective viewport to not 
                // fire if you register during arrange.
                // Bug 17411076: EffectiveViewport: registering for effective viewport in arrange should invalidate viewport
                // EnsureScroller();

                if (HasScroller)
                {
                    double maximumHorizontalCacheBufferPerSide = _maximumHorizontalCacheLength * _visibleWindow.Width / 2.0;
                    double maximumVerticalCacheBufferPerSide = _maximumVerticalCacheLength * _visibleWindow.Height / 2.0;

                    bool continueBuildingCache =
                        _horizontalCacheBufferPerSide < maximumHorizontalCacheBufferPerSide ||
                        _verticalCacheBufferPerSide < maximumVerticalCacheBufferPerSide;

                    if (continueBuildingCache)
                    {
                        _horizontalCacheBufferPerSide += CacheBufferPerSideInflationPixelDelta;
                        _verticalCacheBufferPerSide += CacheBufferPerSideInflationPixelDelta;

                        _horizontalCacheBufferPerSide = Math.Min(_horizontalCacheBufferPerSide, maximumHorizontalCacheBufferPerSide);
                        _verticalCacheBufferPerSide = Math.Min(_verticalCacheBufferPerSide, maximumVerticalCacheBufferPerSide);
                    }
                }
            }
        }

        private void OnLayoutUpdated(object? sender, EventArgs args)
        {
            _owner.LayoutUpdated -= OnLayoutUpdated;
            _layoutUpdatedSubscribed = false;
            if (_managingViewportDisabled)
            {
                return;
            }

            // We were expecting a viewport shift but we never got one and we are not going to in this
            // layout pass. We likely will never get this shift, so lets assume that we are never going to get it and
            // adjust our expected shift to track that. One case where this can happen is when there is no scrollviewer
            // that can scroll in the direction where the shift is expected.
            if (_pendingViewportShift.X != 0 || _pendingViewportShift.Y != 0)
            {
                _pendingShiftCount++;
                if (_pendingShiftCount >= 2 && _owner.Layout is Avalonia.Layout.WrapLayout)
                {
                    _disableScrollAnchoring = true;
                    _expectedViewportShift = default;
                    _pendingViewportShift = default;
                    _unshiftableShift = default;
                    _pendingShiftCount = 0;
                    UnregisterAllAnchorCandidates();
                    ScheduleInvalidateMeasure();
                    return;
                }

                Logger.TryGet(LogEventLevel.Verbose, "Repeater")?.Log(this, "{LayoutId}: Layout Updated with pending shift {Shift}- invalidating measure",
                    _owner.Layout?.LayoutId,
                    _pendingViewportShift);

                // Assume this is never going to come.
                _unshiftableShift = new Point(
                    _unshiftableShift.X + _pendingViewportShift.X,
                    _unshiftableShift.Y + _pendingViewportShift.Y);
                _pendingViewportShift = default;
                _expectedViewportShift = default;

                if (Math.Abs(_unshiftableShift.X) > 1 || Math.Abs(_unshiftableShift.Y) > 1)
                {
                    if (_owner.Layout is Avalonia.Layout.WrapLayout)
                    {
                        ScheduleInvalidateMeasure();
                    }
                    else
                    {
                        TryInvalidateMeasure();
                    }
                }
            }
        }

        public void OnMakeAnchor(Control? anchor, bool isAnchorOutsideRealizedRange)
        {
            if (_makeAnchorElement != anchor)
            {
                _makeAnchorElement = anchor;
                _isAnchorOutsideRealizedRange = isAnchorOutsideRealizedRange;
            }
        }

        public void OnBringIntoViewRequested(RequestBringIntoViewEventArgs args)
        {
            if (!_managingViewportDisabled)
            {
                // During the time between a bring into view request and the element coming into view we do not
                // want the anchor provider to pick some anchor and jump to it. Instead we want to anchor on the
                // element that is being brought into view. We can do this by making just that element as a potential
                // anchor candidate and ensure no other element of this repeater is an anchor candidate.
                // Once the layout pass is done and we render the frame, the element will be in frame and we can
                // switch back to letting the anchor provider pick a suitable anchor.

                // get the targetChild - i.e the immediate child of this repeater that is being brought into view.
                // Note that the element being brought into view could be a descendant.
                var targetChild = GetImmediateChildOfRepeater((Control)args.TargetObject!);

                if (targetChild is null)
                {
                    return;
                }

                // Make sure that only the target child can be the anchor during the bring into view operation.
                if (_scroller is object)
                {
                    foreach (var child in _owner.Children)
                    {
                        var info = ItemsRepeater.GetVirtualizationInfo(child);

                        if (child != targetChild && info.IsRegisteredAsAnchorCandidate)
                        {
                            _scroller.UnregisterAnchorCandidate(child);
                            info.IsRegisteredAsAnchorCandidate = false;
                        }
                    }
                }

                // Register action to go back to how things were before where any child can be the anchor. Here,
                // WinUI uses CompositionTarget.Rendering but we don't currently have that, so post an action to
                // run *after* rendering has completed (priority needs to be lower than Render as Transformed
                // bounds must have been set in order for OnEffectiveViewportChanged to trigger).
                if (!_isBringIntoViewInProgress)
                {
                    _isBringIntoViewInProgress = true;
                    Dispatcher.UIThread.Post(OnCompositionTargetRendering, DispatcherPriority.Loaded);
                }
            }
        }

        public void RegisterScrollAnchorCandidate(Control element, VirtualizationInfo virtInfo)
        {
            if (IsScrollAnchoringDisabled && element != _makeAnchorElement)
            {
                return;
            }

            if (!virtInfo.IsRegisteredAsAnchorCandidate)
            {
                _scroller?.RegisterAnchorCandidate(element);
                virtInfo.IsRegisteredAsAnchorCandidate = true;
            }
        }

        private Control? GetImmediateChildOfRepeater(Control descendant)
        {
            var targetChild = descendant;
            var parent = (Control?)descendant.GetVisualParent();
            while (parent != null && parent != _owner)
            {
                targetChild = parent;
                parent = (Control?)parent.GetVisualParent();
            }

            if (parent == null)
            {
                return null;
            }

            return targetChild;
        }

        private void OnCompositionTargetRendering()
        {
            _isBringIntoViewInProgress = false;
            _makeAnchorElement = null;

            // Undo the anchor deregistrations done by OnBringIntoViewRequested.
            if (_scroller is object)
            {
                foreach (var child in _owner.Children)
                {
                    var info = ItemsRepeater.GetVirtualizationInfo(child);

                    // The item brought into view is still registered - don't register it more than once.
                    if (info.IsRealized && info.IsHeldByLayout && !info.IsRegisteredAsAnchorCandidate)
                    {
                        _scroller.RegisterAnchorCandidate(child);
                        info.IsRegisteredAsAnchorCandidate = true;
                    }
                }
            }

            // HACK: Invalidate measure now that the anchor has been removed so that a layout can be
            // done with a proper realization rect. This is a hack not present upstream to try to fix
            // https://github.com/microsoft/microsoft-ui-xaml/issues/1422
            TryInvalidateMeasure();
        }

        public void ResetScrollers()
        {
            if (_scroller is object)
            {
                foreach (var child in _owner.Children)
                {
                    var info = ItemsRepeater.GetVirtualizationInfo(child);

                    if (info.IsRegisteredAsAnchorCandidate)
                    {
                        _scroller.UnregisterAnchorCandidate(child);
                        info.IsRegisteredAsAnchorCandidate = false;
                    }
                }

                _scroller = null;
            }

            _owner.EffectiveViewportChanged -= OnEffectiveViewportChanged;
            _effectiveViewportChangedSubscribed = false;
            _ensuredScroller = false;
            _disableScrollAnchoring = false;
            _pendingShiftCount = 0;
        }

        private void OnEffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
        {
            Logger.TryGet(LogEventLevel.Verbose, "Repeater")?.Log(this, "{LayoutId}: EffectiveViewportChanged event callback", _owner.Layout?.LayoutId);
            UpdateViewport(e.EffectiveViewport, invalidateMeasure: true);

            _pendingViewportShift = default;
            _unshiftableShift = default;
            _pendingShiftCount = 0;
            if (_visibleWindow.Width == 0 && _visibleWindow.Height == 0)
            {
                // We got cleared.
                _layoutExtent = default;
            }

            // We got a new viewport, we dont need to wait for layout updated anymore to 
            // see if our request for a pending shift was handled.
            if (_layoutUpdatedSubscribed)
            {
                _owner.LayoutUpdated -= OnLayoutUpdated;
                _layoutUpdatedSubscribed = false;
            }
        }

        private void EnsureScroller()
        {
            if (!_ensuredScroller)
            {
                ResetScrollers();

                var parent = _owner.GetVisualParent();
                while (parent != null)
                {
                    if (parent is IScrollAnchorProvider scroller)
                    {
                        _scroller = scroller;
                        break;
                    }

                    parent = parent.GetVisualParent();
                }

                if (!_owner.UsesLogicalScrolling && !_managingViewportDisabled)
                {
                    _owner.EffectiveViewportChanged += OnEffectiveViewportChanged;
                    _effectiveViewportChangedSubscribed = true;
                }

                _ensuredScroller = true;
            }
        }

        public void UpdateViewportFromLogicalScroll(Size viewport, Vector offset, bool invalidateMeasure)
        {
            if (_managingViewportDisabled)
            {
                return;
            }

            UpdateViewport(new Rect(offset.X, offset.Y, viewport.Width, viewport.Height), invalidateMeasure);
        }

        private void UpdateViewport(Rect viewport, bool invalidateMeasure = true)
        {
            var currentVisibleWindow = viewport;
            var previousVisibleWindow = _visibleWindow;

            Logger.TryGet(LogEventLevel.Verbose, "Repeater")?.Log(this, "{LayoutId}: Effective Viewport: ({Before})->({After})",
                _owner.Layout?.LayoutId,
                previousVisibleWindow,
                viewport);

            if (-currentVisibleWindow.X <= ItemsRepeater.ClearedElementsArrangePosition.X &&
                -currentVisibleWindow.Y <= ItemsRepeater.ClearedElementsArrangePosition.Y)
            {
                Logger.TryGet(LogEventLevel.Verbose, "Repeater")?.Log(this, "{LayoutId}: Viewport is invalid. visible window cleared", _owner.Layout?.LayoutId);
                // We got cleared.
                _visibleWindow = default;
            }
            else
            {
                _visibleWindow = currentVisibleWindow;
            }

            if (_visibleWindow != previousVisibleWindow)
            {
                Logger.TryGet(LogEventLevel.Verbose, "Repeater")?.Log(this, "{LayoutId}: Used Viewport: ({Before})->({After})",
                    _owner.Layout?.LayoutId,
                    previousVisibleWindow,
                    currentVisibleWindow);
                UpdateScrollAnchoring(previousVisibleWindow, _visibleWindow);
                if (invalidateMeasure)
                {
                    if (_owner.Layout is Avalonia.Layout.WrapLayout)
                    {
                        if (IsSmallViewportDelta(previousVisibleWindow, _visibleWindow))
                        {
                            TryInvalidateMeasure();
                        }
                        else
                        {
                            ScheduleInvalidateMeasure();
                        }
                    }
                    else
                    {
                        TryInvalidateMeasure();
                    }
                }
            }
        }

        private void UpdateScrollAnchoring(Rect previous, Rect current)
        {
            if (_owner.Layout is Avalonia.Layout.WrapLayout)
            {
                if (!_disableScrollAnchoring)
                {
                    _disableScrollAnchoring = true;
                    UnregisterAllAnchorCandidates();
                }

                return;
            }

            if (!HasScroller)
            {
                return;
            }

            if ((previous.Width == 0 && previous.Height == 0) || (current.Width == 0 && current.Height == 0))
            {
                return;
            }

            var verticalDelta = Math.Abs(current.Y - previous.Y);
            var horizontalDelta = Math.Abs(current.X - previous.X);
            var thresholdMultiplier = _owner.Layout is Avalonia.Layout.WrapLayout ? 0.25 : 0.5;
            var verticalThreshold = Math.Max(previous.Height, current.Height) * thresholdMultiplier;
            var horizontalThreshold = Math.Max(previous.Width, current.Width) * thresholdMultiplier;

            var disableAnchoring = (verticalThreshold > 0 && verticalDelta > verticalThreshold) ||
                (horizontalThreshold > 0 && horizontalDelta > horizontalThreshold);

            if (_disableScrollAnchoring == disableAnchoring)
            {
                return;
            }

            _disableScrollAnchoring = disableAnchoring;
            if (_disableScrollAnchoring)
            {
                UnregisterAllAnchorCandidates();
            }
        }

        private void UnregisterAllAnchorCandidates()
        {
            if (_scroller is null)
            {
                return;
            }

            foreach (var child in _owner.Children)
            {
                var info = ItemsRepeater.GetVirtualizationInfo(child);
                if (info.IsRegisteredAsAnchorCandidate)
                {
                    _scroller.UnregisterAnchorCandidate(child);
                    info.IsRegisteredAsAnchorCandidate = false;
                }
            }
        }

        private static void ValidateCacheLength(double cacheLength)
        {
            if (cacheLength < 0.0 || double.IsInfinity(cacheLength) || double.IsNaN(cacheLength))
            {
                throw new ArgumentException("The maximum cache length must be equal or superior to zero.");
            }
        }

        private void TryInvalidateMeasure()
        {
            // Don't invalidate measure if we have an invalid window.
            if (_visibleWindow.Width != 0 || _visibleWindow.Height != 0)
            {
                // We invalidate measure instead of just invalidating arrange because
                // we don't invalidate measure in UpdateViewport if the view is changing to
                // avoid layout cycles.
                Logger.TryGet(LogEventLevel.Verbose, "Repeater")?.Log(this, "{LayoutId}: Invalidating measure due to viewport change", _owner.Layout?.LayoutId);
                _owner.InvalidateMeasure();
            }
        }

        private void ScheduleInvalidateMeasure()
        {
            if (_invalidateMeasureScheduled)
            {
                return;
            }

            _invalidateMeasureScheduled = true;
            Dispatcher.UIThread.Post(() =>
            {
                _invalidateMeasureScheduled = false;
                TryInvalidateMeasure();
            }, DispatcherPriority.Loaded);
        }

        private static bool IsSmallViewportDelta(Rect previous, Rect current)
        {
            if (previous.Width == 0 && previous.Height == 0)
            {
                return false;
            }

            var verticalDelta = Math.Abs(current.Y - previous.Y);
            var horizontalDelta = Math.Abs(current.X - previous.X);
            var verticalThreshold = Math.Max(previous.Height, current.Height) * 0.4;
            var horizontalThreshold = Math.Max(previous.Width, current.Width) * 0.4;

            if (verticalThreshold > 0 && verticalDelta > verticalThreshold)
            {
                return false;
            }

            if (horizontalThreshold > 0 && horizontalDelta > horizontalThreshold)
            {
                return false;
            }

            return true;
        }

        private class ScrollerInfo
        {
            public ScrollerInfo(ScrollViewer scroller)
            {
                Scroller = scroller;
            }

            public ScrollViewer Scroller { get; }
        }
    };
}
