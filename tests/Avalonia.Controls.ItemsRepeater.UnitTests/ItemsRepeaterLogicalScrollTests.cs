// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.UnitTests;

public class ItemsRepeaterLogicalScrollTests
{
    private static (Window window, ItemsRepeater repeater) CreateRepeater<T>(
        AttachedLayout layout,
        IReadOnlyList<T> items,
        Func<T, Control> factory,
        Size windowSize)
    {
        var repeater = new ItemsRepeater
        {
            Layout = layout,
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<T>((item, _) => factory(item))
        };

        var window = new Window
        {
            Width = windowSize.Width,
            Height = windowSize.Height,
            Content = repeater
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, repeater);
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
            Content = repeater
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

    [AvaloniaFact]
    public void LogicalScroll_Raises_ScrollInvalidated_On_Offset_Change()
    {
        var items = Enumerable.Range(0, 200).ToList();
        var (window, repeater) = CreateRepeater(
            new StackLayout { Orientation = Orientation.Vertical },
            items,
            _ => new Border { Width = 80, Height = 20 },
            new Size(200, 120));

        var logical = (ILogicalScrollable)repeater;
        logical.CanVerticallyScroll = true;
        var invalidatedCount = 0;
        logical.ScrollInvalidated += (_, _) => invalidatedCount++;

        ((IScrollable)repeater).Offset = new Vector(0, 200);

        Assert.True(invalidatedCount > 0);
        Assert.True(((IScrollable)repeater).Offset.Y > 0);

        window.Close();
    }

    [AvaloniaFact]
    public void LogicalScroll_Coerces_Offset_When_Extent_Smaller_Than_Viewport()
    {
        var items = Enumerable.Range(0, 2).ToList();
        var (window, repeater) = CreateRepeater(
            new StackLayout { Orientation = Orientation.Vertical },
            items,
            _ => new Border { Width = 80, Height = 20 },
            new Size(200, 200));

        var logical = (ILogicalScrollable)repeater;
        logical.CanVerticallyScroll = true;
        ((IScrollable)repeater).Offset = new Vector(0, 100);

        Assert.Equal(0, ((IScrollable)repeater).Offset.Y);

        window.Close();
    }

    [AvaloniaFact]
    public void LogicalScroll_Clamps_Offset_To_Extent()
    {
        var items = Enumerable.Range(0, 200).ToList();
        var (window, repeater) = CreateRepeater(
            new StackLayout { Orientation = Orientation.Vertical },
            items,
            _ => new Border { Width = 80, Height = 20 },
            new Size(200, 120));

        var logical = (ILogicalScrollable)repeater;
        logical.CanVerticallyScroll = true;

        var maxY = Math.Max(logical.Extent.Height - logical.Viewport.Height, 0);
        ((IScrollable)repeater).Offset = new Vector(0, maxY + 500);

        Assert.Equal(maxY, ((IScrollable)repeater).Offset.Y, 3);

        window.Close();
    }

    [AvaloniaFact]
    public void LogicalScroll_Disabled_Axis_Coerces_To_Zero()
    {
        var items = Enumerable.Range(0, 200).ToList();
        var (window, repeater) = CreateRepeater(
            new StackLayout { Orientation = Orientation.Vertical },
            items,
            _ => new Border { Width = 80, Height = 20 },
            new Size(200, 120));

        var logical = (ILogicalScrollable)repeater;
        logical.CanVerticallyScroll = true;
        ((IScrollable)repeater).Offset = new Vector(0, 100);

        logical.CanVerticallyScroll = false;

        Assert.Equal(0, ((IScrollable)repeater).Offset.Y);

        window.Close();
    }

    [AvaloniaFact]
    public void LogicalScroll_BringIntoView_Returns_False_When_Visible()
    {
        var items = Enumerable.Range(0, 50).ToList();
        var (window, repeater) = CreateRepeater(
            new StackLayout { Orientation = Orientation.Vertical },
            items,
            _ => new Border { Width = 80, Height = 20 },
            new Size(200, 120));

        var logical = (ILogicalScrollable)repeater;
        var element = (Border)repeater.GetOrCreateElement(0);
        Dispatcher.UIThread.RunJobs();

        var result = logical.BringIntoView(element, new Rect(element.Bounds.Size));

        Assert.False(result);
        Assert.Equal(0, ((IScrollable)repeater).Offset.Y);

        window.Close();
    }

    [AvaloniaFact]
    public void LogicalScroll_BringIntoView_Returns_False_When_Detached()
    {
        var repeater = new ItemsRepeater();
        var logical = (ILogicalScrollable)repeater;
        var target = new Border();

        var result = logical.BringIntoView(target, new Rect(0, 0, 10, 10));

        Assert.False(result);
    }

    [AvaloniaFact]
    public void LogicalScroll_ScrollSize_Includes_StackLayout_Spacing_Vertical()
    {
        var items = Enumerable.Range(0, 50).ToList();
        var (window, repeater) = CreateRepeater(
            new StackLayout { Orientation = Orientation.Vertical, Spacing = 7 },
            items,
            _ => new Border { Width = 80, Height = 20 },
            new Size(200, 120));

        var scrollSize = ((ILogicalScrollable)repeater).ScrollSize;

        Assert.Equal(27, scrollSize.Height, 3);

        window.Close();
    }

    [AvaloniaFact]
    public void LogicalScroll_ScrollSize_Includes_StackLayout_Spacing_Horizontal()
    {
        var items = Enumerable.Range(0, 50).ToList();
        var (window, repeater) = CreateRepeater(
            new StackLayout { Orientation = Orientation.Horizontal, Spacing = 9 },
            items,
            _ => new Border { Width = 20, Height = 40 },
            new Size(120, 200));

        var scrollSize = ((ILogicalScrollable)repeater).ScrollSize;

        Assert.Equal(29, scrollSize.Width, 3);

        window.Close();
    }

    [AvaloniaFact]
    public void LogicalScroll_ScrollSize_Includes_WrapLayout_Spacing()
    {
        var items = Enumerable.Range(0, 200).ToList();
        var (window, repeater) = CreateRepeater(
            new WrapLayout { Orientation = Orientation.Horizontal, VerticalSpacing = 5 },
            items,
            _ => new Border { Width = 20, Height = 10 },
            new Size(100, 60));

        var scrollSize = ((ILogicalScrollable)repeater).ScrollSize;

        Assert.Equal(15, scrollSize.Height, 3);

        window.Close();
    }

    [AvaloniaFact]
    public void LogicalScroll_ScrollSize_Uses_UniformGrid_Minimums()
    {
        var items = Enumerable.Range(0, 200).ToList();
        var (window, repeater) = CreateRepeater(
            new UniformGridLayout
            {
                Orientation = Orientation.Horizontal,
                MinItemWidth = 30,
                MinItemHeight = 40,
                MinRowSpacing = 6,
                MinColumnSpacing = 8,
                MaximumRowsOrColumns = 2
            },
            items,
            _ => new Border { Width = 30, Height = 40 },
            new Size(120, 90));

        var scrollSize = ((ILogicalScrollable)repeater).ScrollSize;

        Assert.Equal(46, scrollSize.Height, 3);
        Assert.Equal(30, scrollSize.Width, 3);

        window.Close();
    }

    [AvaloniaFact]
    public void LogicalScroll_ScrollSize_Updates_After_Layout_Change()
    {
        var items = Enumerable.Range(0, 200).ToList();
        var (window, repeater) = CreateRepeater(
            new StackLayout { Orientation = Orientation.Vertical, Spacing = 0 },
            items,
            _ => new Border { Width = 80, Height = 20 },
            new Size(200, 120));

        var initial = ((ILogicalScrollable)repeater).ScrollSize;

        repeater.Layout = new StackLayout { Orientation = Orientation.Vertical, Spacing = 10 };
        Dispatcher.UIThread.RunJobs();

        var updated = ((ILogicalScrollable)repeater).ScrollSize;

        Assert.Equal(20, initial.Height, 3);
        Assert.Equal(30, updated.Height, 3);

        window.Close();
    }

    [AvaloniaFact]
    public void LogicalScroll_Uses_Inner_Repeater_ScrollSize_For_Groups()
    {
        var groups = Enumerable.Range(0, 5)
            .Select(i => new Group(i, Enumerable.Range(0, 200).ToList()))
            .ToList();

        var (window, repeater) = CreateRepeater(
            new StackLayout { Orientation = Orientation.Vertical, Spacing = 0 },
            groups,
            group => new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new Border { Width = 100, Height = 24 },
                    new ItemsRepeater
                    {
                        Layout = new StackLayout { Orientation = Orientation.Vertical, Spacing = 0 },
                        ItemsSource = group.Items,
                        ItemTemplate = new FuncDataTemplate<int>((_, __) => new Border { Width = 100, Height = 20 })
                    }
                }
            },
            new Size(200, 200));

