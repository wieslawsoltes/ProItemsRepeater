using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Controls.UnitTests;

public class WrapLayoutTests
{
    [AvaloniaFact]
    public void Horizontal_WrapLayout_Wraps_And_Virtualizes()
    {
        var (window, repeater) = CreateRepeater(
            new WrapLayout
            {
                Orientation = Orientation.Horizontal,
                HorizontalSpacing = 10,
                VerticalSpacing = 10
            },
            ScrollBarVisibility.Disabled,
            ScrollBarVisibility.Auto,
            new Size(130, 100),
            new Size(50, 20),
            200);

        var realized = repeater.GetOrCreateElement(0);
        Assert.NotNull(realized);
        Assert.Null(repeater.TryGetElement(199));

        var element0 = (Border)repeater.GetOrCreateElement(0);
        var element1 = (Border)repeater.GetOrCreateElement(1);
        var element2 = (Border)repeater.GetOrCreateElement(2);

        Dispatcher.UIThread.RunJobs();

        Assert.Equal(element0.Bounds.Y, element1.Bounds.Y, 3);
        Assert.True(element1.Bounds.X > element0.Bounds.X);
        Assert.True(element2.Bounds.Y > element0.Bounds.Y);
        Assert.Equal(element0.Bounds.X, element2.Bounds.X, 3);

        window.Close();
    }

    [AvaloniaFact]
    public void Vertical_WrapLayout_Wraps_And_Virtualizes()
    {
        var (window, repeater) = CreateRepeater(
            new WrapLayout
            {
                Orientation = Orientation.Vertical,
                HorizontalSpacing = 10,
                VerticalSpacing = 10
            },
            ScrollBarVisibility.Auto,
            ScrollBarVisibility.Disabled,
            new Size(120, 90),
            new Size(30, 40),
            200);

        var realized = repeater.GetOrCreateElement(0);
        Assert.NotNull(realized);
        Assert.Null(repeater.TryGetElement(199));

        var element0 = (Border)repeater.GetOrCreateElement(0);
        var element1 = (Border)repeater.GetOrCreateElement(1);
        var element2 = (Border)repeater.GetOrCreateElement(2);

        Dispatcher.UIThread.RunJobs();

        Assert.Equal(element0.Bounds.X, element1.Bounds.X, 3);
        Assert.True(element1.Bounds.Y > element0.Bounds.Y);
        Assert.True(element2.Bounds.X > element0.Bounds.X);
        Assert.Equal(element0.Bounds.Y, element2.Bounds.Y, 3);

        window.Close();
    }

    [AvaloniaFact]
    public void Vertical_WrapLayout_Virtualizes_When_U_Is_Infinite()
    {
        var (window, repeater) = CreateRepeater(
            new WrapLayout { Orientation = Orientation.Vertical },
            ScrollBarVisibility.Disabled,
            ScrollBarVisibility.Auto,
            new Size(120, 100),
            new Size(30, 20),
            500);

        var realized = repeater.GetOrCreateElement(0);
        Assert.NotNull(realized);
        Assert.Null(repeater.TryGetElement(499));

        window.Close();
    }

