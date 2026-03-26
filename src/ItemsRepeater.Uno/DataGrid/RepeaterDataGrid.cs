using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using Avalonia.Collections;
using Avalonia.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI;

namespace Avalonia.Controls.DataGrid;

[ContentProperty(Name = nameof(Columns))]
[TemplatePart(Name = HeaderHostPartName, Type = typeof(Border))]
[TemplatePart(Name = HeaderPanelPartName, Type = typeof(StackPanel))]
[TemplatePart(Name = RowsRepeaterPartName, Type = typeof(Avalonia.Controls.SelectingItemsRepeater))]
[TemplatePart(Name = BodyScrollViewerPartName, Type = typeof(ScrollViewer))]
public class RepeaterDataGrid : Control
{
    private const string HeaderHostPartName = "PART_HeaderHost";
    private const string HeaderPanelPartName = "PART_HeaderPanel";
    private const string RowsRepeaterPartName = "PART_RowsRepeater";
    private const string BodyScrollViewerPartName = "PART_BodyScrollViewer";

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(RepeaterDataGrid),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty SelectionModeProperty =
        DependencyProperty.Register(
            nameof(SelectionMode),
            typeof(SelectionMode),
            typeof(RepeaterDataGrid),
            new PropertyMetadata(SelectionMode.Single, OnSelectionModeChanged));

    public static readonly DependencyProperty RowHeightBindingPathProperty =
        DependencyProperty.Register(
            nameof(RowHeightBindingPath),
            typeof(string),
            typeof(RepeaterDataGrid),
            new PropertyMetadata(null, OnRowHeightBindingPathChanged));

    public static readonly DependencyProperty SelectedCellProperty =
        DependencyProperty.Register(
            nameof(SelectedCell),
            typeof(RepeaterDataGridCellInfo?),
            typeof(RepeaterDataGrid),
            new PropertyMetadata(null, OnSelectedCellChanged));

    public static readonly DependencyProperty AutoMeasureRowLimitProperty =
        DependencyProperty.Register(
            nameof(AutoMeasureRowLimit),
            typeof(int),
            typeof(RepeaterDataGrid),
            new PropertyMetadata(int.MaxValue));

    private readonly AvaloniaList<RepeaterDataGridColumn> _columns = new();
    private readonly Dictionary<int, RepeaterDataGridRowControl> _realizedRows = new();
    private readonly RowElementFactory _rowFactory = new();
    private readonly RectangleGeometry _headerClip = new();
    private readonly TranslateTransform _headerTransform = new();
    private Border? _headerHost;
    private StackPanel? _headerPanel;
    private SelectingItemsRepeater? _rowsRepeater;
    private ScrollViewer? _scroller;

    public RepeaterDataGrid()
    {
        DefaultStyleKey = typeof(RepeaterDataGrid);
        _columns.CollectionChanged += OnColumnsCollectionChanged;
        SizeChanged += OnSizeChanged;
    }

    public AvaloniaList<RepeaterDataGridColumn> Columns => _columns;

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public SelectionMode SelectionMode
    {
        get => (SelectionMode)GetValue(SelectionModeProperty);
        set => SetValue(SelectionModeProperty, value);
    }

    public string? RowHeightBindingPath
    {
        get => (string?)GetValue(RowHeightBindingPathProperty);
        set => SetValue(RowHeightBindingPathProperty, value);
    }

    public RepeaterDataGridCellInfo? SelectedCell
    {
        get => (RepeaterDataGridCellInfo?)GetValue(SelectedCellProperty);
        set => SetValue(SelectedCellProperty, value);
    }

    public int AutoMeasureRowLimit
    {
        get => (int)GetValue(AutoMeasureRowLimitProperty);
        set => SetValue(AutoMeasureRowLimitProperty, value);
    }

    public double TotalColumnWidth { get; private set; }

    public double HeaderHeight { get; private set; }

    protected override void OnApplyTemplate()
    {
        DetachTemplateParts();
        base.OnApplyTemplate();

        _headerHost = GetTemplateChild(HeaderHostPartName) as Border;
        _headerPanel = GetTemplateChild(HeaderPanelPartName) as StackPanel;
        _scroller = GetTemplateChild(BodyScrollViewerPartName) as ScrollViewer;
        _rowsRepeater = GetTemplateChild(RowsRepeaterPartName) as SelectingItemsRepeater;

        if (_scroller is not null)
        {
            _scroller.ViewChanged += OnScrollViewChanged;
        }

        if (_headerHost is not null)
        {
            _headerHost.Clip = _headerClip;
            _headerHost.SizeChanged += OnHeaderHostSizeChanged;
            UpdateHeaderClip();
        }

        if (_headerPanel is not null)
            _headerPanel.RenderTransform = _headerTransform;

        if (_rowsRepeater is not null)
        {
            _rowsRepeater.ItemTemplate = _rowFactory;
            _rowsRepeater.ItemsSource = ItemsSource;
            _rowsRepeater.SelectionMode = SelectionMode;
            _rowsRepeater.ElementPrepared += OnRowPrepared;
            _rowsRepeater.ElementClearing += OnRowClearing;
            _rowsRepeater.ElementIndexChanged += OnRowIndexChanged;
            _rowsRepeater.SelectionChanged += OnRowsSelectionChanged;
            _rowsRepeater.AddHandler(PointerPressedEvent, new PointerEventHandler(OnRowsPointerPressed), true);
        }

        BuildHeader();
        UpdateColumnWidths();
        SyncHeaderScroll();
        UpdateRealizedRows();
    }

