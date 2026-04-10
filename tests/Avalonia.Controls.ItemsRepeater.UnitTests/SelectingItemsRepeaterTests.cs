using Avalonia;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Selection;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.UnitTests;

public class SelectingItemsRepeaterTests
{
    private sealed class TestItem
    {
        public TestItem(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public int Id { get; }
        public string Name { get; }
        public override string ToString() => Name;
    }

    [AvaloniaFact]
    public void SelectedIndex_And_SelectedItem_Are_Synced()
    {
        var items = new[] { "a", "b", "c" };
        var target = new SelectingItemsRepeater
        {
            ItemsSource = items
        };

        target.SelectedIndex = 1;

        Assert.Equal(1, target.SelectedIndex);
        Assert.Equal("b", target.SelectedItem);
        Assert.Equal(new[] { "b" }, target.SelectedItems!.Cast<string>());

        target.SelectedItem = "c";

        Assert.Equal(2, target.SelectedIndex);
        Assert.Equal("c", target.SelectedItem);
        Assert.Equal(new[] { "c" }, target.SelectedItems!.Cast<string>());

        target.SelectedIndex = -1;

        Assert.Equal(-1, target.SelectedIndex);
        Assert.Null(target.SelectedItem);
        Assert.Empty(target.SelectedItems!);
    }

    [AvaloniaFact]
    public void SelectedValue_Uses_Binding_To_Select_Item()
    {
        var items = new[]
        {
            new TestItem(1, "one"),
            new TestItem(2, "two"),
            new TestItem(3, "three"),
        };

        var target = new SelectingItemsRepeater
        {
            ItemsSource = items,
            SelectedValueBinding = new Binding(nameof(TestItem.Id))
        };

        target.SelectedValue = 2;

        Assert.Equal(1, target.SelectedIndex);
        Assert.Same(items[1], target.SelectedItem);
        Assert.Equal(2, target.SelectedValue);

        target.SelectedIndex = 0;

        Assert.Equal(1, target.SelectedValue);
    }

    [AvaloniaFact]
    public void SelectedItems_List_Updates_Selection_In_Multiple_Mode()
    {
        var items = new[] { "alpha", "beta", "gamma" };
        var target = new SelectingItemsRepeater
        {
            ItemsSource = items,
            SelectionMode = SelectionMode.Multiple
        };

        var selected = target.SelectedItems!;
        selected.Add(items[0]);
        selected.Add(items[2]);

        Assert.Equal(new[] { 0, 2 }, target.Selection.SelectedIndexes);
        Assert.Equal(new[] { "alpha", "gamma" }, target.Selection.SelectedItems.Cast<string>());
    }

    [AvaloniaFact]
    public void AlwaysSelected_Selects_First_Item_When_ItemsSource_Set()
    {
        var target = new SelectingItemsRepeater
        {
            SelectionMode = SelectionMode.AlwaysSelected,
            ItemsSource = new[] { "first", "second" }
        };

        Assert.Equal(0, target.SelectedIndex);
        Assert.Equal("first", target.SelectedItem);
    }

    [AvaloniaFact]
    public void Assigning_SelectionModel_With_Different_Source_Throws()
    {
        var items = new List<string> { "a", "b" };
        var target = new SelectingItemsRepeater
        {
            ItemsSource = items
        };

        var otherSource = new List<string> { "x", "y" };
        var selection = new SelectionModel<object?> { Source = otherSource };

        Assert.Throws<ArgumentException>(() => target.Selection = selection);
    }
}

public class SelectingItemsRepeaterHeadlessTests
{
    private static readonly AttachedProperty<bool> s_isSelectedManagedProperty =
        (AttachedProperty<bool>)typeof(SelectingItemsRepeater)
            .GetField("IsSelectedManagedProperty", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

    private static (Window window, SelectingItemsRepeater repeater) CreateRepeater(IList items)
    {
        var repeater = new SelectingItemsRepeater
        {
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<object?>((_, __) => new Border
            {
                Width = 60,
                Height = 20
            })
        };

        var window = new Window
        {
            Width = 200,
            Height = 120,
            Content = new ScrollViewer { Content = repeater }
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, repeater);
    }

    [AvaloniaFact]
    public void SelectedIndex_Sets_IsSelected_On_Realized_Elements()
    {
        var (window, repeater) = CreateRepeater(new[] { "a", "b", "c" });

        repeater.SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();

        var element0 = repeater.GetOrCreateElement(0);
        var element1 = repeater.GetOrCreateElement(1);

        Assert.False(SelectingItemsRepeater.GetIsSelected(element0));
        Assert.True(SelectingItemsRepeater.GetIsSelected(element1));

        window.Close();
    }

    [AvaloniaFact]
    public void Setting_IsSelected_On_Container_Updates_Selection()
    {
        var (window, repeater) = CreateRepeater(new[] { "a", "b", "c" });

        var element = repeater.GetOrCreateElement(2);
        SelectingItemsRepeater.SetIsSelected(element, true);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, repeater.SelectedIndex);
        Assert.Equal("c", repeater.SelectedItem);

        SelectingItemsRepeater.SetIsSelected(element, false);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(-1, repeater.SelectedIndex);

        window.Close();
    }