    [AvaloniaFact]
    public void WrapLayout_Variable_Heights_Realizes_Items_After_Fast_Scroll()
    {
        var items = Enumerable.Range(0, 500)
            .Select(i => new SizedItem(40, 20 + (i % 5) * 10))
            .ToList();

        var repeater = new ItemsRepeater
        {
            Layout = new WrapLayout
            {
                Orientation = Orientation.Horizontal,
                HorizontalSpacing = 5,
                VerticalSpacing = 5
            },
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<SizedItem>((item, _) => new Border
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
            Width = 200,
            Height = 120,
            Content = scroller
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        scroller.Offset = new Vector(0, 1200);
        Dispatcher.UIThread.RunJobs();

        var viewportStart = scroller.Offset.Y;
        var viewportEnd = viewportStart + scroller.Viewport.Height;
        Assert.Contains(repeater.Children, child => child.Bounds.Bottom >= viewportStart && child.Bounds.Y <= viewportEnd);
        Assert.True(repeater.Children.Count > 0);

        window.Close();
    }

    [AvaloniaFact]
    public void WrapLayout_SampleApp_Fast_Scroll_Vertical()
    {
        var (window, scroller, repeater, itemCount) = CreateSampleRepeater(Orientation.Horizontal);
        var maxOffset = Math.Max(0, scroller.Extent.Height - scroller.Viewport.Height);
        var targetOffset = maxOffset * 0.75;

        scroller.Offset = new Vector(0, targetOffset);
        Dispatcher.UIThread.RunJobs();

        AssertViewportIntersection(repeater, scroller, verticalScroll: true);
        Assert.True(repeater.Children.Count < itemCount);

        window.Close();
    }

    [AvaloniaFact]
    public void WrapLayout_SampleApp_Slow_Scroll_Vertical()
    {
        var (window, scroller, repeater, itemCount) = CreateSampleRepeater(Orientation.Horizontal);
        var maxOffset = Math.Max(0, scroller.Extent.Height - scroller.Viewport.Height);
        const int steps = 6;

        for (var i = 0; i <= steps; i++)
        {
            var offset = maxOffset * i / steps;
            scroller.Offset = new Vector(0, offset);
            Dispatcher.UIThread.RunJobs();
            AssertViewportIntersection(repeater, scroller, verticalScroll: true);
        }

        Assert.True(repeater.Children.Count < itemCount);

        window.Close();
    }

    [AvaloniaFact]
    public void WrapLayout_SampleApp_Fast_Scroll_Horizontal()
    {
        var (window, scroller, repeater, itemCount) = CreateSampleRepeater(Orientation.Vertical);
        var maxOffset = Math.Max(0, scroller.Extent.Width - scroller.Viewport.Width);
        var targetOffset = maxOffset * 0.75;

        scroller.Offset = new Vector(targetOffset, 0);
        Dispatcher.UIThread.RunJobs();

        AssertViewportIntersection(repeater, scroller, verticalScroll: false);
        Assert.True(repeater.Children.Count < itemCount);

        window.Close();
    }

    [AvaloniaFact]
    public void WrapLayout_Random_Sizes_Stay_Visible_After_Randomize_And_Fast_Scroll_Vertical()
    {
        var random = new Random(123);
        var items = new ObservableCollection<MutableItem>(
            Enumerable.Range(0, 5000).Select(_ => new MutableItem(80, 40)));

        var repeater = new ItemsRepeater
        {
            Layout = new WrapLayout
            {
                Orientation = Orientation.Horizontal,
                HorizontalSpacing = 10,
                VerticalSpacing = 10
            },
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<MutableItem>((_, __) =>
            {
                var border = new Border();
                border.Bind(Border.WidthProperty, new Binding(nameof(MutableItem.Width)));
                border.Bind(Border.HeightProperty, new Binding(nameof(MutableItem.Height)));
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
            Width = 320,
            Height = 240,
            Content = scroller
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        foreach (var item in items)
        {
            item.Width = random.Next(10, 250);
            item.Height = random.Next(10, 250);
        }

        Dispatcher.UIThread.RunJobs();

        var maxOffset = Math.Max(0, scroller.Extent.Height - scroller.Viewport.Height);
        scroller.Offset = new Vector(0, maxOffset * 0.6);
        Dispatcher.UIThread.RunJobs();

        AssertViewportIntersection(repeater, scroller, verticalScroll: true);

        window.Close();
    }

    [AvaloniaFact]
    public void WrapLayout_Random_Sizes_Stay_Visible_After_Randomize_And_Fast_Scroll_Horizontal()
    {
        var random = new Random(321);
        var items = new ObservableCollection<MutableItem>(
            Enumerable.Range(0, 5000).Select(_ => new MutableItem(40, 80)));

        var repeater = new ItemsRepeater
        {
            Layout = new WrapLayout
            {
                Orientation = Orientation.Vertical,
                HorizontalSpacing = 10,
                VerticalSpacing = 10
            },
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<MutableItem>((_, __) =>
            {
                var border = new Border();
                border.Bind(Border.WidthProperty, new Binding(nameof(MutableItem.Width)));
                border.Bind(Border.HeightProperty, new Binding(nameof(MutableItem.Height)));
                return border;
            })
        };

        var scroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
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

        foreach (var item in items)
        {
            item.Width = random.Next(10, 250);
            item.Height = random.Next(10, 250);
        }

        Dispatcher.UIThread.RunJobs();

        var maxOffset = Math.Max(0, scroller.Extent.Width - scroller.Viewport.Width);
        scroller.Offset = new Vector(maxOffset * 0.6, 0);
        Dispatcher.UIThread.RunJobs();

        AssertViewportIntersection(repeater, scroller, verticalScroll: false);

        window.Close();
    }

    [AvaloniaFact]
    public void WrapLayout_Random_Heights_Slow_Scroll_Uses_Expected_Line()
    {
        var random = new Random(42);
        const int itemCount = 300;
        const double itemWidth = 80;
        const double horizontalSpacing = 10;
        const double verticalSpacing = 10;
        var items = Enumerable.Range(0, itemCount)
            .Select(i => new SizedItem(itemWidth, 20 + random.Next(120)))
            .ToList();

        var repeater = new ItemsRepeater
        {
            Layout = new WrapLayout
            {
                Orientation = Orientation.Horizontal,
                HorizontalSpacing = horizontalSpacing,
                VerticalSpacing = verticalSpacing
            },
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<SizedItem>((item, _) => new Border
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
        for (var i = 0; i <= steps; i++)
        {
            var offset = maxOffset * i / steps;
            scroller.Offset = new Vector(0, offset);
            Dispatcher.UIThread.RunJobs();

            var expectedLineIndex = FindLineIndex(lineStarts, lineHeights, offset);
            var expectedStartIndex = expectedLineIndex * itemsPerLine;
            var viewportStart = scroller.Offset.Y;
            var viewportEnd = viewportStart + scroller.Viewport.Height;

            var visibleIndices = repeater.Children
                .Where(child => child.Bounds.Bottom >= viewportStart && child.Bounds.Y <= viewportEnd)
                .Select(repeater.GetElementIndex)
                .ToList();

            Assert.Contains(visibleIndices, index => index >= expectedStartIndex && index < expectedStartIndex + itemsPerLine);
        }

        window.Close();
    }

    private static (Window window, ItemsRepeater repeater) CreateRepeater(
        WrapLayout layout,
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

    private static (Window window, ScrollViewer scroller, ItemsRepeater repeater, int itemCount) CreateSampleRepeater(
        Orientation orientation)
    {
        const int itemCount = 10000;
        var random = new Random(12345);
        var items = Enumerable.Range(1, itemCount)
            .Select(i => new SampleItem(i, $"Item {i}", random.Next(240) + 10, random.Next(240) + 10))
            .ToList();

        var repeater = new ItemsRepeater
        {
            Layout = new WrapLayout
            {
                Orientation = orientation,
                HorizontalSpacing = 20,
                VerticalSpacing = 20
            },
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<SampleItem>((item, _) => new TextBlock
            {
                Width = item.Width,
                Height = item.Height,
                Text = item.Text
            })
        };

        var scroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = orientation == Orientation.Vertical
                ? ScrollBarVisibility.Auto
                : ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = orientation == Orientation.Horizontal
                ? ScrollBarVisibility.Auto
                : ScrollBarVisibility.Disabled,
            Content = repeater
        };

        var window = new Window
        {
            Width = 800,
            Height = 600,
            Content = scroller
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, scroller, repeater, itemCount);
    }

    private static void AssertViewportIntersection(ItemsRepeater repeater, ScrollViewer scroller, bool verticalScroll)
    {
        var viewportStart = verticalScroll ? scroller.Offset.Y : scroller.Offset.X;
        var viewportEnd = viewportStart + (verticalScroll ? scroller.Viewport.Height : scroller.Viewport.Width);

        Assert.Contains(
            repeater.Children,
            child => verticalScroll
                ? child.Bounds.Bottom >= viewportStart && child.Bounds.Y <= viewportEnd
                : child.Bounds.Right >= viewportStart && child.Bounds.X <= viewportEnd);
    }

    private sealed class SizedItem
    {
        public SizedItem(double width, double height)
        {
            Width = width;
            Height = height;
        }

        public double Width { get; }
        public double Height { get; }
    }

    private sealed class SampleItem
    {
        public SampleItem(int index, string text, double width, double height)
        {
            Index = index;
            Text = text;
            Width = width;
            Height = height;
        }

        public int Index { get; }
        public string Text { get; }
        public double Width { get; }
        public double Height { get; }
    }

    private sealed class MutableItem : INotifyPropertyChanged
    {
        private double _width;
        private double _height;

        public MutableItem(double width, double height)
        {
            _width = width;
            _height = height;
        }

        public double Width
        {
            get => _width;
            set
            {
                if (Math.Abs(_width - value) > 0.001)
                {
                    _width = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Width)));
                }
            }
        }

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
}
