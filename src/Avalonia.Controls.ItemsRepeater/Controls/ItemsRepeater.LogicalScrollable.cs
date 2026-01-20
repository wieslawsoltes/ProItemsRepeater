// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Avalonia.Controls
{
    public partial class ItemsRepeater : ILogicalScrollable
    {
        private Size _extent;
        private Size _viewport;
        private Vector _offset;
        private Size _scrollSizeCache;
        private bool _scrollSizeCacheValid;
        private bool _canHorizontallyScroll;
        private bool _canVerticallyScroll;
        private bool _scrollAxesConfigured;
        private EventHandler? _scrollInvalidated;
        private bool _isLogicalScrollActive;
        private bool _usesLogicalScrolling;
        private bool _scrollSizeCacheUsesNested;
        internal bool UsesLogicalScrolling => _usesLogicalScrolling;

        bool ILogicalScrollable.CanHorizontallyScroll
        {
            get => _canHorizontallyScroll;
            set
            {
                if (_canHorizontallyScroll == value)
                {
                    _scrollAxesConfigured = true;
                    return;
                }

                _canHorizontallyScroll = value;
                _scrollAxesConfigured = true;
                OnCanScrollChanged();
            }
        }

        bool ILogicalScrollable.CanVerticallyScroll
        {
            get => _canVerticallyScroll;
            set
            {
                if (_canVerticallyScroll == value)
                {
                    _scrollAxesConfigured = true;
                    return;
                }

                _canVerticallyScroll = value;
                _scrollAxesConfigured = true;
                OnCanScrollChanged();
            }
        }

        bool ILogicalScrollable.IsLogicalScrollEnabled => IsLogicalScrollEnabled;

        Size ILogicalScrollable.ScrollSize => GetScrollSize();

        Size ILogicalScrollable.PageScrollSize => _viewport;

        Size IScrollable.Extent => _extent;

        Size IScrollable.Viewport => _viewport;

        Vector IScrollable.Offset
        {
            get => _offset;
            set => SetLogicalOffset(value, raiseInvalidated: true);
        }

        event EventHandler? ILogicalScrollable.ScrollInvalidated
        {
            add
            {
                var wasActive = _isLogicalScrollActive;
                _scrollInvalidated += value;
                _isLogicalScrollActive = _scrollInvalidated != null;
                if (_isLogicalScrollActive != wasActive)
                {
                    UpdateLogicalScrollingState();
                }
            }
            remove
            {
                var wasActive = _isLogicalScrollActive;
                _scrollInvalidated -= value;
                _isLogicalScrollActive = _scrollInvalidated != null;
                if (_isLogicalScrollActive != wasActive)
                {
                    UpdateLogicalScrollingState();
                }
            }
        }

        bool ILogicalScrollable.BringIntoView(Control target, Rect targetRect)
        {
            if (!this.IsAttachedToVisualTree())
            {
                return false;
            }

            Rect rect;
            if (target.GetVisualParent() == this)
            {
                var targetBounds = target.Bounds;
                if (targetRect.Width <= 0 || targetRect.Height <= 0)
                {
                    rect = targetBounds;
                }
                else
                {
                    rect = new Rect(
                        targetBounds.X + targetRect.X,
                        targetBounds.Y + targetRect.Y,
                        targetRect.Width,
                        targetRect.Height);
                }
            }
            else
            {
                var transform = target!.TransformToVisual(this);
                if (transform is null)
                {
                    return false;
                }

                rect = targetRect.TransformToAABB(transform.Value);
            }

            if (UsesLogicalScrolling && (_offset.X != 0 || _offset.Y != 0))
            {
                rect = rect.Translate(_offset);
            }

            var offset = _offset;
            var updated = offset;

            if (rect.Bottom > offset.Y + _viewport.Height)
            {
                updated = updated.WithY(rect.Bottom - _viewport.Height);
            }
            else if (rect.Y < offset.Y)
            {
                updated = updated.WithY(rect.Y);
            }

            if (rect.Right > offset.X + _viewport.Width)
            {
                updated = updated.WithX(rect.Right - _viewport.Width);
            }
            else if (rect.X < offset.X)
            {
                updated = updated.WithX(rect.X);
            }

            if (updated == offset)
            {
                return false;
            }

            SetLogicalOffset(updated, raiseInvalidated: true);
            return _offset != offset;
        }

        Control? ILogicalScrollable.GetControlInDirection(NavigationDirection direction, Control? from) => null;

        void ILogicalScrollable.RaiseScrollInvalidated(EventArgs e)
        {
            _scrollInvalidated?.Invoke(this, e);
        }

        internal Vector ApplyScrollOffsetShift(Vector shift, bool raiseInvalidated)
        {
            if (shift == default)
            {
                return default;
            }

            var previous = _offset;
            SetLogicalOffset(previous + shift, raiseInvalidated, invalidateMeasure: !_isLayoutInProgress);
            return _offset - previous;
        }

        private void SetExtent(Size extent)
        {
            if (_extent == extent)
            {
                return;
            }

            _extent = extent;
            _scrollSizeCacheValid = false;
            if (SetLogicalOffset(_offset, raiseInvalidated: false) != default)
            {
                _scrollInvalidated?.Invoke(this, EventArgs.Empty);
                return;
            }

            _scrollInvalidated?.Invoke(this, EventArgs.Empty);
        }

        private void SetViewport(Size viewport, bool raiseInvalidated, bool invalidateMeasure, bool forceInvalidate = false)
        {
            if (_viewport == viewport)
            {
                if (raiseInvalidated && forceInvalidate)
                {
                    _scrollInvalidated?.Invoke(this, EventArgs.Empty);
                }

                return;
            }

            _viewport = viewport;
            _scrollSizeCacheValid = false;
            var coerced = CoerceOffset(_offset);
            _offset = coerced;
            if (UsesLogicalScrolling || !_viewportManager.HasScroller)
            {
                _viewportManager.UpdateViewportFromLogicalScroll(_viewport, _offset, invalidateMeasure && !_isLayoutInProgress);
            }
            if (raiseInvalidated)
            {
                _scrollInvalidated?.Invoke(this, EventArgs.Empty);
            }
        }

        private Vector SetLogicalOffset(Vector offset, bool raiseInvalidated, bool invalidateMeasure = true)
        {
            var coerced = CoerceOffset(offset);
            if (coerced == _offset)
            {
                return default;
            }

            var previous = _offset;
            _offset = coerced;
            if (UsesLogicalScrolling)
            {
                _viewportManager.UpdateViewportFromLogicalScroll(_viewport, _offset, invalidateMeasure && !_isLayoutInProgress);
            }

            if (raiseInvalidated)
            {
                _scrollInvalidated?.Invoke(this, EventArgs.Empty);
            }

            return _offset - previous;
        }

        private Vector CoerceOffset(Vector offset)
        {
            var maxX = Math.Max(_extent.Width - _viewport.Width, 0);
            var maxY = Math.Max(_extent.Height - _viewport.Height, 0);
            var x = Clamp(offset.X, 0, maxX);
            var y = Clamp(offset.Y, 0, maxY);

            if (_scrollAxesConfigured)
            {
                if (!_canHorizontallyScroll)
                {
                    x = 0;
                }

                if (!_canVerticallyScroll)
                {
                    y = 0;
                }
            }

            return new Vector(x, y);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private void OnCanScrollChanged()
        {
            SetLogicalOffset(_offset, raiseInvalidated: true);
            InvalidateMeasure();
        }

        private void UpdateLogicalScrollingState()
        {
            var usesLogicalScrolling = IsLogicalScrollEnabled && _isLogicalScrollActive;
            if (_usesLogicalScrolling == usesLogicalScrolling)
            {
                return;
            }

            _usesLogicalScrolling = usesLogicalScrolling;
            _viewportManager.OnLayoutChanged(Layout is VirtualizingLayout);
            _scrollInvalidated?.Invoke(this, EventArgs.Empty);
            InvalidateMeasure();
        }

        private Size GetScrollSize()
        {
            if (!_scrollSizeCacheValid)
            {
                Size size = default;
                var totalWidth = 0.0;
                var totalHeight = 0.0;
                var realizedCount = 0;
                var usedNestedScrollSize = false;
                var axis = GetPreferredScrollAxis();

                for (var i = 0; i < Children.Count; i++)
                {
                    var child = Children[i];
                    var info = GetVirtualizationInfo(child);
                    if (!info.IsRealized)
                    {
                        continue;
                    }

                    size = child.Bounds.Size;
                    if (size.Width <= 0 || size.Height <= 0)
                    {
                        size = child.DesiredSize;
                    }

                    if (size.Width <= 0 || size.Height <= 0)
                    {
                        continue;
                    }

                    if (TryGetNestedRepeaterScrollSize(child, out var nestedSize))
                    {
                        size = ApplyNestedScrollSize(size, nestedSize, axis);
                        usedNestedScrollSize = true;
                    }

                    totalWidth += size.Width;
                    totalHeight += size.Height;
                    realizedCount++;
                }

                if (realizedCount > 0)
                {
                    size = new Size(totalWidth / realizedCount, totalHeight / realizedCount);
                }

                if (size.Width > 0 && size.Height > 0)
                {
                    _scrollSizeCache = size;
                }
                else if (_scrollSizeCache.Width <= 0 || _scrollSizeCache.Height <= 0)
                {
                    if (Layout is UniformGridLayout grid)
                    {
                        _scrollSizeCache = new Size(grid.MinItemWidth, grid.MinItemHeight);
                    }
                    else
                    {
                        _scrollSizeCache = new Size(50, 50);
                    }
                }

                _scrollSizeCacheUsesNested = usedNestedScrollSize;
                _scrollSizeCacheValid = true;
            }

            var width = Math.Max(1, _scrollSizeCache.Width);
            var height = Math.Max(1, _scrollSizeCache.Height);

            if (!_scrollSizeCacheUsesNested && Layout is StackLayout stack)
            {
                if (stack.Orientation == Orientation.Horizontal)
                {
                    width = Math.Max(1, width + stack.Spacing);
                }
                else
                {
                    height = Math.Max(1, height + stack.Spacing);
                }
            }
            else if (!_scrollSizeCacheUsesNested && Layout is NonVirtualizingStackLayout nonVirtualizingStack)
            {
                if (nonVirtualizingStack.Orientation == Orientation.Horizontal)
                {
                    width = Math.Max(1, width + nonVirtualizingStack.Spacing);
                }
                else
                {
                    height = Math.Max(1, height + nonVirtualizingStack.Spacing);
                }
            }
            else if (!_scrollSizeCacheUsesNested && Layout is WrapLayout wrap)
            {
                if (wrap.Orientation == Orientation.Horizontal)
                {
                    height = Math.Max(1, height + wrap.VerticalSpacing);
                }
                else
                {
                    width = Math.Max(1, width + wrap.HorizontalSpacing);
                }
            }
            else if (!_scrollSizeCacheUsesNested && Layout is UniformGridLayout uniformGrid)
            {
                if (uniformGrid.Orientation == Orientation.Horizontal)
                {
                    height = Math.Max(1, height + uniformGrid.MinRowSpacing);
                }
                else
                {
                    width = Math.Max(1, width + uniformGrid.MinColumnSpacing);
                }
            }

            return new Size(width, height);
        }

        private static bool TryGetNestedRepeaterScrollSize(Control child, out Size scrollSize)
        {
            if (child is ItemsRepeater itemsRepeater)
            {
                scrollSize = ((ILogicalScrollable)itemsRepeater).ScrollSize;
                return scrollSize.Width > 0 || scrollSize.Height > 0;
            }

            var nestedRepeater = child.GetVisualDescendants().OfType<ItemsRepeater>().FirstOrDefault();
            if (nestedRepeater != null)
            {
                scrollSize = ((ILogicalScrollable)nestedRepeater).ScrollSize;
                return scrollSize.Width > 0 || scrollSize.Height > 0;
            }

            scrollSize = default;
            return false;
        }

        private enum ScrollAxis
        {
            Both,
            Vertical,
            Horizontal
        }

        private ScrollAxis GetPreferredScrollAxis()
        {
            if (_scrollAxesConfigured)
            {
                if (_canVerticallyScroll && !_canHorizontallyScroll)
                {
                    return ScrollAxis.Vertical;
                }

                if (_canHorizontallyScroll && !_canVerticallyScroll)
                {
                    return ScrollAxis.Horizontal;
                }
            }

            return Layout switch
            {
                StackLayout stack => stack.Orientation == Orientation.Horizontal ? ScrollAxis.Horizontal : ScrollAxis.Vertical,
                NonVirtualizingStackLayout nonVirtualizingStack => nonVirtualizingStack.Orientation == Orientation.Horizontal ? ScrollAxis.Horizontal : ScrollAxis.Vertical,
                WrapLayout wrap => wrap.Orientation == Orientation.Horizontal ? ScrollAxis.Vertical : ScrollAxis.Horizontal,
                UniformGridLayout uniformGrid => uniformGrid.Orientation == Orientation.Horizontal ? ScrollAxis.Vertical : ScrollAxis.Horizontal,
                _ => ScrollAxis.Both
            };
        }

        private static Size ApplyNestedScrollSize(Size childSize, Size nestedSize, ScrollAxis axis)
        {
            var nestedWidth = nestedSize.Width > 0 ? nestedSize.Width : childSize.Width;
            var nestedHeight = nestedSize.Height > 0 ? nestedSize.Height : childSize.Height;

            return axis switch
            {
                ScrollAxis.Vertical => new Size(childSize.Width, Math.Min(childSize.Height, nestedHeight)),
                ScrollAxis.Horizontal => new Size(Math.Min(childSize.Width, nestedWidth), childSize.Height),
                _ => new Size(
                    Math.Min(childSize.Width, nestedWidth),
                    Math.Min(childSize.Height, nestedHeight))
            };
        }

    }
}
