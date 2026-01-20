using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Avalonia.Controls.DataGrid;

public class RepeaterDataGrid : TemplatedControl
{
    private const double WidthEpsilon = 0.25;
    private const string HeaderRepeaterPartName = "PART_HeaderRepeater";
    private const string RowsRepeaterPartName = "PART_RowsRepeater";
    private const string ScrollerPartName = "PART_Scroller";

    public static readonly DirectProperty<RepeaterDataGrid, IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.RegisterDirect<RepeaterDataGrid, IEnumerable?>(
            nameof(ItemsSource),
            o => o.ItemsSource,
            (o, v) => o.ItemsSource = v);

    public static readonly DirectProperty<RepeaterDataGrid, AvaloniaList<RepeaterDataGridColumn>> ColumnsProperty =
        AvaloniaProperty.RegisterDirect<RepeaterDataGrid, AvaloniaList<RepeaterDataGridColumn>>(
            nameof(Columns),
            o => o.Columns);

    public static readonly StyledProperty<SelectionMode> SelectionModeProperty =
        AvaloniaProperty.Register<RepeaterDataGrid, SelectionMode>(nameof(SelectionMode), SelectionMode.Single);

    public static readonly StyledProperty<string?> RowHeightBindingPathProperty =
        AvaloniaProperty.Register<RepeaterDataGrid, string?>(nameof(RowHeightBindingPath));

    public static readonly DirectProperty<RepeaterDataGrid, RepeaterDataGridCellInfo?> SelectedCellProperty =
        AvaloniaProperty.RegisterDirect<RepeaterDataGrid, RepeaterDataGridCellInfo?>(
            nameof(SelectedCell),
            o => o.SelectedCell,
            (o, v) => o.SelectedCell = v);

    public static readonly DirectProperty<RepeaterDataGrid, double> TotalColumnWidthProperty =
        AvaloniaProperty.RegisterDirect<RepeaterDataGrid, double>(
            nameof(TotalColumnWidth),
            o => o.TotalColumnWidth);

    public static readonly DirectProperty<RepeaterDataGrid, double> HeaderHeightProperty =
        AvaloniaProperty.RegisterDirect<RepeaterDataGrid, double>(
            nameof(HeaderHeight),
            o => o.HeaderHeight);

    public static readonly StyledProperty<int> AutoMeasureRowLimitProperty =
        AvaloniaProperty.Register<RepeaterDataGrid, int>(nameof(AutoMeasureRowLimit), int.MaxValue);

    private readonly AvaloniaList<RepeaterDataGridColumn> _columns = new();
    private readonly HashSet<Border> _autoMeasureQueue = new();
    private readonly TranslateTransform _headerTransform = new();
    private List<double> _autoColumnWidths = new();
    private List<double> _columnWidths = new();
    private double _lastMeasuredAvailableWidth = double.NaN;
    private double _totalColumnWidth;
    private double _headerHeight;
    private IEnumerable? _itemsSource;
    private RepeaterDataGridCellInfo? _selectedCell;
    private bool _autoMeasureQueued;
    private bool _autoMeasureCompleted;
    private bool _pendingHeaderWidthUpdate;
    private bool _updatingColumnIndices;
    private bool _updatingColumnWidths;
    private ItemsRepeater? _headerRepeater;
    private SelectingItemsRepeater? _rowsRepeater;
    private ScrollViewer? _scroller;
    private int _autoMeasuredRowCount;

    public RepeaterDataGrid()
    {
        _columns.CollectionChanged += OnColumnsCollectionChanged;
        SizeChanged += OnSizeChanged;
    }

    public AvaloniaList<RepeaterDataGridColumn> Columns => _columns;

    public IEnumerable? ItemsSource
    {
        get => _itemsSource;
        set => SetAndRaise(ItemsSourceProperty, ref _itemsSource, value);
    }

    public SelectionMode SelectionMode
    {
        get => GetValue(SelectionModeProperty);
        set => SetValue(SelectionModeProperty, value);
    }