    [AvaloniaFact]
    public void SelectionChanged_Raises_Added_And_Removed_Items()
    {
        var (window, repeater) = CreateRepeater(new[] { "a", "b", "c" });

        var added = new List<object?>();
        var removed = new List<object?>();

        repeater.SelectionChanged += (_, args) =>
        {
            added.AddRange(args.AddedItems.Cast<object?>());
            removed.AddRange(args.RemovedItems.Cast<object?>());
        };

        repeater.SelectedIndex = 0;
        Dispatcher.UIThread.RunJobs();

        repeater.SelectedIndex = 2;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(new object?[] { "a", "c" }, added);
        Assert.Equal(new object?[] { "a" }, removed);

        window.Close();
    }

    [AvaloniaFact]
    public void Unselected_Realized_Containers_Do_Not_Set_Local_IsSelected_Value()
    {
        var (window, repeater) = CreateRepeater(new[] { "a", "b", "c" });

        var element = repeater.GetOrCreateElement(0);
        Dispatcher.UIThread.RunJobs();

        Assert.False(element.IsSet(SelectingItemsRepeater.IsSelectedProperty));
        Assert.False(SelectingItemsRepeater.GetIsSelected(element));

        window.Close();
    }

    [AvaloniaFact]
    public void Deselecting_Managed_Container_Releases_Internal_Selection_State()
    {
        var (window, repeater) = CreateRepeater(new[] { "a", "b", "c" });

        repeater.SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();

        var element = repeater.GetOrCreateElement(1);

        Assert.True(GetIsSelectedManaged(element));
        Assert.True(element.IsSet(SelectingItemsRepeater.IsSelectedProperty));

        repeater.SelectedIndex = -1;
        Dispatcher.UIThread.RunJobs();

        Assert.False(GetIsSelectedManaged(element));
        Assert.False(element.IsSet(SelectingItemsRepeater.IsSelectedProperty));
        Assert.False(SelectingItemsRepeater.GetIsSelected(element));

        window.Close();
    }

    [AvaloniaFact]
    public void External_IsSelected_Value_Remains_Set_When_Selection_Model_Deselects()
    {
        var (window, repeater) = CreateRepeater(new[] { "a", "b", "c" });

        var element = repeater.GetOrCreateElement(1);
        SelectingItemsRepeater.SetIsSelected(element, true);
        Dispatcher.UIThread.RunJobs();

        repeater.SelectedIndex = -1;
        Dispatcher.UIThread.RunJobs();

        Assert.True(element.IsSet(SelectingItemsRepeater.IsSelectedProperty));
        Assert.False(SelectingItemsRepeater.GetIsSelected(element));

        window.Close();
    }

