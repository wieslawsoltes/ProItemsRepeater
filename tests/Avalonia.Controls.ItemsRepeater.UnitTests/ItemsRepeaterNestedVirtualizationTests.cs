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

public class ItemsRepeaterNestedVirtualizationTests
{
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

    [AvaloniaFact]
    public void NestedItemsRepeater_Uses_EffectiveViewport_For_Inner_Virtualization()
    {
        var groups = Enumerable.Range(0, 5)
            .Select(i => new Group(i, Enumerable.Range(0, 200).ToList()))
            .ToList();

        var outerRepeater = new ItemsRepeater
        {
            Layout = new StackLayout { Orientation = Orientation.Vertical, Spacing = 8 },
            ItemsSource = groups,
            ItemTemplate = new FuncDataTemplate<Group>((group, _) =>
            {
                var innerRepeater = new ItemsRepeater
                {
                    Layout = new StackLayout { Orientation = Orientation.Vertical },
                    ItemsSource = group.Items,
                    ItemTemplate = new FuncDataTemplate<int>((_, __) => new Border
                    {
                        Width = 120,
                        Height = 20
                    })
                };

                return new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        new TextBlock { Text = $"Group {group.Index}" },
                        innerRepeater
                    }
                };
            })
        };

        var scroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = outerRepeater
        };

        var window = new Window
        {
            Width = 200,
            Height = 200,
            Content = scroller
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var outerElement = (Control)outerRepeater.GetOrCreateElement(0);
        Dispatcher.UIThread.RunJobs();

        var innerRepeater = outerElement.GetVisualDescendants()
            .OfType<ItemsRepeater>()
            .FirstOrDefault();

        Assert.NotNull(innerRepeater);

        innerRepeater!.GetOrCreateElement(0);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(innerRepeater.TryGetElement(100));

        window.Close();
    }
}
