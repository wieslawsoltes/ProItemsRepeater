using System.Collections.ObjectModel;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Xunit;

namespace Avalonia.Controls.UnitTests;

public class XamlStyleTests
{
    [AvaloniaFact]
    public void Style_Can_Use_NthChild_Selector_With_ItemsRepeater()
    {
        var xaml = @"
<Window xmlns='https://github.com/avaloniaui'
             xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
    <Window.Styles>
        <Style Selector='TextBlock'>
            <Setter Property='Foreground' Value='Transparent'/>
        </Style>
        <Style Selector='TextBlock:nth-child(2n)'>
            <Setter Property='Foreground' Value='{Binding}'/>
        </Style>
    </Window.Styles>
    <ItemsRepeater x:Name='list' />
</Window>";
        var window = (Window)AvaloniaRuntimeXamlLoader.Load(xaml);
        var collection = new ObservableCollection<IBrush>()
        {
            Brushes.Red, Brushes.Green, Brushes.Blue
        };

        var list = Assert.IsType<ItemsRepeater>(window.FindControl<ItemsRepeater>("list"));
        list.ItemsSource = collection;

        window.Show();

        IEnumerable<IBrush?> GetColors() => Enumerable.Range(0, list.ItemsSourceView!.Count)
            .Select(t => (list.GetOrCreateElement(t) as TextBlock)!.Foreground);

        Assert.Equal(new IBrush?[] { Brushes.Transparent, Brushes.Green, Brushes.Transparent }, GetColors());

        collection.Remove(Brushes.Green);

        Assert.Equal(new IBrush?[] { Brushes.Transparent, Brushes.Blue }, GetColors().ToList());

        collection.Add(Brushes.Violet);
        collection.Add(Brushes.Black);

        Assert.Equal(new IBrush?[] { Brushes.Transparent, Brushes.Blue, Brushes.Transparent, Brushes.Black }, GetColors());
    }
}
