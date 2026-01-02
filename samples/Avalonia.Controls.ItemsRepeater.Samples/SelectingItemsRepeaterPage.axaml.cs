using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Controls.Primitives;

namespace Avalonia.Controls.Samples;

public partial class SelectingItemsRepeaterPage : UserControl
{
    private readonly SelectingItemsRepeaterPageViewModel _viewModel;
    private readonly Random _random = new Random(0);

    public SelectingItemsRepeaterPage()
    {
        InitializeComponent();
        selectingRepeater.KeyDown += SelectingRepeaterOnKeyDown;
        selectingRepeater.SelectionChanged += SelectingRepeaterSelectionChanged;
        scrollToLast.Click += ScrollToLast_Click;
        scrollToRandom.Click += ScrollToRandom_Click;
        scrollToSelected.Click += ScrollToSelected_Click;
        DataContext = _viewModel = new SelectingItemsRepeaterPageViewModel();
        ApplyLayout(layout.SelectedIndex);
    }

    public void OnSelectTemplateKey(object sender, SelectTemplateEventArgs e)
    {
        if (e.DataContext is SelectingItemsRepeaterPageViewModelItem item)
        {
            e.TemplateKey = (item.Index % 2 == 0) ? "even" : "odd";
        }
    }

    private void LayoutChanged(object sender, SelectionChangedEventArgs e)
    {
        if (selectingRepeater is null || scroller is null)
        {
            return;
        }

        var comboBox = (ComboBox)sender;
        ApplyLayout(comboBox.SelectedIndex);
    }

    private void ApplyLayout(int selectedIndex)
    {
        switch (selectedIndex)
        {
            case 0:
                scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                selectingRepeater.Layout = new StackLayout { Orientation = Orientation.Vertical };
                break;
            case 1:
                scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                selectingRepeater.Layout = new StackLayout { Orientation = Orientation.Horizontal };
                break;
            case 2:
                scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                selectingRepeater.Layout = new NonVirtualizingStackLayout { Orientation = Orientation.Vertical };
                break;
            case 3:
                scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                selectingRepeater.Layout = new NonVirtualizingStackLayout { Orientation = Orientation.Horizontal };
                break;
            case 4:
                scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                selectingRepeater.Layout = new UniformGridLayout
                {
                    Orientation = Orientation.Vertical,
                    MinItemWidth = 200,
                    MinItemHeight = 200,
                };
                break;
            case 5:
                scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                selectingRepeater.Layout = new UniformGridLayout
                {
                    Orientation = Orientation.Horizontal,
                    MinItemWidth = 200,
                    MinItemHeight = 200,
                };
                break;
            case 6:
                scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                selectingRepeater.Layout = new WrapLayout
                {
                    Orientation = Orientation.Vertical,
                    HorizontalSpacing = 20,
                    VerticalSpacing = 20
                };
                break;
            case 7:
                scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                selectingRepeater.Layout = new WrapLayout
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalSpacing = 20,
                    VerticalSpacing = 20
                };
                break;
        }
    }

    private void ScrollTo(int index)
    {
        if (index < 0)
        {
            return;
        }

        var element = selectingRepeater.GetOrCreateElement(index);
        UpdateLayout();
        element.BringIntoView();
    }

    private void SelectingRepeaterSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _viewModel.SelectedItem = selectingRepeater.SelectedItem as SelectingItemsRepeaterPageViewModelItem;
    }

    private void SelectingRepeaterOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5)
        {
            _viewModel.ResetItems();
        }
    }

    private void ScrollToLast_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ScrollTo(_viewModel.Items.Count - 1);
    }

    private void ScrollToRandom_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ScrollTo(GetRandomIndex());
    }

    private void ScrollToSelected_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ScrollTo(selectingRepeater.SelectedIndex);
    }

    private int GetRandomIndex()
    {
        var count = _viewModel.Items.Count;
        return count == 0 ? -1 : _random.Next(count);
    }

}
