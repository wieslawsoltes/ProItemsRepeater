using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
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
    private const double WidthEpsilon = 0.25;
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
            new PropertyMetadata(int.MaxValue, OnAutoMeasureRowLimitChanged));

    public static readonly DependencyProperty TotalColumnWidthProperty =
        DependencyProperty.Register(
            nameof(TotalColumnWidth),
            typeof(double),
            typeof(RepeaterDataGrid),
            new PropertyMetadata(0d));

    public static readonly DependencyProperty HeaderHeightProperty =
        DependencyProperty.Register(
            nameof(HeaderHeight),
            typeof(double),
            typeof(RepeaterDataGrid),
            new PropertyMetadata(0d));

    private readonly AvaloniaList<RepeaterDataGridColumn> _columns = new();
    private readonly Dictionary<int, RepeaterDataGridRowControl> _realizedRows = new();
    private readonly HashSet<RepeaterDataGridRowControl> _autoMeasureQueue = new();
    private readonly RowElementFactory _rowFactory = new();
    private readonly RectangleGeometry _headerClip = new();
    private readonly TranslateTransform _headerTransform = new();
    private readonly PointerEventHandler _rowsPointerPressedHandler;
    private readonly PointerEventHandler _rowsPointerReleasedHandler;
    private List<double> _autoColumnWidths = new();
    private List<double> _columnWidths = new();
    private double _lastMeasuredAvailableWidth = double.NaN;
    private Border? _headerHost;
    private StackPanel? _headerPanel;
    private SelectingItemsRepeater? _rowsRepeater;
    private ScrollViewer? _scroller;
    private bool _autoMeasureQueued;
    private bool _autoMeasureCompleted;
    private bool _pendingHeaderWidthUpdate;
    private bool _updatingColumnIndices;
    private bool _updatingColumnWidths;
    private int _autoMeasuredRowCount;

    public RepeaterDataGrid()
    {
        DefaultStyleKey = typeof(RepeaterDataGrid);
        _columns.CollectionChanged += OnColumnsCollectionChanged;
        SizeChanged += OnSizeChanged;
        _rowsPointerPressedHandler = OnRowsPointerPressed;
        _rowsPointerReleasedHandler = OnRowsPointerReleased;
        ResetAutoColumnWidths();
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

    public double TotalColumnWidth
    {
        get => (double)GetValue(TotalColumnWidthProperty);
        private set => SetValue(TotalColumnWidthProperty, value);
    }

    public double HeaderHeight
    {
        get => (double)GetValue(HeaderHeightProperty);
        private set => SetValue(HeaderHeightProperty, value);
    }

    protected override void OnApplyTemplate()
    {
        DetachTemplateParts();
        base.OnApplyTemplate();

        _headerHost = GetTemplateChild(HeaderHostPartName) as Border;
        _headerPanel = GetTemplateChild(HeaderPanelPartName) as StackPanel;
        _scroller = GetTemplateChild(BodyScrollViewerPartName) as ScrollViewer;
        _rowsRepeater = GetTemplateChild(RowsRepeaterPartName) as SelectingItemsRepeater;

        if (_scroller is not null)
            _scroller.ViewChanged += OnScrollViewChanged;

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
            _rowsRepeater.AddHandler(PointerPressedEvent, _rowsPointerPressedHandler, true);
            _rowsRepeater.AddHandler(PointerReleasedEvent, _rowsPointerReleasedHandler, true);
        }

        BuildHeader();
        UpdateColumnWidths();
        RequestHeaderWidthUpdate();
        RequestAutoColumnMeasurement();
        SyncHeaderScroll();
        RebindRealizedRows();
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
            _rowsRepeater.RemoveHandler(PointerPressedEvent, _rowsPointerPressedHandler);
            _rowsRepeater.RemoveHandler(PointerReleasedEvent, _rowsPointerReleasedHandler);
        }

        if (_scroller is not null)
            _scroller.ViewChanged -= OnScrollViewChanged;

        _realizedRows.Clear();
        _headerHost = null;
        _headerPanel = null;
        _rowsRepeater = null;
        _scroller = null;
        _headerTransform.X = 0;
    }

    private void OnColumnsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems)
            {
                if (item is RepeaterDataGridColumn column)
                    column.PropertyChanged -= OnColumnPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems)
            {
                if (item is RepeaterDataGridColumn column)
                    column.PropertyChanged += OnColumnPropertyChanged;
            }
        }

        UpdateColumnIndices();
        ResetAutoColumnWidths();
        ResetHeaderHeight();
        BuildHeader();
        UpdateColumnWidths();
        RebindRealizedRows();
        RequestHeaderWidthUpdate();
        RequestAutoColumnMeasurement();
    }

    private void OnColumnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_updatingColumnIndices || _updatingColumnWidths)
            return;

        if (e.PropertyName is nameof(RepeaterDataGridColumn.Index) or nameof(RepeaterDataGridColumn.ActualWidth))
            return;

        if (e.PropertyName is nameof(RepeaterDataGridColumn.Header) or nameof(RepeaterDataGridColumn.HeaderTemplate))
        {
            ResetHeaderHeight();
            BuildHeader();
            RequestHeaderWidthUpdate();
        }

        ResetAutoColumnWidths();
        UpdateColumnWidths();
        if (e.PropertyName is nameof(RepeaterDataGridColumn.BindingPath) or nameof(RepeaterDataGridColumn.CellTemplate))
            RebindRealizedRows();
        RequestAutoColumnMeasurement();
    }

    private void UpdateColumnIndices()
    {
        _updatingColumnIndices = true;
        try
        {
            for (var i = 0; i < _columns.Count; ++i)
                _columns[i].Index = i;
        }
        finally
        {
            _updatingColumnIndices = false;
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _ = sender;
        _lastMeasuredAvailableWidth = e.NewSize.Width;
        UpdateHeaderClip();
        UpdateColumnWidths();
        RequestHeaderWidthUpdate();
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
        QueueAutoMeasureRow(row);
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
        QueueAutoMeasureRow(row);
    }

    private void OnRowsSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        UpdateRealizedRowsSelection();
    }

    private void OnRowsPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _ = sender;
        HandlePointerSelection(e);
    }

    private void OnRowsPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _ = sender;
        HandlePointerSelection(e);
    }

    private void HandlePointerSelection(PointerRoutedEventArgs e)
    {
        if (e.Handled || _rowsRepeater is null)
            return;

        var source = e.OriginalSource as DependencyObject;
        var cell = FindCellBorder(source);
        var row = cell is not null
            ? FindAncestor<RepeaterDataGridRowControl>(cell)
            : FindAncestor<RepeaterDataGridRowControl>(source);

        if (row is null)
            return;

        if (!_rowsRepeater.UpdateSelectionFromEvent(row, e))
            return;

        if (cell?.Tag is int columnIndex)
            SelectedCell = new RepeaterDataGridCellInfo(row.RowIndex, columnIndex);
    }

    private void OnScrollViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        SyncHeaderScroll();
    }

    private void SyncHeaderScroll()
    {
        if (_headerPanel is null || _scroller is null)
            return;

        _headerTransform.X = -_scroller.HorizontalOffset;
        _headerTransform.Y = 0;
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
            _headerPanel.Children.Add(CreateHeaderCell(column));

        ApplyHeaderWidths();
        UpdateHeaderHeight();
    }

    private void UpdateColumnWidths()
    {
        EnsureAutoColumnWidths();

        if (_columns.Count == 0)
        {
            _columnWidths = new List<double>();
            TotalColumnWidth = 0;
            UpdateColumnActualWidths(Array.Empty<double>());
            if (_rowsRepeater is not null)
                _rowsRepeater.Width = double.NaN;
            return;
        }

        var availableWidth = GetAvailableWidth();
        var hasFiniteWidth = availableWidth > 0 && !double.IsInfinity(availableWidth);
        var widths = new double[_columns.Count];
        var starColumns = new List<(int Index, double Weight, double MinWidth)>();
        var fixedWidth = 0d;

        for (var i = 0; i < _columns.Count; ++i)
        {
            var column = _columns[i];
            column.Index = i;
            var minWidth = Math.Max(0, column.MinWidth);

            if (column.Width.IsAbsolute)
            {
                widths[i] = Math.Max(column.Width.Value, minWidth);
                fixedWidth += widths[i];
            }
            else if (column.Width.IsAuto)
            {
                var autoWidth = Math.Max(MeasureHeaderWidth(i, column), GetAutoColumnWidth(i));
                widths[i] = Math.Max(autoWidth, minWidth);
                fixedWidth += widths[i];
            }
            else
            {
                starColumns.Add((i, Math.Max(1, column.Width.Value), minWidth));
            }
        }

        var remaining = hasFiniteWidth ? Math.Max(0, availableWidth - fixedWidth) : 0;
        var totalStarWeight = 0d;
        foreach (var starColumn in starColumns)
            totalStarWeight += starColumn.Weight;

        foreach (var starColumn in starColumns)
        {
            var starWidth = hasFiniteWidth && totalStarWeight > 0
                ? remaining * (starColumn.Weight / totalStarWeight)
                : 0;
            widths[starColumn.Index] = Math.Max(starColumn.MinWidth, starWidth);
        }

        for (var i = 0; i < widths.Length; ++i)
            widths[i] = Math.Max(1, widths[i]);

        if (AreColumnWidthsUnchanged(widths))
            return;

        _columnWidths = new List<double>(widths);

        var totalWidth = 0d;
        for (var i = 0; i < widths.Length; ++i)
            totalWidth += widths[i];

        TotalColumnWidth = totalWidth;
        UpdateColumnActualWidths(_columnWidths);
        ApplyHeaderWidths();
        ApplyRealizedRowColumnWidths();

        if (_rowsRepeater is not null)
            _rowsRepeater.Width = TotalColumnWidth;
    }

    private void UpdateColumnActualWidths(IReadOnlyList<double> widths)
    {
        _updatingColumnWidths = true;
        try
        {
            for (var i = 0; i < _columns.Count; ++i)
            {
                var width = i < widths.Count ? widths[i] : 0;
                _columns[i].ActualWidth = width;
            }
        }
        finally
        {
            _updatingColumnWidths = false;
        }
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

    private void RebindRealizedRows()
    {
        foreach (var pair in _realizedRows)
        {
            pair.Value.Bind(pair.Value.DataContext, pair.Key, _columns, RowHeightBindingPath);
            pair.Value.ApplySelectionState(IsRowSelected(pair.Key), SelectedCell);
        }
    }

    private void ApplyRealizedRowColumnWidths()
    {
        foreach (var pair in _realizedRows)
            pair.Value.ApplyColumnWidths(_columns);
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

    private double MeasureHeaderWidth(int columnIndex, RepeaterDataGridColumn column)
    {
        if (_headerPanel is not null &&
            columnIndex >= 0 &&
            columnIndex < _headerPanel.Children.Count &&
            _headerPanel.Children[columnIndex] is FrameworkElement element)
        {
            element.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            return Math.Max(element.DesiredSize.Width, column.MinWidth);
        }

        var headerCell = CreateHeaderCell(column);
        return Math.Max(headerCell.DesiredSize.Width, column.MinWidth);
    }

    private void RequestAutoColumnMeasurement()
    {
        if (_autoMeasureQueued || !HasAutoColumns() || _autoMeasureCompleted || AutoMeasureRowLimit <= 0)
            return;

        if (_autoMeasureQueue.Count == 0)
        {
            QueueAutoMeasureRealizedRows();
            if (_autoMeasureQueue.Count == 0)
                return;
        }

        _autoMeasureQueued = true;
        var dispatcherQueue = DispatcherQueue;
        if (dispatcherQueue is not null && dispatcherQueue.TryEnqueue(ProcessAutoColumnMeasurement))
            return;

        ProcessAutoColumnMeasurement();
    }

    private void ProcessAutoColumnMeasurement()
    {
        _autoMeasureQueued = false;

        if (!HasAutoColumns() || _autoMeasureCompleted || AutoMeasureRowLimit <= 0)
        {
            _autoMeasureQueue.Clear();
            return;
        }

        if (_autoMeasureQueue.Count == 0)
            return;

        var rows = new List<RepeaterDataGridRowControl>(_autoMeasureQueue);
        _autoMeasureQueue.Clear();

        if (UpdateAutoColumnWidthsFromRows(rows))
            UpdateColumnWidths();
    }

    private void RequestHeaderWidthUpdate()
    {
        if (_pendingHeaderWidthUpdate)
            return;

        _pendingHeaderWidthUpdate = true;
        var dispatcherQueue = DispatcherQueue;
        if (dispatcherQueue is not null && dispatcherQueue.TryEnqueue(ProcessHeaderWidthUpdate))
            return;

        ProcessHeaderWidthUpdate();
    }

    private void ProcessHeaderWidthUpdate()
    {
        _pendingHeaderWidthUpdate = false;
        UpdateHeaderHeight();
        UpdateColumnWidths();
    }

    private void ResetHeaderHeight()
    {
        HeaderHeight = 0;
    }

    private void UpdateHeaderHeight()
    {
        if (_headerPanel is null)
            return;

        var maxHeight = 0d;
        foreach (var child in _headerPanel.Children)
        {
            if (child is FrameworkElement element)
            {
                element.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
                maxHeight = Math.Max(maxHeight, element.DesiredSize.Height);
            }
        }

        if (Math.Abs(maxHeight - HeaderHeight) > WidthEpsilon)
            HeaderHeight = maxHeight;
    }

    private void QueueAutoMeasureRealizedRows()
    {
        foreach (var row in _realizedRows.Values)
            _autoMeasureQueue.Add(row);
    }

    private bool UpdateAutoColumnWidthsFromRows(IEnumerable<RepeaterDataGridRowControl> rows)
    {
        if (_columns.Count == 0 || _autoMeasureCompleted || AutoMeasureRowLimit <= 0)
            return false;

        EnsureAutoColumnWidths();

        var updated = false;
        var limit = AutoMeasureRowLimit;

        foreach (var row in rows)
        {
            if (_autoMeasureCompleted)
                break;

            if (!row.IsLoaded && row.Parent is null)
                continue;

            for (var columnIndex = 0; columnIndex < _columns.Count; ++columnIndex)
            {
                if (!_columns[columnIndex].Width.IsAuto)
                    continue;

                var desiredWidth = row.GetDesiredCellWidth(columnIndex);
                if (desiredWidth > _autoColumnWidths[columnIndex])
                {
                    _autoColumnWidths[columnIndex] = desiredWidth;
                    updated = true;
                }
            }

            ++_autoMeasuredRowCount;
            if (_autoMeasuredRowCount >= limit)
                _autoMeasureCompleted = true;
        }

        return updated;
    }

    private bool HasAutoColumns()
    {
        foreach (var column in _columns)
        {
            if (column.Width.IsAuto)
                return true;
        }

        return false;
    }

    private void EnsureAutoColumnWidths()
    {
        if (_autoColumnWidths.Count != _columns.Count)
            ResetAutoColumnWidths();
    }

    private void ResetAutoColumnWidths()
    {
        _autoColumnWidths = new List<double>(_columns.Count);
        for (var i = 0; i < _columns.Count; ++i)
            _autoColumnWidths.Add(0d);

        _autoMeasureQueue.Clear();
        _autoMeasuredRowCount = 0;
        _autoMeasureCompleted = AutoMeasureRowLimit <= 0;
    }

    private double GetAutoColumnWidth(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= _autoColumnWidths.Count)
            return 0;

        return _autoColumnWidths[columnIndex];
    }

    private void QueueAutoMeasureRow(RepeaterDataGridRowControl row)
    {
        if (!HasAutoColumns() || _autoMeasureCompleted || AutoMeasureRowLimit <= 0)
            return;

        _autoMeasureQueue.Add(row);
        RequestAutoColumnMeasurement();
    }

    private bool AreColumnWidthsUnchanged(double[] widths)
    {
        if (_columnWidths.Count != widths.Length)
            return false;

        var totalWidth = 0d;
        for (var i = 0; i < widths.Length; ++i)
        {
            totalWidth += widths[i];
            if (Math.Abs(_columnWidths[i] - widths[i]) > WidthEpsilon)
                return false;
        }

        return Math.Abs(TotalColumnWidth - totalWidth) <= WidthEpsilon;
    }

    private double GetAvailableWidth()
    {
        var width = ActualWidth;

        if (width <= 0)
            width = _scroller?.ActualWidth ?? 0;

        if ((width <= 0 || double.IsInfinity(width)) &&
            !double.IsNaN(_lastMeasuredAvailableWidth) &&
            !double.IsInfinity(_lastMeasuredAvailableWidth))
        {
            width = _lastMeasuredAvailableWidth;
        }

        return width;
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
        if (sender is not RepeaterDataGrid grid)
            return;

        if (grid._rowsRepeater is not null)
            grid._rowsRepeater.ItemsSource = (IEnumerable?)args.NewValue;

        grid._realizedRows.Clear();
        grid.ResetAutoColumnWidths();
        grid.UpdateColumnWidths();
        grid.RequestAutoColumnMeasurement();
    }

    private static void OnSelectionModeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is RepeaterDataGrid grid && grid._rowsRepeater is not null)
            grid._rowsRepeater.SelectionMode = (SelectionMode)args.NewValue;
    }

    private static void OnRowHeightBindingPathChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is RepeaterDataGrid grid)
            grid.RebindRealizedRows();
    }

    private static void OnSelectedCellChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is RepeaterDataGrid grid)
            grid.UpdateRealizedRowsSelection();
    }

    private static void OnAutoMeasureRowLimitChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not RepeaterDataGrid grid)
            return;

        grid.ResetAutoColumnWidths();
        grid.UpdateColumnWidths();
        grid.RequestAutoColumnMeasurement();
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
