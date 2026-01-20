// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Controls.UnitTests;

public class ItemsRepeaterScrollViewerBindingTests
{
    [AvaloniaFact]
    public void ScrollViewer_Binds_CanScroll_To_LogicalScrollable()
    {
        var items = Enumerable.Range(0, 200).ToList();
        var (window, _, repeater) = CreateScrollableRepeater(
            new StackLayout { Orientation = Orientation.Vertical },
            items,
            _ => new Border { Width = 80, Height = 20 },
            new Size(200, 120),
            ScrollBarVisibility.Disabled,
            ScrollBarVisibility.Auto);

        var logical = (ILogicalScrollable)repeater;

        Assert.False(logical.CanHorizontallyScroll);
        Assert.True(logical.CanVerticallyScroll);

        window.Close();
    }

    [AvaloniaFact]
    public void ScrollViewer_Writes_Offset_To_LogicalScrollable()
    {
        var items = Enumerable.Range(0, 200).ToList();
        var (window, scroller, repeater) = CreateScrollableRepeater(
            new StackLayout { Orientation = Orientation.Vertical },
            items,
            _ => new Border { Width = 80, Height = 20 },
            new Size(200, 120),
            ScrollBarVisibility.Disabled,
            ScrollBarVisibility.Auto);

        scroller.Offset = new Vector(0, 200);
        Dispatcher.UIThread.RunJobs();

        var offset = ((IScrollable)repeater).Offset;

        Assert.Equal(scroller.Offset.Y, offset.Y, 3);

        window.Close();
    }

    [AvaloniaFact]
    public void ScrollViewer_Reads_Offset_From_LogicalScrollable()
    {
        var items = Enumerable.Range(0, 200).ToList();
        var (window, scroller, repeater) = CreateScrollableRepeater(
            new StackLayout { Orientation = Orientation.Vertical },
            items,
            _ => new Border { Width = 80, Height = 20 },
            new Size(200, 120),
            ScrollBarVisibility.Disabled,
            ScrollBarVisibility.Auto);

        var logical = (ILogicalScrollable)repeater;
        logical.CanVerticallyScroll = true;

        ((IScrollable)repeater).Offset = new Vector(0, 160);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(((IScrollable)repeater).Offset.Y, scroller.Offset.Y, 3);

        window.Close();
    }

    [AvaloniaFact]
    public void ScrollViewer_Reads_Viewport_And_Extent_From_LogicalScrollable()
    {
        var items = Enumerable.Range(0, 200).ToList();
        var (window, scroller, repeater) = CreateScrollableRepeater(
            new StackLayout { Orientation = Orientation.Vertical },
            items,
            _ => new Border { Width = 80, Height = 20 },
            new Size(200, 120),
            ScrollBarVisibility.Disabled,
            ScrollBarVisibility.Auto);

        var logical = (ILogicalScrollable)repeater;

        Assert.Equal(logical.Viewport, scroller.Viewport);
        Assert.Equal(logical.Extent, scroller.Extent);

        window.Close();
    }

    [AvaloniaFact]
    public void ScrollViewer_Uses_Physical_Scrolling_When_Logical_Disabled()
    {
        var items = Enumerable.Range(0, 50).ToList();
        var repeater = new ItemsRepeater
        {
            IsLogicalScrollEnabled = false,
            Layout = new StackLayout { Orientation = Orientation.Vertical },
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<int>((_, __) => new Border { Width = 80, Height = 20 })
        };

        var (window, scroller) = CreateScrollViewer(repeater, new Size(100, 100));

        scroller.Offset = new Vector(0, 30);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, repeater.Bounds.X, 3);
        Assert.Equal(-30, repeater.Bounds.Y, 3);

        window.Close();
    }

    [AvaloniaFact]
    public void PhysicalScroll_Offsets_Content_Bounds()
    {
        var content = new Border
        {
            Width = 300,
            Height = 300
        };

        var (window, scroller) = CreateScrollViewer(content, new Size(100, 100));

        scroller.Offset = new Vector(20, 30);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(-20, content.Bounds.X, 3);
        Assert.Equal(-30, content.Bounds.Y, 3);

        window.Close();
    }

    [AvaloniaFact]
    public void PhysicalScroll_Offsets_NonLogical_Scrollable_Bounds()
    {
        var content = new NonLogicalScrollableControl();
        var (window, scroller) = CreateScrollViewer(content, new Size(100, 100));

        scroller.Offset = new Vector(25, 35);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(-25, content.Bounds.X, 3);
        Assert.Equal(-35, content.Bounds.Y, 3);

        window.Close();
    }

    private static (Window window, ScrollViewer scroller, ItemsRepeater repeater) CreateScrollableRepeater<T>(
        AttachedLayout layout,
        IReadOnlyList<T> items,
        Func<T, Control> factory,
        Size windowSize,
        ScrollBarVisibility horizontalScrollBarVisibility,
        ScrollBarVisibility verticalScrollBarVisibility)
    {
        var repeater = new ItemsRepeater
        {
            Layout = layout,
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<T>((item, _) => factory(item))
        };

        var scroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = horizontalScrollBarVisibility,
            VerticalScrollBarVisibility = verticalScrollBarVisibility,
            Content = repeater,
            Template = CreateScrollViewerTemplate()
        };

        var window = new Window
        {
            Width = windowSize.Width,
            Height = windowSize.Height,
            Content = scroller
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, scroller, repeater);
    }

    private static (Window window, ScrollViewer scroller) CreateScrollViewer(Control content, Size windowSize)
    {
        var scroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content,
            Template = CreateScrollViewerTemplate()
        };

        var window = new Window
        {
            Width = windowSize.Width,
            Height = windowSize.Height,
            Content = scroller
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, scroller);
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
                        [!ScrollContentPresenter.ContentProperty] =
                            new TemplateBinding(ContentControl.ContentProperty),
                    }.RegisterInNameScope(scope),
                }
            });
    }

    private sealed class NonLogicalScrollableControl : Control, ILogicalScrollable
    {
        public bool CanHorizontallyScroll { get; set; }
        public bool CanVerticallyScroll { get; set; }
        public bool IsLogicalScrollEnabled => false;
        public Size ScrollSize => default;
        public Size PageScrollSize => default;
        public Size Extent => new(300, 300);
        public Size Viewport => Bounds.Size;
        public Vector Offset { get; set; }
        public event EventHandler? ScrollInvalidated;

        public bool BringIntoView(Control target, Rect targetRect) => false;

        public Control? GetControlInDirection(NavigationDirection direction, Control? from) => null;

        public void RaiseScrollInvalidated(EventArgs e)
        {
            ScrollInvalidated?.Invoke(this, e);
        }

        protected override Size MeasureOverride(Size availableSize) => new(300, 300);
    }
}
