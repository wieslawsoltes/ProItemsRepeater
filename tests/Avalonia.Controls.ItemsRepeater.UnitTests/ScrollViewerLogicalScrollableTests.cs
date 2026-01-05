// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using Avalonia;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Controls.UnitTests;

public class ScrollViewerLogicalScrollableTests
{
    [AvaloniaFact]
    public void Extent_Offset_And_Viewport_Should_Be_Read_From_ILogicalScrollable()
    {
        var scrollable = new TestScrollable
        {
            Extent = new Size(100, 100),
            Offset = new Vector(50, 50),
            Viewport = new Size(25, 25),
        };

        var scrollViewer = new ScrollViewer
        {
            Content = scrollable,
            Template = CreateScrollViewerTemplate(),
        };

        var window = new Window
        {
            Width = 200,
            Height = 200,
            Content = scrollViewer,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(scrollable.Extent, scrollViewer.Extent);
        Assert.Equal(scrollable.Offset, scrollViewer.Offset);
        Assert.Equal(scrollable.Viewport, scrollViewer.Viewport);

        scrollable.Extent = new Size(200, 200);
        scrollable.Offset = new Vector(100, 100);
        scrollable.Viewport = new Size(50, 50);

        Dispatcher.UIThread.RunJobs();

        Assert.Equal(scrollable.Extent, scrollViewer.Extent);
        Assert.Equal(scrollable.Offset, scrollViewer.Offset);
        Assert.Equal(scrollable.Viewport, scrollViewer.Viewport);

        window.Close();
    }

    private static FuncControlTemplate CreateScrollViewerTemplate()
    {
        return new FuncControlTemplate<ScrollViewer>((parent, scope) =>
            new Panel
            {
                Children =
                {
                    new ScrollContentPresenter
                    {
                        Name = "PART_ContentPresenter",
                    }.RegisterInNameScope(scope),
                }
            });
    }

    private sealed class TestScrollable : Control, ILogicalScrollable
    {
        private Size _extent;
        private Vector _offset;
        private Size _viewport;
        private EventHandler? _scrollInvalidated;

        public bool CanHorizontallyScroll { get; set; }
        public bool CanVerticallyScroll { get; set; }
        public bool IsLogicalScrollEnabled { get; set; } = true;
        public Size AvailableSize { get; private set; }

        public event EventHandler? ScrollInvalidated
        {
            add => _scrollInvalidated += value;
            remove => _scrollInvalidated -= value;
        }

        public Size Extent
        {
            get => _extent;
            set
            {
                _extent = value;
                _scrollInvalidated?.Invoke(this, EventArgs.Empty);
            }
        }

        public Vector Offset
        {
            get => _offset;
            set
            {
                _offset = value;
                _scrollInvalidated?.Invoke(this, EventArgs.Empty);
            }
        }

        public Size Viewport
        {
            get => _viewport;
            set
            {
                _viewport = value;
                _scrollInvalidated?.Invoke(this, EventArgs.Empty);
            }
        }

        public Size ScrollSize => new(double.PositiveInfinity, 1);
        public Size PageScrollSize => new(double.PositiveInfinity, Viewport.Height);

        public bool BringIntoView(Control target, Rect targetRect) => false;

        public void RaiseScrollInvalidated(EventArgs e)
        {
            _scrollInvalidated?.Invoke(this, e);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            AvailableSize = availableSize;
            return new Size(150, 150);
        }

        public Control? GetControlInDirection(NavigationDirection direction, Control? from) => null;
    }
}