    public string? RowHeightBindingPath
    {
        get => GetValue(RowHeightBindingPathProperty);
        set => SetValue(RowHeightBindingPathProperty, value);
    }

    public RepeaterDataGridCellInfo? SelectedCell
    {
        get => _selectedCell;
        set => SetAndRaise(SelectedCellProperty, ref _selectedCell, value);
    }

    public double TotalColumnWidth
    {
        get => _totalColumnWidth;
        private set => SetAndRaise(TotalColumnWidthProperty, ref _totalColumnWidth, value);
    }

    public double HeaderHeight
    {
        get => _headerHeight;
        private set => SetAndRaise(HeaderHeightProperty, ref _headerHeight, value);
    }

    public int AutoMeasureRowLimit
    {
        get => GetValue(AutoMeasureRowLimitProperty);
        set => SetValue(AutoMeasureRowLimitProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        DetachTemplateParts();

        _headerRepeater = e.NameScope.Find<ItemsRepeater>(HeaderRepeaterPartName);
        _rowsRepeater = e.NameScope.Find<SelectingItemsRepeater>(RowsRepeaterPartName);
        _scroller = e.NameScope.Find<ScrollViewer>(ScrollerPartName);

        if (_headerRepeater is not null)
        {
            _headerRepeater.ElementPrepared += OnHeaderPrepared;
            _headerRepeater.ItemsSource = _columns;
        }

        if (_rowsRepeater is not null)
        {
            _rowsRepeater.ElementPrepared += OnRowPrepared;
            _rowsRepeater.SelectionChanged += OnSelectionChanged;
            _rowsRepeater.AddHandler(InputElement.PointerPressedEvent, OnRepeaterPointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
            _rowsRepeater.AddHandler(InputElement.PointerReleasedEvent, OnRepeaterPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
            _rowsRepeater.ItemsSource = ItemsSource;
            _rowsRepeater.SelectionMode = SelectionMode;
        }

        if (_scroller is not null)
        {
            _scroller.ScrollChanged += OnScrollChanged;
        }

        UpdateColumnWidths();
        RequestAutoColumnMeasurement();
        UpdateHeaderTransform();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateColumnWidths();
        UpdateHeaderTransform();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ItemsSourceProperty)
        {
            if (_rowsRepeater is not null)
            {
                _rowsRepeater.ItemsSource = ItemsSource;
            }

            ResetAutoColumnWidths();
            RequestAutoColumnMeasurement();
        }
        else if (change.Property == SelectionModeProperty)
        {
            if (_rowsRepeater is not null)
            {
                _rowsRepeater.SelectionMode = SelectionMode;
            }
        }
        else if (change.Property == RowHeightBindingPathProperty)
        {
            InvalidateMeasure();
        }
        else if (change.Property == SelectedCellProperty)
        {
            UpdateCellSelectionVisuals();
        }
        else if (change.Property == AutoMeasureRowLimitProperty)
        {
            _autoMeasuredRowCount = 0;
            _autoMeasureCompleted = AutoMeasureRowLimit <= 0;
            _autoMeasureQueue.Clear();
            RequestAutoColumnMeasurement();
        }
    }

    private void DetachTemplateParts()
    {
        if (_headerRepeater is not null)
        {
            _headerRepeater.ElementPrepared -= OnHeaderPrepared;
        }

        if (_rowsRepeater is not null)
        {
            _rowsRepeater.ElementPrepared -= OnRowPrepared;
            _rowsRepeater.SelectionChanged -= OnSelectionChanged;
            _rowsRepeater.RemoveHandler(InputElement.PointerPressedEvent, OnRepeaterPointerPressed);
            _rowsRepeater.RemoveHandler(InputElement.PointerReleasedEvent, OnRepeaterPointerReleased);
        }

        if (_scroller is not null)
        {
            _scroller.ScrollChanged -= OnScrollChanged;
        }

        _headerRepeater = null;
        _rowsRepeater = null;
        _scroller = null;
    }

    private void OnColumnsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<RepeaterDataGridColumn>())
            {
                item.PropertyChanged -= OnColumnPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<RepeaterDataGridColumn>())
            {
                item.PropertyChanged += OnColumnPropertyChanged;
            }
        }