    [AvaloniaFact]
    public void Hold_With_Mouse_Drag_Does_Not_Select_Item_On_Release()
    {
        var repeater = new SelectingItemsRepeater
        {
            AutoScrollToSelectedItem = false,
            ItemsSource = new[] { "a", "b", "c" },
            ItemTemplate = new FuncDataTemplate<string>((_, __) =>
            {
                var border = new Border
                {
                    Width = 100,
                    Height = 40
                };
                InputElement.SetIsHoldWithMouseEnabled(border, true);
                return border;
            })
        };

        var window = new Window
        {
            Width = 200,
            Height = 120,
            Content = new ScrollViewer { Content = repeater }
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var element = repeater.GetOrCreateElement(0);
        var center = element.TranslatePoint(new Point(element.Bounds.Width / 2, element.Bounds.Height / 2), window);
        Assert.NotNull(center);

        window.MouseMove(center!.Value);
        window.MouseDown(center.Value, MouseButton.Left);

        var dragPoint = new Point(center.Value.X + 20, center.Value.Y + 10);
        window.MouseMove(dragPoint, RawInputModifiers.LeftMouseButton);
        window.MouseUp(dragPoint, MouseButton.Left);

        Assert.Equal(-1, repeater.SelectedIndex);

        window.Close();
    }

    [AvaloniaFact]
    public void WrapLayout_Slow_Scroll_Preserves_Expected_Line_When_Scrolling_Back()
    {
        var random = new Random(42);
        const int itemCount = 300;
        const double itemWidth = 80;
        const double horizontalSpacing = 10;
        const double verticalSpacing = 10;
        var items = Enumerable.Range(0, itemCount)
            .Select(_ => new SizedSelectionItem(itemWidth, 20 + random.Next(120)))
            .ToList();

        var (window, scroller, repeater) = CreateScrollableWrapRepeater(items, horizontalSpacing, verticalSpacing);

        var firstLineY = repeater.Children.Min(child => child.Bounds.Y);
        var itemsPerLine = repeater.Children.Count(child => Math.Abs(child.Bounds.Y - firstLineY) <= 0.5);
        Assert.True(itemsPerLine > 0);

        var lineHeights = new double[(int)Math.Ceiling((double)itemCount / itemsPerLine)];
        for (var lineIndex = 0; lineIndex < lineHeights.Length; lineIndex++)
        {
            var startIndex = lineIndex * itemsPerLine;
            var count = Math.Min(itemsPerLine, itemCount - startIndex);
            lineHeights[lineIndex] = items
                .Skip(startIndex)
                .Take(count)
                .Max(item => item.Height);
        }

        var lineStarts = new double[lineHeights.Length];
        var position = 0.0;
        for (var i = 0; i < lineHeights.Length; i++)
        {
            lineStarts[i] = position;
            position += lineHeights[i];
            if (i < lineHeights.Length - 1)
            {
                position += verticalSpacing;
            }
        }

        var maxOffset = Math.Max(0, scroller.Extent.Height - scroller.Viewport.Height);
        const int steps = 8;
        var offsets = Enumerable.Range(0, steps + 1)
            .Select(i => maxOffset * i / steps)
            .Concat(Enumerable.Range(0, steps).Select(i => maxOffset * (steps - i - 1) / steps));

        foreach (var offset in offsets)
        {
            scroller.Offset = new Vector(0, offset);
            Dispatcher.UIThread.RunJobs();

            var viewportStart = scroller.Offset.Y;
            var viewportEnd = viewportStart + scroller.Viewport.Height;
            var expectedLineIndex = FindLineIndex(lineStarts, lineHeights, viewportStart);
            var expectedStartIndex = expectedLineIndex * itemsPerLine;
            var visibleIndices = repeater.Children
                .Where(child => child.Bounds.Bottom >= viewportStart && child.Bounds.Y <= viewportEnd)
                .Select(repeater.GetElementIndex)
                .ToList();

            Assert.Contains(
                visibleIndices,
                index => index >= expectedStartIndex && index < expectedStartIndex + itemsPerLine);
        }

        window.Close();
    }

    private static (Window window, ScrollViewer scroller, SelectingItemsRepeater repeater) CreateScrollableWrapRepeater(
        IReadOnlyList<SizedSelectionItem> items,
        double horizontalSpacing,
        double verticalSpacing)
    {
        var repeater = new SelectingItemsRepeater
        {
            AutoScrollToSelectedItem = false,
            Layout = new WrapLayout
            {
                Orientation = Orientation.Horizontal,
                HorizontalSpacing = horizontalSpacing,
                VerticalSpacing = verticalSpacing
            },
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<SizedSelectionItem>((item, _) => new Border
            {
                Width = item.Width,
                Height = item.Height
            })
        };

        var scroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = repeater
        };

        var window = new Window
        {
            Width = 320,
            Height = 240,
            Content = scroller
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, scroller, repeater);
    }

    private static int FindLineIndex(double[] lineStarts, double[] lineHeights, double offset)
    {
        var result = 0;
        for (var i = 0; i < lineStarts.Length; i++)
        {
            var lineStart = lineStarts[i];
            var lineEnd = lineStart + lineHeights[i];
            if (offset < lineEnd || i == lineStarts.Length - 1)
            {
                result = i;
                break;
            }
        }

        return result;
    }

    private static bool GetIsSelectedManaged(Control container) => container.GetValue(s_isSelectedManagedProperty);

    private sealed class SizedSelectionItem
    {
        public SizedSelectionItem(double width, double height)
        {
            Width = width;
            Height = height;
        }

        public double Width { get; }
        public double Height { get; }
    }
}
