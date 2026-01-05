// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
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
        internal bool UsesLogicalScrolling => true;

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

        bool ILogicalScrollable.IsLogicalScrollEnabled => true;

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
            add => _scrollInvalidated += value;
            remove => _scrollInvalidated -= value;
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
            _viewportManager.UpdateViewportFromLogicalScroll(_viewport, _offset, invalidateMeasure && !_isLayoutInProgress);
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
            _viewportManager.UpdateViewportFromLogicalScroll(_viewport, _offset, invalidateMeasure && !_isLayoutInProgress);

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

        private Size GetScrollSize()
        {
            if (!_scrollSizeCacheValid)
            {
                Size size = default;
                var totalWidth = 0.0;
                var totalHeight = 0.0;
                var realizedCount = 0;

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

                _scrollSizeCacheValid = true;
            }

            var width = Math.Max(1, _scrollSizeCache.Width);
            var height = Math.Max(1, _scrollSizeCache.Height);

            if (Layout is StackLayout stack)
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
            else if (Layout is NonVirtualizingStackLayout nonVirtualizingStack)
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
            else if (Layout is WrapLayout wrap)
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
            else if (Layout is UniformGridLayout uniformGrid)
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

    }
}
