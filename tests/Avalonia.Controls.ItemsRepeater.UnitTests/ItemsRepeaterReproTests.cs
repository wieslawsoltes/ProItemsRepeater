using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.UnitTests;

public class ItemsRepeaterReproTests
{
    private sealed class ReproViewModel
    {
        public List<int> Albums { get; } = Enumerable.Range(0, 400).ToList();
    }

    [AvaloniaFact]
    public void ItemsRepeater_UniformGridLayout_Repro_Renders_Items()
    {
        var xaml = @"
<Window xmlns='https://github.com/avaloniaui'
        xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
        Width='1000'
        Height='800'>
  <ScrollViewer x:Name='scroller'
                HorizontalScrollBarVisibility='Disabled'
                VerticalScrollBarVisibility='Auto'>
    <ItemsRepeater x:Name='repeater'
                   Background='Transparent'
                   ItemsSource='{Binding Albums}'
                   Margin='0 60'>
      <ItemsRepeater.Layout>
        <UniformGridLayout ItemsJustification='SpaceEvenly'
                           MinRowSpacing='60'
                           Orientation='Horizontal'
                           MaximumRowsOrColumns='4' />
      </ItemsRepeater.Layout>
      <ItemsRepeater.ItemTemplate>
        <DataTemplate>
          <Border BorderBrush='Black'
                  Height='200'
                  Width='200'
                  BorderThickness='1'>
            <Panel>
              <Panel>
                <TextBlock Text='{Binding}'
                           FontSize='40'
                           VerticalAlignment='Center'
                           HorizontalAlignment='Center' />
              </Panel>
            </Panel>
          </Border>
        </DataTemplate>
      </ItemsRepeater.ItemTemplate>
    </ItemsRepeater>
  </ScrollViewer>
</Window>";

        var window = (Window)AvaloniaRuntimeXamlLoader.Load(xaml);
        window.DataContext = new ReproViewModel();

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var repeater = window.FindControl<ItemsRepeater>("repeater");
        Assert.NotNull(repeater);

        var resolvedRepeater = repeater!;
        Assert.NotNull(resolvedRepeater.ItemsSourceView);
        Assert.Equal(400, resolvedRepeater.ItemsSourceView!.Count);
        Assert.True(resolvedRepeater.Children.Count > 0);
        Assert.True(resolvedRepeater.Children.Count < 400);

        var element0 = (Border)resolvedRepeater.GetOrCreateElement(0);
        var element1 = (Border)resolvedRepeater.GetOrCreateElement(1);
        var element2 = (Border)resolvedRepeater.GetOrCreateElement(2);
        var element4 = (Border)resolvedRepeater.GetOrCreateElement(4);

        Dispatcher.UIThread.RunJobs();

        var textBlock = element0.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault();
        Assert.NotNull(textBlock);
        Assert.Equal("0", textBlock!.Text);

        Assert.Equal(element0.Bounds.Y, element1.Bounds.Y, 3);
        Assert.Equal(element1.Bounds.Y, element2.Bounds.Y, 3);
        Assert.True(element1.Bounds.X > element0.Bounds.X);
        Assert.True(element2.Bounds.X > element1.Bounds.X);

        var rowDelta = element4.Bounds.Y - element0.Bounds.Y;
        Assert.True(rowDelta >= element0.Bounds.Height + 60 - 0.01);
        Assert.Equal(element0.Bounds.X, element4.Bounds.X, 3);
        Assert.Equal(200, element0.Bounds.Width, 3);
        Assert.Equal(200, element0.Bounds.Height, 3);
    }
}