        UpdateColumnIndices();
        ResetAutoColumnWidths();
        ResetHeaderHeight();
        UpdateColumnWidths();
        UpdateCellSelectionVisuals();
        RequestAutoColumnMeasurement();
        RequestHeaderWidthUpdate();
        InvalidateMeasure();
    }

    private void UpdateColumnIndices()
    {
        _updatingColumnIndices = true;

        for (var i = 0; i < _columns.Count; i++)
        {
            _columns[i].Index = i;
        }

        _updatingColumnIndices = false;
    }

    private void OnColumnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_updatingColumnIndices || _updatingColumnWidths)
        {
            return;
        }

        if (e.Property == RepeaterDataGridColumn.IndexProperty ||
            e.Property == RepeaterDataGridColumn.ActualWidthProperty)
        {
            return;
        }

        ResetAutoColumnWidths();
        ResetHeaderHeight();
        UpdateColumnWidths();
        UpdateCellSelectionVisuals();
        RequestAutoColumnMeasurement();
        RequestHeaderWidthUpdate();
        InvalidateMeasure();
    }

    private void UpdateColumnWidths()
    {
        EnsureAutoColumnWidths();

        if (_columns.Count == 0)
        {
            _columnWidths = new List<double>();
            TotalColumnWidth = 0;
            ResetAutoColumnWidths();
            UpdateColumnActualWidths(Array.Empty<double>());
            return;
        }

        var availableWidth = GetAvailableWidth();
        var hasFiniteWidth = availableWidth > 0 && !double.IsInfinity(availableWidth);

        var widths = new double[_columns.Count];
        var starColumns = new List<(int Index, double Weight, double MinWidth)>();
        var fixedWidth = 0.0;

        for (var i = 0; i < _columns.Count; i++)
        {
            var column = _columns[i];
            var width = column.Width;
            var minWidth = Math.Max(0, column.MinWidth);

            if (width.IsAbsolute)
            {
                widths[i] = Math.Max(width.Value, minWidth);
                fixedWidth += widths[i];
            }
            else if (width.IsAuto)
            {
                var autoWidth = MeasureHeaderWidth(i);
                autoWidth = Math.Max(autoWidth, GetAutoColumnWidth(i));
                widths[i] = Math.Max(autoWidth, minWidth);
                fixedWidth += widths[i];
            }
            else
            {
                starColumns.Add((i, width.Value, minWidth));
            }
        }

        var remaining = hasFiniteWidth ? Math.Max(0.0, availableWidth - fixedWidth) : 0.0;
        var totalStar = starColumns.Sum(column => column.Weight);

        foreach (var (index, weight, minWidth) in starColumns)
        {
            var starWidth = hasFiniteWidth && totalStar > 0 ? remaining * (weight / totalStar) : 0;
            widths[index] = Math.Max(starWidth, minWidth);
        }

        for (var i = 0; i < widths.Length; i++)
        {
            widths[i] = Math.Max(1, widths[i]);
        }

        if (AreColumnWidthsUnchanged(widths))
        {
            return;
        }

        _columnWidths = widths.ToList();
        TotalColumnWidth = _columnWidths.Sum();
        UpdateColumnActualWidths(_columnWidths);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _lastMeasuredAvailableWidth = availableSize.Width;
        UpdateColumnWidths();
        return base.MeasureOverride(availableSize);
    }

    private void UpdateColumnActualWidths(IReadOnlyList<double> widths)
    {
        _updatingColumnWidths = true;

        try
        {
            for (var i = 0; i < _columns.Count; i++)
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

    private double MeasureHeaderWidth(int columnIndex)
    {
        if (_headerRepeater is null || columnIndex < 0 || columnIndex >= _columns.Count)
        {
            return 0;
        }

        var element = _headerRepeater.TryGetElement(columnIndex);
        return MeasureElementWidth(element);
    }

    private void OnHeaderPrepared(object? sender, ItemsRepeaterElementPreparedEventArgs e)
    {
        RequestHeaderWidthUpdate();
    }

    private void OnRowPrepared(object? sender, ItemsRepeaterElementPreparedEventArgs e)
    {
        if (e.Element is Border rowBorder)
        {
            UpdateRowCellSelection(rowBorder);
            UpdateRowSelection(rowBorder);
            QueueAutoMeasureRow(rowBorder);
        }
    }

    private void OnRepeaterPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        HandlePointerSelection(e);
    }

    private void OnRepeaterPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        HandlePointerSelection(e);
    }

    private void HandlePointerSelection(PointerEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        var rowsRepeater = _rowsRepeater;
        if (rowsRepeater is null)
        {
            return;
        }

        var source = e.Source as Visual;
        if (source is null)
        {
            return;
        }

        var cellBorder = FindCellBorder(source);
        var rowBorder = cellBorder is not null ? FindRowBorder(cellBorder) : FindRowBorder(source);
        if (rowBorder is null)
        {
            return;
        }

        var rowIndex = rowsRepeater.GetElementIndex(rowBorder);
        if (rowIndex < 0)
        {
            return;
        }

        if (!rowsRepeater.UpdateSelectionFromEvent(rowBorder, e))
        {
            return;
        }

        if (cellBorder?.Tag is int columnIndex)
        {
            SelectedCell = new RepeaterDataGridCellInfo(rowIndex, columnIndex);
        }
    }

    private void UpdateCellSelectionVisuals()
    {
        var rowsRepeater = _rowsRepeater;
        if (rowsRepeater is null)
        {
            return;
        }

        foreach (var child in rowsRepeater.Children)
        {
            if (child is Border rowBorder)
            {
                UpdateRowCellSelection(rowBorder);
            }
        }
    }

    private void UpdateRowCellSelection(Border rowBorder)
    {
        var rowsRepeater = _rowsRepeater;
        if (rowsRepeater is null)
        {
            return;
        }

        var rowIndex = rowsRepeater.GetElementIndex(rowBorder);
        if (rowIndex < 0)
        {
            return;
        }

        foreach (var cell in EnumerateRowCells(rowBorder))
        {
            var columnIndex = cell.Tag is int value ? value : -1;
            var isSelected = SelectedCell is { } selected &&
                             selected.RowIndex == rowIndex &&
                             selected.ColumnIndex == columnIndex;

            UpdateClass(cell.Classes, "cellSelected", isSelected);
        }
    }

    private void UpdateRowSelection(Border rowBorder)
    {
        var rowsRepeater = _rowsRepeater;
        if (rowsRepeater is null)
        {
            return;
        }

        var rowIndex = rowsRepeater.GetElementIndex(rowBorder);
        if (rowIndex < 0)
        {
            return;
        }

        UpdateClass(rowBorder.Classes, "rowSelected", rowsRepeater.Selection.IsSelected(rowIndex));
    }


    private void RequestAutoColumnMeasurement()
    {
        if (_autoMeasureQueued || !HasAutoColumns() || _autoMeasureCompleted || AutoMeasureRowLimit <= 0)
        {
            return;
        }

        if (_autoMeasureQueue.Count == 0)
        {
            QueueAutoMeasureRealizedRows();
            if (_autoMeasureQueue.Count == 0)
            {
                return;
            }
        }

        _autoMeasureQueued = true;

        Dispatcher.UIThread.Post(() =>
        {
            _autoMeasureQueued = false;
            if (!HasAutoColumns() || _autoMeasureCompleted || AutoMeasureRowLimit <= 0)
            {
                _autoMeasureQueue.Clear();
                return;
            }

            if (_autoMeasureQueue.Count == 0)
            {
                return;
            }

            var rows = _autoMeasureQueue.ToArray();
            _autoMeasureQueue.Clear();

            if (UpdateAutoColumnWidthsFromRows(rows))
            {
                UpdateColumnWidths();
            }
        }, DispatcherPriority.Background);
    }

    private void RequestHeaderWidthUpdate()
    {
        if (_pendingHeaderWidthUpdate)
        {
            return;
        }

        _pendingHeaderWidthUpdate = true;

        Dispatcher.UIThread.Post(() =>
        {
            _pendingHeaderWidthUpdate = false;
            UpdateHeaderHeight();
            UpdateColumnWidths();
        }, DispatcherPriority.Background);
    }

    private void ResetHeaderHeight()
    {
        HeaderHeight = 0;
    }

    private void UpdateHeaderHeight()
    {
        if (_headerRepeater is null)
        {
            return;
        }

        var maxHeight = 0.0;
        foreach (var child in _headerRepeater.Children.OfType<Control>())
        {
            maxHeight = Math.Max(maxHeight, MeasureElementHeight(child));
        }

        if (Math.Abs(maxHeight - _headerHeight) > WidthEpsilon)
        {
            HeaderHeight = maxHeight;
        }
    }

    private void QueueAutoMeasureRealizedRows()
    {
        var rowsRepeater = _rowsRepeater;
        if (rowsRepeater is null)
        {
            return;
        }

        foreach (var child in rowsRepeater.Children)
        {
            if (child is Border rowBorder)
            {
                _autoMeasureQueue.Add(rowBorder);
            }
        }
    }

    private bool UpdateAutoColumnWidthsFromRows(IEnumerable<Border> rows)
    {
        if (_columns.Count == 0)
        {
            return false;
        }

        if (_autoMeasureCompleted || AutoMeasureRowLimit <= 0)
        {
            return false;
        }

        EnsureAutoColumnWidths();

        var updated = false;
        var limit = AutoMeasureRowLimit;

        foreach (var rowBorder in rows)
        {
            if (_autoMeasureCompleted)
            {
                break;
            }

            if (!rowBorder.IsAttachedToVisualTree())
            {
                continue;
            }

            foreach (var cell in EnumerateRowCells(rowBorder))
            {
                if (cell.Tag is not int columnIndex || columnIndex < 0 || columnIndex >= _columns.Count)
                {
                    continue;
                }

                if (!_columns[columnIndex].Width.IsAuto)
                {
                    continue;
                }

                var desiredWidth = MeasureCellWidth(cell);
                if (desiredWidth > _autoColumnWidths[columnIndex])
                {
                    _autoColumnWidths[columnIndex] = desiredWidth;
                    updated = true;
                }
            }

            _autoMeasuredRowCount++;
            if (_autoMeasuredRowCount >= limit)
            {
                _autoMeasureCompleted = true;
            }
        }

        return updated;
    }

    private static IEnumerable<Border> EnumerateRowCells(Border rowBorder)
    {
        if (rowBorder.Child is ItemsRepeater rowCells)
        {
            return rowCells.Children.OfType<Border>();
        }

        return Array.Empty<Border>();
    }

    private static double MeasureCellWidth(Border cell)
    {
        return MeasureBorderContentWidth(cell);
    }

    private static double MeasureElementWidth(Control? element)
    {
        if (element is null)
        {
            return 0;
        }

        if (element is Border border)
        {
            return MeasureBorderContentWidth(border);
        }

        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return element.DesiredSize.Width;
    }

    private static double MeasureElementHeight(Control? element)
    {
        if (element is null)
        {
            return 0;
        }

        if (element is Border border)
        {
            return MeasureBorderContentHeight(border);
        }

        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return element.DesiredSize.Height;
    }

    private static double MeasureBorderContentWidth(Border border)
    {
        if (border.Child is null)
        {
            border.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return border.DesiredSize.Width;
        }

        border.Child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var desired = border.Child.DesiredSize.Width;
        var padding = border.Padding;
        var thickness = border.BorderThickness;
        return desired + padding.Left + padding.Right + thickness.Left + thickness.Right;
    }

    private static double MeasureBorderContentHeight(Border border)
    {
        if (border.Child is null)
        {
            border.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return border.DesiredSize.Height;
        }

        border.Child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var desired = border.Child.DesiredSize.Height;
        var padding = border.Padding;
        var thickness = border.BorderThickness;
        return desired + padding.Top + padding.Bottom + thickness.Top + thickness.Bottom;
    }

    private bool HasAutoColumns() => _columns.Any(column => column.Width.IsAuto);

    private void EnsureAutoColumnWidths()
    {
        if (_autoColumnWidths.Count != _columns.Count)
        {
            ResetAutoColumnWidths();
        }
    }

    private void ResetAutoColumnWidths()
    {
        _autoColumnWidths = Enumerable.Repeat(0.0, _columns.Count).ToList();
        _autoMeasureQueue.Clear();
        _autoMeasuredRowCount = 0;
        _autoMeasureCompleted = AutoMeasureRowLimit <= 0;
    }

    private double GetAutoColumnWidth(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= _autoColumnWidths.Count)
        {
            return 0;
        }

        return _autoColumnWidths[columnIndex];
    }

    private void QueueAutoMeasureRow(Border rowBorder)
    {
        if (!HasAutoColumns() || _autoMeasureCompleted || AutoMeasureRowLimit <= 0)
        {
            return;
        }

        _autoMeasureQueue.Add(rowBorder);
        RequestAutoColumnMeasurement();
    }

    private bool AreColumnWidthsUnchanged(double[] widths)
    {
        if (_columnWidths.Count != widths.Length)
        {
            return false;
        }

        for (var i = 0; i < widths.Length; i++)
        {
            if (Math.Abs(_columnWidths[i] - widths[i]) > WidthEpsilon)
            {
                return false;
            }
        }

        return Math.Abs(_totalColumnWidth - widths.Sum()) <= WidthEpsilon;
    }

    private static void UpdateClass(Classes classes, string className, bool isEnabled)
    {
        if (isEnabled)
        {
            if (!classes.Contains(className))
            {
                classes.Add(className);
            }
        }
        else
        {
            classes.Remove(className);
        }
    }

    private Border? FindRowBorder(Visual element)
    {
        return FindBorderByClass(element, "dataGridRow");
    }

    private Border? FindCellBorder(Visual element)
    {
        return FindBorderByClass(element, "dataGridCell");
    }

    private Border? FindBorderByClass(Visual element, string className)
    {
        for (var current = element; current != null; current = current.GetVisualParent())
        {
            if (current is Border border && border.Classes.Contains(className))
            {
                return border;
            }
        }

        return null;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateRowSelectionVisuals();
        UpdateCellSelectionVisuals();
    }

    private void UpdateRowSelectionVisuals()
    {
        var rowsRepeater = _rowsRepeater;
        if (rowsRepeater is null)
        {
            return;
        }

        foreach (var child in rowsRepeater.Children)
        {
            if (child is Border rowBorder)
            {
                UpdateRowSelection(rowBorder);
            }
        }
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        UpdateHeaderTransform();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateColumnWidths();
    }

    private void UpdateHeaderTransform()
    {
        if (_headerRepeater is null || _scroller is null)
        {
            return;
        }

        var offset = _scroller.Offset;
        _headerTransform.X = -offset.X;
        _headerTransform.Y = 0;
        _headerRepeater.RenderTransform = _headerTransform;
    }

    private double GetAvailableWidth()
    {
        var width = Bounds.Width;
        if (width <= 0)
        {
            width = _scroller?.Bounds.Width ?? 0;
        }

        if ((width <= 0 || double.IsInfinity(width)) &&
            !double.IsNaN(_lastMeasuredAvailableWidth) &&
            !double.IsInfinity(_lastMeasuredAvailableWidth))
        {
            width = _lastMeasuredAvailableWidth;
        }

        return width;
    }
}
