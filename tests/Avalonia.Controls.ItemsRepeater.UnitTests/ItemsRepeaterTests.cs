using System.Collections.ObjectModel;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Xunit;

namespace Avalonia.Controls.UnitTests;

public class ItemsRepeaterTests
{
    [AvaloniaFact]
    public void Can_Reassign_Items()
    {
        var target = new ItemsRepeater();
        target.ItemsSource = new ObservableCollection<string>();
        target.ItemsSource = new ObservableCollection<string>();
    }

    [AvaloniaFact]
    public void Can_Reassign_Items_To_Null()
    {
        var target = new ItemsRepeater();
        target.ItemsSource = new ObservableCollection<string>();
        target.ItemsSource = null;
    }

    [AvaloniaFact]
    public void Default_Layout_Is_Not_Shared()
    {
        var first = new ItemsRepeater();
        var second = new ItemsRepeater();

        Assert.NotNull(first.Layout);
        Assert.NotNull(second.Layout);
        Assert.NotSame(first.Layout, second.Layout);
        Assert.IsType<StackLayout>(first.Layout);
        Assert.IsType<StackLayout>(second.Layout);
    }

    [AvaloniaFact]
    public void Default_Layout_Does_Not_Propagate_Property_Changes()
    {
        var first = new ItemsRepeater();
        var second = new ItemsRepeater();

        var firstLayout = (StackLayout)first.Layout!;
        var secondLayout = (StackLayout)second.Layout!;

        firstLayout.Orientation = Orientation.Horizontal;
        firstLayout.Spacing = 17;

        Assert.Equal(Orientation.Horizontal, firstLayout.Orientation);
        Assert.NotEqual(Orientation.Horizontal, secondLayout.Orientation);
        Assert.NotEqual(17, secondLayout.Spacing);
    }
}