        var outerElement = (Control)repeater.GetOrCreateElement(0);
        Dispatcher.UIThread.RunJobs();

        var innerRepeater = outerElement.GetVisualDescendants()
            .OfType<ItemsRepeater>()
            .FirstOrDefault();

        Assert.NotNull(innerRepeater);

        innerRepeater!.GetOrCreateElement(0);
        Dispatcher.UIThread.RunJobs();

        repeater.InvalidateMeasure();
        Dispatcher.UIThread.RunJobs();

        var scrollSize = ((ILogicalScrollable)repeater).ScrollSize;

        Assert.Equal(20, scrollSize.Height, 3);

        window.Close();
    }

    [AvaloniaFact]
    public void ScrollViewer_Uses_LogicalScroll_Sizes()
    {
        var items = Enumerable.Range(0, 200).ToList();
        var (window, scroller, repeater) = CreateScrollableRepeater(
            new StackLayout { Orientation = Orientation.Vertical, Spacing = 4 },
            items,
            _ => new Border { Width = 80, Height = 20 },
            new Size(200, 120),
            ScrollBarVisibility.Disabled,
            ScrollBarVisibility.Auto);

        var logical = (ILogicalScrollable)repeater;
        var scrollSize = logical.ScrollSize;
        var pageSize = logical.PageScrollSize;

        Assert.Equal(scrollSize, scroller.SmallChange);
        Assert.Equal(pageSize, scroller.LargeChange);

        window.Close();
    }

    private sealed class Group
    {
        public Group(int index, IReadOnlyList<int> items)
        {
            Index = index;
            Items = items;
        }

        public int Index { get; }
        public IReadOnlyList<int> Items { get; }
    }
}
