using System;
using Avalonia.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NonVirtualizingStackLayout = Avalonia.Layout.NonVirtualizingStackLayout;
using StackLayout = Avalonia.Layout.StackLayout;
using UniformGridLayout = Avalonia.Layout.UniformGridLayout;
using WrapLayout = Avalonia.Layout.WrapLayout;
using Orientation = Avalonia.Layout.Orientation;

namespace ItemsRepeaterUnoSample;

public sealed partial class SelectingItemsRepeaterPage : UserControl
{
    private readonly SelectingItemsRepeaterPageViewModel _viewModel = new();
    private readonly Random _random = new(0);

    public SelectingItemsRepeaterPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        selectingRepeater.SelectionChanged += SelectingRepeaterSelectionChanged;
        selectingRepeater.ElementPrepared += SelectingRepeaterElementPrepared;
        selectingRepeater.ElementIndexChanged += SelectingRepeaterElementIndexChanged;
        ApplyLayout(layoutCombo.SelectedIndex);
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
                selectingRepeater.Layout = new StackLayout { Orientation = Orientation.Vertical };
                scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                break;
            case 1:
                selectingRepeater.Layout = new StackLayout { Orientation = Orientation.Horizontal };
                scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                break;
            case 2:
                selectingRepeater.Layout = new NonVirtualizingStackLayout { Orientation = Orientation.Vertical };
                scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                break;
            case 3:
                selectingRepeater.Layout = new NonVirtualizingStackLayout { Orientation = Orientation.Horizontal };
                scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                break;
            case 4:
                selectingRepeater.Layout = new UniformGridLayout
                {
                    Orientation = Orientation.Vertical,
                    MinItemWidth = 200,
                    MinItemHeight = 200,
                };
                scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                break;
            case 5:
                selectingRepeater.Layout = new UniformGridLayout
                {
                    Orientation = Orientation.Horizontal,
                    MinItemWidth = 200,
                    MinItemHeight = 200,
                };
                scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                break;
            case 6:
                selectingRepeater.Layout = new WrapLayout
                {
                    Orientation = Orientation.Vertical,
                    HorizontalSpacing = 20,
                    VerticalSpacing = 20,
                };
                scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                break;
            case 7:
                selectingRepeater.Layout = new WrapLayout
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

    private void SelectingRepeaterSelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
    {
        _viewModel.SelectedItem = selectingRepeater.SelectedItem as SelectingItemsRepeaterPageViewModelItem;
        UpdateSelectionVisuals();
    }

    private void SelectingRepeaterElementPrepared(object? sender, Avalonia.Controls.ItemsRepeaterElementPreparedEventArgs args)
    {
        UpdateSelectionVisual(args.Element as Border, args.Index);
    }

    private void SelectingRepeaterElementIndexChanged(object? sender, Avalonia.Controls.ItemsRepeaterElementIndexChangedEventArgs args)
    {
        UpdateSelectionVisual(args.Element as Border, args.NewIndex);
    }

    private void UpdateSelectionVisuals()
    {
        foreach (var element in selectingRepeater.GetRealizedElements())
            UpdateSelectionVisual(element as Border, selectingRepeater.GetElementIndex(element));
    }

    private static void UpdateSelectionVisual(Border? border, int index)
    {
        if (border?.DataContext is not SelectingItemsRepeaterPageViewModelItem item || index < 0)
            return;

        var selected = SelectingItemsRepeater.GetIsSelected(border);
        border.Background = selected
            ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x25, 0x63, 0xEB))
            : new Microsoft.UI.Xaml.Media.SolidColorBrush(item.Index % 2 == 0
                ? Windows.UI.Color.FromArgb(0xFF, 0xF5, 0xDE, 0xB3)
                : Windows.UI.Color.FromArgb(0xFF, 0xFD, 0xE6, 0x8A));

        if (border.Child is TextBlock textBlock)
            textBlock.Foreground = selected
                ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF))
                : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x00, 0x00, 0x00));
    }

    private void AddItemClick(object sender, RoutedEventArgs e) => _viewModel.AddItem();

    private void RemoveItemClick(object sender, RoutedEventArgs e) => _viewModel.RemoveItem();

    private void RandomizeHeightsClick(object sender, RoutedEventArgs e) => _viewModel.RandomizeHeights();

    private void RandomizeWidthsClick(object sender, RoutedEventArgs e) => _viewModel.RandomizeWidths();

    private void ResetItemsClick(object sender, RoutedEventArgs e) => _viewModel.ResetItems();

    private void ScrollToLastClick(object sender, RoutedEventArgs e) => ScrollTo(_viewModel.Items.Count - 1);

    private void ScrollToRandomClick(object sender, RoutedEventArgs e) => ScrollTo(GetRandomIndex());

    private void ScrollToSelectedClick(object sender, RoutedEventArgs e) => ScrollTo(selectingRepeater.SelectedIndex);

    private void ScrollTo(int index)
    {
        if (index < 0)
            return;

        if (selectingRepeater.GetOrCreateElement(index) is FrameworkElement element)
            element.StartBringIntoView();
    }

    private int GetRandomIndex()
    {
        var count = _viewModel.Items.Count;
        return count == 0 ? -1 : _random.Next(count);
    }
}
