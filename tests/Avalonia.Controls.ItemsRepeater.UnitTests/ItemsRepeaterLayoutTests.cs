using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Controls.UnitTests;

public class ItemsRepeaterLayoutTests
{
    private static (Window window, ItemsRepeater repeater) CreateRepeater(
        AttachedLayout layout,
        ScrollBarVisibility horizontalScrollBarVisibility,
        ScrollBarVisibility verticalScrollBarVisibility,
        Size windowSize,
        Size itemSize,
        int itemCount)
    {
        var items = Enumerable.Range(0, itemCount).ToList();
        var repeater = new ItemsRepeater
        {
            Layout = layout,
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<int>((_, __) => new Border
            {
                Width = itemSize.Width,
                Height = itemSize.Height
            })
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

        return (window, repeater);
    }

    [AvaloniaFact]
    public void StackLayout_Vertical_Virtualizes()
    {
        var (window, repeater) = CreateRepeater(
            new StackLayout { Orientation = Orientation.Vertical },
            ScrollBarVisibility.Disabled,
            ScrollBarVisibility.Auto,
            new Size(200, 120),
            new Size(80, 20),
            200);

        var element0 = (Border)repeater.GetOrCreateElement(0);
        var element1 = (Border)repeater.GetOrCreateElement(1);

        Dispatcher.UIThread.RunJobs();

        Assert.Null(repeater.TryGetElement(199));
        Assert.Equal(element0.Bounds.X, element1.Bounds.X, 3);
        Assert.True(element1.Bounds.Y > element0.Bounds.Y);
        Assert.Equal(element0.Bounds.Height, element1.Bounds.Y - element0.Bounds.Y, 3);

        window.Close();
    }

    [AvaloniaFact]
    public void StackLayout_Horizontal_Virtualizes()
    {
        var (window, repeater) = CreateRepeater(
            new StackLayout { Orientation = Orientation.Horizontal },
            ScrollBarVisibility.Auto,
            ScrollBarVisibility.Disabled,
            new Size(120, 200),
            new Size(20, 60),
            200);

        var element0 = (Border)repeater.GetOrCreateElement(0);
        var element1 = (Border)repeater.GetOrCreateElement(1);

        Dispatcher.UIThread.RunJobs();

        Assert.Null(repeater.TryGetElement(199));
        Assert.Equal(element0.Bounds.Y, element1.Bounds.Y, 3);
        Assert.True(element1.Bounds.X > element0.Bounds.X);
        Assert.Equal(element0.Bounds.Width, element1.Bounds.X - element0.Bounds.X, 3);

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(Orientation.Vertical)]
    [InlineData(Orientation.Horizontal)]
    public void NonVirtualizingStackLayout_Realizes_All_Items(Orientation orientation)
    {
        var (window, repeater) = CreateRepeater(
            new NonVirtualizingStackLayout { Orientation = orientation },
            ScrollBarVisibility.Auto,
            ScrollBarVisibility.Auto,
            new Size(200, 120),
            new Size(20, 20),
            30);

        Assert.Equal(30, repeater.Children.Count);

        window.Close();
    }

    [AvaloniaFact]
    public void UniformGridLayout_Horizontal_Wraps_And_Virtualizes()
    {
        var layout = new UniformGridLayout
        {
            Orientation = Orientation.Horizontal,
            MinItemWidth = 50,
            MinItemHeight = 20,
            MinRowSpacing = 10,
            MinColumnSpacing = 10,
            ItemsJustification = UniformGridLayoutItemsJustification.Start,
            MaximumRowsOrColumns = 2
        };

        var (window, repeater) = CreateRepeater(
            layout,
            ScrollBarVisibility.Disabled,
            ScrollBarVisibility.Auto,
            new Size(130, 100),
            new Size(50, 20),
            200);

        var element0 = (Border)repeater.GetOrCreateElement(0);
        var element1 = (Border)repeater.GetOrCreateElement(1);
        var element2 = (Border)repeater.GetOrCreateElement(2);

        Dispatcher.UIThread.RunJobs();

        Assert.Null(repeater.TryGetElement(199));
        Assert.Equal(element0.Bounds.Y, element1.Bounds.Y, 3);
        Assert.True(element1.Bounds.X > element0.Bounds.X);
        Assert.True(element2.Bounds.Y > element0.Bounds.Y);

        window.Close();
    }

    [AvaloniaFact]
    public void UniformGridLayout_Vertical_Wraps_And_Virtualizes()
    {
        var layout = new UniformGridLayout
        {
            Orientation = Orientation.Vertical,
            MinItemWidth = 30,
            MinItemHeight = 40,
            MinRowSpacing = 10,
            MinColumnSpacing = 10,
            ItemsJustification = UniformGridLayoutItemsJustification.Start,
            MaximumRowsOrColumns = 2
        };

        var (window, repeater) = CreateRepeater(
            layout,
            ScrollBarVisibility.Auto,
            ScrollBarVisibility.Disabled,
            new Size(120, 90),
            new Size(30, 40),
            200);

        var element0 = (Border)repeater.GetOrCreateElement(0);
        var element1 = (Border)repeater.GetOrCreateElement(1);
        var element2 = (Border)repeater.GetOrCreateElement(2);

        Dispatcher.UIThread.RunJobs();

        Assert.Null(repeater.TryGetElement(199));
        Assert.Equal(element0.Bounds.X, element1.Bounds.X, 3);
        Assert.True(element1.Bounds.Y > element0.Bounds.Y);
        Assert.True(element2.Bounds.X > element0.Bounds.X);

        window.Close();
    }

    [AvaloniaFact]
    public void UniformGridLayout_Realizes_Items_After_Fast_Scroll()
    {
        var items = Enumerable.Range(0, 5000).ToList();
        var repeater = new ItemsRepeater
        {
            Layout = new UniformGridLayout
            {
                Orientation = Orientation.Horizontal,
                MinItemWidth = 40,
                MinItemHeight = 30,
                MinRowSpacing = 6,
                MinColumnSpacing = 6,
                MaximumRowsOrColumns = 3,
                ItemsJustification = UniformGridLayoutItemsJustification.Start
            },
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<int>((_, __) => new Border
            {
                Width = 40,
                Height = 30
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
            Width = 220,
            Height = 140,
            Content = scroller
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        scroller.Offset = new Vector(0, 90000);
        Dispatcher.UIThread.RunJobs();

        var viewportStart = scroller.Offset.Y;
        var viewportEnd = viewportStart + scroller.Viewport.Height;
        var intersectsViewport = repeater.Children
            .Any(child => child.Bounds.Bottom >= viewportStart && child.Bounds.Y <= viewportEnd);

        Assert.True(intersectsViewport);

        window.Close();
    }

    [AvaloniaFact]
    public void UniformGridLayout_Move_Does_Not_Throw_And_Updates_First_Item()
    {
        var items = new ObservableCollection<int>(Enumerable.Range(0, 10));
        var repeater = new ItemsRepeater
        {
            Layout = new UniformGridLayout
            {
                Orientation = Orientation.Horizontal,
                MinItemWidth = 20,
                MinItemHeight = 20,
                MaximumRowsOrColumns = 2
            },
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<int>((_, __) => new TextBlock())
        };

        var scroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = repeater
        };

        var window = new Window
        {
            Width = 200,
            Height = 200,
            Content = scroller
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var element0 = (TextBlock)repeater.GetOrCreateElement(0);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0, element0.DataContext);

        items.Move(0, items.Count - 1);
        Dispatcher.UIThread.RunJobs();

        var element0AfterMove = (TextBlock)repeater.GetOrCreateElement(0);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, element0AfterMove.DataContext);

        window.Close();
    }

    [AvaloniaFact]
    public void StackLayout_Vertical_Uses_Variable_Heights()
    {
        var items = new ObservableCollection<SizeItem>
        {
            new SizeItem(10),
            new SizeItem(30),
            new SizeItem(20)
        };

        var repeater = new ItemsRepeater
        {
            Layout = new StackLayout { Orientation = Orientation.Vertical },
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<SizeItem>((_, __) =>
            {
                var border = new Border { Width = 80 };
                border.Bind(Border.HeightProperty, new Binding(nameof(SizeItem.Height)));
                return border;
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
            Width = 200,
            Height = 200,
            Content = scroller
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var element0 = (Border)repeater.GetOrCreateElement(0);
        var element1 = (Border)repeater.GetOrCreateElement(1);
        var element2 = (Border)repeater.GetOrCreateElement(2);

        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, element0.Bounds.Y, 3);
        Assert.Equal(items[0].Height, element1.Bounds.Y, 3);
        Assert.Equal(items[0].Height + items[1].Height, element2.Bounds.Y, 3);
        Assert.Equal(items[0].Height, element0.Bounds.Height, 3);
        Assert.Equal(items[1].Height, element1.Bounds.Height, 3);
        Assert.Equal(items[2].Height, element2.Bounds.Height, 3);

        window.Close();
    }

    [AvaloniaFact]
    public void StackLayout_Vertical_Updates_After_Height_Changes()
    {
        var items = new ObservableCollection<SizeItem>
        {
            new SizeItem(10),
            new SizeItem(25),
            new SizeItem(15)
        };

        var repeater = new ItemsRepeater
        {
            Layout = new StackLayout { Orientation = Orientation.Vertical },
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<SizeItem>((_, __) =>
            {
                var border = new Border { Width = 80 };
                border.Bind(Border.HeightProperty, new Binding(nameof(SizeItem.Height)));
                return border;
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
            Width = 200,
            Height = 200,
            Content = scroller
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        items[0].Height = 40;
        items[1].Height = 5;
        Dispatcher.UIThread.RunJobs();

        var element0 = (Border)repeater.GetOrCreateElement(0);
        var element1 = (Border)repeater.GetOrCreateElement(1);
        var element2 = (Border)repeater.GetOrCreateElement(2);

        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, element0.Bounds.Y, 3);
        Assert.Equal(items[0].Height, element1.Bounds.Y, 3);
        Assert.Equal(items[0].Height + items[1].Height, element2.Bounds.Y, 3);

        window.Close();
    }

    [AvaloniaFact]
    public void StackLayout_Vertical_Anchors_When_Scrolled_Past_Estimated_Extent()
    {
        var items = new ObservableCollection<SizeItem>(
            Enumerable.Range(0, 2000).Select(i => new SizeItem(i < 5 ? 10 : 200)));

        var repeater = new ItemsRepeater
        {
            Layout = new StackLayout { Orientation = Orientation.Vertical },
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<SizeItem>((_, __) =>
            {
                var border = new Border { Width = 80 };
                border.Bind(Border.HeightProperty, new Binding(nameof(SizeItem.Height)));
                return border;
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
            Width = 200,
            Height = 200,
            Content = scroller
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        scroller.Offset = new Vector(0, 100000);
        Dispatcher.UIThread.RunJobs();

        Assert.True(repeater.Children.Count > 0);

        window.Close();
    }

    [AvaloniaFact]
    public void StackLayout_Vertical_Realizes_Items_After_Fast_Scroll_With_Variable_Heights()
    {
        var items = new ObservableCollection<SizeItem>(
            Enumerable.Range(0, 5000).Select(i => new SizeItem(i % 2 == 0 ? 20 : 200)));

        var repeater = new ItemsRepeater
        {
            Layout = new StackLayout { Orientation = Orientation.Vertical },
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<SizeItem>((_, __) =>
            {
                var border = new Border { Width = 80 };
                border.Bind(Border.HeightProperty, new Binding(nameof(SizeItem.Height)));
                return border;
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
            Width = 200,
            Height = 200,
            Content = scroller
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        scroller.Offset = new Vector(0, 90000);
        Dispatcher.UIThread.RunJobs();

        var viewportStart = scroller.Offset.Y;
        var viewportEnd = viewportStart + scroller.Viewport.Height;
        var intersectsViewport = repeater.Children
            .Any(child => child.Bounds.Bottom >= viewportStart && child.Bounds.Y <= viewportEnd);

        Assert.True(intersectsViewport);

        window.Close();
    }

    private sealed class SizeItem : INotifyPropertyChanged
    {
        private double _height;

        public SizeItem(double height) => _height = height;

        public double Height
        {
            get => _height;
            set
            {
                if (Math.Abs(_height - value) > 0.001)
                {
                    _height = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Height)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