    private void DetachTemplateParts()
    {
        if (_headerHost is not null)
            _headerHost.SizeChanged -= OnHeaderHostSizeChanged;

        if (_rowsRepeater is not null)
        {
            _rowsRepeater.ElementPrepared -= OnRowPrepared;
            _rowsRepeater.ElementClearing -= OnRowClearing;
            _rowsRepeater.ElementIndexChanged -= OnRowIndexChanged;
            _rowsRepeater.SelectionChanged -= OnRowsSelectionChanged;
            _rowsRepeater.RemoveHandler(PointerPressedEvent, new PointerEventHandler(OnRowsPointerPressed));
        }

        if (_scroller is not null)
        {
            _scroller.ViewChanged -= OnScrollViewChanged;
        }

        _realizedRows.Clear();
        _headerHost = null;
        _headerPanel = null;
        _headerTransform.X = 0;
        _rowsRepeater = null;
        _scroller = null;
    }

    private void OnColumnsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        for (var i = 0; i < _columns.Count; ++i)
            _columns[i].Index = i;

        BuildHeader();
        UpdateColumnWidths();
        UpdateRealizedRows();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateHeaderClip();
        UpdateColumnWidths();
    }

    private void OnHeaderHostSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        UpdateHeaderClip();
    }

    private void OnRowPrepared(object? sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is not RepeaterDataGridRowControl row)
            return;

        _realizedRows[args.Index] = row;
        row.Bind(row.DataContext, args.Index, _columns, RowHeightBindingPath);
        row.ApplySelectionState(IsRowSelected(args.Index), SelectedCell);
    }

    private void OnRowClearing(object? sender, ItemsRepeaterElementClearingEventArgs args)
    {
        foreach (var pair in _realizedRows)
        {
            if (ReferenceEquals(pair.Value, args.Element))
            {
                _realizedRows.Remove(pair.Key);
                return;
            }
        }
    }

    private void OnRowIndexChanged(object? sender, ItemsRepeaterElementIndexChangedEventArgs args)
    {
        if (args.Element is not RepeaterDataGridRowControl row)
            return;

        _realizedRows.Remove(args.OldIndex);
        _realizedRows[args.NewIndex] = row;
        row.Bind(row.DataContext, args.NewIndex, _columns, RowHeightBindingPath);
        row.ApplySelectionState(IsRowSelected(args.NewIndex), SelectedCell);
    }

    private void OnRowsSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateRealizedRowsSelection();
    }

    private void OnRowsPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_rowsRepeater is null)
            return;

        var row = FindAncestor<RepeaterDataGridRowControl>(e.OriginalSource as DependencyObject);
        if (row is null)
            return;

        if (_rowsRepeater.UpdateSelectionFromEvent(row, e))
        {
            var cell = FindCellBorder(e.OriginalSource as DependencyObject);
            if (cell?.Tag is int columnIndex)
                SelectedCell = new RepeaterDataGridCellInfo(row.RowIndex, columnIndex);
        }
    }

    private void OnScrollViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        SyncHeaderScroll();
    }

    private void SyncHeaderScroll()
    {
        if (_headerPanel is null || _scroller is null)
            return;

        _headerTransform.X = -_scroller.HorizontalOffset;
    }

    private void UpdateHeaderClip()
    {
        if (_headerHost is null)
            return;

        _headerClip.Rect = new Windows.Foundation.Rect(0, 0, Math.Max(0, _headerHost.ActualWidth), Math.Max(0, _headerHost.ActualHeight));
    }

    private void BuildHeader()
    {
        if (_headerPanel is null)
            return;

        _headerPanel.Children.Clear();

        foreach (var column in _columns)
        {
            var cell = CreateHeaderCell(column);
            _headerPanel.Children.Add(cell);
        }

        HeaderHeight = 0;
        foreach (var child in _headerPanel.Children)
            HeaderHeight = Math.Max(HeaderHeight, child.DesiredSize.Height);
    }

    private void UpdateColumnWidths()
    {
        if (_columns.Count == 0)
        {
            TotalColumnWidth = 0;
            return;
        }

        var widths = new double[_columns.Count];
        var fixedWidth = 0d;
        var starColumns = new List<(int Index, double Weight, double MinWidth)>();

        for (var i = 0; i < _columns.Count; ++i)
        {
            var column = _columns[i];
            column.Index = i;
            var minWidth = Math.Max(0, column.MinWidth);

            if (column.Width.IsAbsolute)
            {
                widths[i] = Math.Max(minWidth, column.Width.Value);
                fixedWidth += widths[i];
            }
            else if (column.Width.IsAuto)
            {
                widths[i] = Math.Max(minWidth, MeasureHeaderWidth(column));
                fixedWidth += widths[i];
            }
            else
            {
                starColumns.Add((i, Math.Max(1, column.Width.Value), minWidth));
            }
        }

        var availableWidth = ActualWidth > 0 ? ActualWidth : double.NaN;
        var remaining = !double.IsNaN(availableWidth) && !double.IsInfinity(availableWidth)
            ? Math.Max(0, availableWidth - fixedWidth)
            : 0;
        var totalStar = 0d;
        foreach (var starColumn in starColumns)
            totalStar += starColumn.Weight;

        foreach (var starColumn in starColumns)
        {
            var starWidth = totalStar > 0 ? remaining * (starColumn.Weight / totalStar) : 0;
            widths[starColumn.Index] = Math.Max(starColumn.MinWidth, starWidth);
        }

        TotalColumnWidth = 0;
        for (var i = 0; i < _columns.Count; ++i)
        {
            _columns[i].ActualWidth = widths[i];
            TotalColumnWidth += widths[i];
        }

        ApplyHeaderWidths();
        UpdateRealizedRows();

        if (_rowsRepeater is not null)
            _rowsRepeater.Width = TotalColumnWidth;
    }

    private void ApplyHeaderWidths()
    {
        if (_headerPanel is null)
            return;

        for (var i = 0; i < _headerPanel.Children.Count && i < _columns.Count; ++i)
        {
            if (_headerPanel.Children[i] is FrameworkElement element)
                element.Width = _columns[i].ActualWidth;
        }
    }

    private void UpdateRealizedRows()
    {
        foreach (var pair in _realizedRows)
        {
            pair.Value.Bind(pair.Value.DataContext, pair.Key, _columns, RowHeightBindingPath);
            pair.Value.ApplyColumnWidths(_columns);
            pair.Value.ApplySelectionState(IsRowSelected(pair.Key), SelectedCell);
        }
    }

    private void UpdateRealizedRowsSelection()
    {
        foreach (var pair in _realizedRows)
            pair.Value.ApplySelectionState(IsRowSelected(pair.Key), SelectedCell);
    }

    private bool IsRowSelected(int rowIndex)
    {
        return _rowsRepeater?.Selection.IsSelected(rowIndex) == true;
    }

    private Border CreateHeaderCell(RepeaterDataGridColumn column)
    {
        var border = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xD9, 0xD9, 0xD9)),
            BorderThickness = new Thickness(1, 1, 1, 1),
            Padding = new Thickness(8, 6, 8, 6),
            Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF4, 0xF4, 0xF4)),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        if (column.HeaderTemplate is not null)
        {
            border.Child = new ContentControl
            {
                Content = column.Header,
                ContentTemplate = column.HeaderTemplate,
            };
        }
        else
        {
            border.Child = new TextBlock
            {
                Text = column.Header?.ToString() ?? string.Empty,
            };
        }

        border.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        return border;
    }

    private double MeasureHeaderWidth(RepeaterDataGridColumn column)
    {
        var headerCell = CreateHeaderCell(column);
        return Math.Max(headerCell.DesiredSize.Width, column.MinWidth);
    }

    private static Border? FindCellBorder(DependencyObject? source)
    {
        for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is Border border && border.Tag is int)
                return border;
        }

        return null;
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : class
    {
        for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is T value)
                return value;
        }

        return null;
    }

    private static void OnItemsSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not RepeaterDataGrid grid || grid._rowsRepeater is null)
            return;

        grid._rowsRepeater.ItemsSource = (IEnumerable?)args.NewValue;
        grid._realizedRows.Clear();
    }

    private static void OnSelectionModeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is RepeaterDataGrid grid && grid._rowsRepeater is not null)
            grid._rowsRepeater.SelectionMode = (SelectionMode)args.NewValue;
    }

    private static void OnRowHeightBindingPathChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is RepeaterDataGrid grid)
            grid.UpdateRealizedRows();
    }

    private static void OnSelectedCellChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is RepeaterDataGrid grid)
            grid.UpdateRealizedRowsSelection();
    }

    private sealed class RowElementFactory : ElementFactory
    {
        private readonly Queue<RepeaterDataGridRowControl> _pool = new();

        protected override UIElement GetElementCore(ElementFactoryGetArgs args)
        {
            var row = _pool.Count > 0 ? _pool.Dequeue() : new RepeaterDataGridRowControl();
            row.DataContext = args.Data;
            return row;
        }

        protected override void RecycleElementCore(ElementFactoryRecycleArgs args)
        {
            if (args.Element is RepeaterDataGridRowControl row)
                _pool.Enqueue(row);
        }
    }
}
