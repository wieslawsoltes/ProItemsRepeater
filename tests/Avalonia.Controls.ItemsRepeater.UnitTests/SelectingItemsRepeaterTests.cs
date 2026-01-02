using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
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
}
