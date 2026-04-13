using System;
using Avalonia.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using NonVirtualizingStackLayout = Avalonia.Layout.NonVirtualizingStackLayout;
using StackLayout = Avalonia.Layout.StackLayout;
using UniformGridLayout = Avalonia.Layout.UniformGridLayout;
using WrapLayout = Avalonia.Layout.WrapLayout;
using Orientation = Avalonia.Layout.Orientation;

namespace ItemsRepeaterUnoSample;

public sealed partial class ItemsRepeaterPage : UserControl
{
    private readonly ItemsRepeaterPageViewModel _viewModel = new();
    private readonly Random _random = new(0);
    private int _selectedIndex = -1;

    public ItemsRepeaterPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        ApplyLayout(layoutCombo.SelectedIndex);
    }

    private void ItemPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ItemsRepeaterPageViewModelItem item)
        {
            _viewModel.SelectedItem = item;
            _selectedIndex = _viewModel.Items.IndexOf(item);
        }
    }

    private void LayoutChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        ApplyLayout(layoutCombo.SelectedIndex);
    }

    private void ApplyLayout(int selectedIndex)
    {
        switch (selectedIndex)
        {
            case 0:
                repeater.Layout = new StackLayout { Orientation = Orientation.Vertical };
                scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                break;
            case 1:
                repeater.Layout = new StackLayout { Orientation = Orientation.Horizontal };
                scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                break;
            case 2:
                repeater.Layout = new NonVirtualizingStackLayout { Orientation = Orientation.Vertical };
                scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                break;
            case 3:
                repeater.Layout = new NonVirtualizingStackLayout { Orientation = Orientation.Horizontal };
                scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                break;
            case 4:
                repeater.Layout = new UniformGridLayout
                {
                    Orientation = Orientation.Vertical,
                    MinItemWidth = 200,
                    MinItemHeight = 200,
                };
                scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                break;
            case 5:
                repeater.Layout = new UniformGridLayout
                {
                    Orientation = Orientation.Horizontal,
                    MinItemWidth = 200,
                    MinItemHeight = 200,
                };
                scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                break;
            case 6:
                repeater.Layout = new WrapLayout
                {
                    Orientation = Orientation.Vertical,
                    HorizontalSpacing = 20,
                    VerticalSpacing = 20,
                };
                scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                break;
            case 7:
                repeater.Layout = new WrapLayout
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalSpacing = 20,
                    VerticalSpacing = 20,
                };
                scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                break;
        }
    }

    private void AddItemClick(object sender, RoutedEventArgs e) => _viewModel.AddItem();

    private void RemoveItemClick(object sender, RoutedEventArgs e) => _viewModel.RemoveItem();

    private void RandomizeHeightsClick(object sender, RoutedEventArgs e) => _viewModel.RandomizeHeights();

    private void RandomizeWidthsClick(object sender, RoutedEventArgs e) => _viewModel.RandomizeWidths();

    private void ResetItemsClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ResetItems();
        _selectedIndex = -1;
    }

    private void ScrollToLastClick(object sender, RoutedEventArgs e) => ScrollTo(_viewModel.Items.Count - 1);

    private void ScrollToRandomClick(object sender, RoutedEventArgs e) => ScrollTo(GetRandomIndex());

    private void ScrollToSelectedClick(object sender, RoutedEventArgs e) => ScrollTo(_selectedIndex);

    private void ScrollTo(int index)
    {
        if (index < 0)
            return;

        if (repeater.GetOrCreateElement(index) is FrameworkElement element)
            element.StartBringIntoView();
    }

    private int GetRandomIndex()
    {
        var count = _viewModel.Items.Count;
        return count == 0 ? -1 : _random.Next(count);
    }
}
