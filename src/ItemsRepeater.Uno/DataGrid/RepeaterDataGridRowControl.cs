using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Avalonia.Controls.DataGrid;

internal sealed class RepeaterDataGridRowControl : Border
{
    private static readonly SolidColorBrush GridBorderBrush = new(Color.FromArgb(0xFF, 0xD9, 0xD9, 0xD9));
    private static readonly SolidColorBrush TransparentBrush = new(Color.FromArgb(0x00, 0x00, 0x00, 0x00));
    private static readonly SolidColorBrush SelectedRowBrush = new(Color.FromArgb(0xFF, 0xD9, 0xEC, 0xFF));
    private static readonly SolidColorBrush SelectedCellBrush = new(Color.FromArgb(0xFF, 0x25, 0x63, 0xEB));
    private static readonly SolidColorBrush SelectedCellForegroundBrush = new(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));

    private readonly StackPanel _panel;
    private readonly List<Border> _cells = new();
    private readonly List<string?> _cellBindingPaths = new();
    private readonly List<bool> _cellUsesTemplate = new();
    private string? _rowHeightBindingPath;

    public RepeaterDataGridRowControl()
    {
        BorderBrush = GridBorderBrush;
        BorderThickness = new Thickness(0, 0, 0, 1);
        HorizontalAlignment = HorizontalAlignment.Left;
        _panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        Child = _panel;
    }

    public int RowIndex { get; private set; }

    public IReadOnlyList<Border> Cells => _cells;

    public void Bind(object? item, int rowIndex, IReadOnlyList<RepeaterDataGridColumn> columns, string? rowHeightBindingPath)
    {
        DataContext = item;
        RowIndex = rowIndex;
        EnsureCellCount(columns.Count);
        UpdateRowHeightBinding(rowHeightBindingPath);
        ApplyColumnWidths(columns);

        for (var i = 0; i < columns.Count; ++i)
            UpdateCell(i, columns[i], item);
    }

    public void ApplyColumnWidths(IReadOnlyList<RepeaterDataGridColumn> columns)
    {
        for (var i = 0; i < _cells.Count && i < columns.Count; ++i)
        {
            _cells[i].Tag = i;
            var width = columns[i].ActualWidth;
            _cells[i].Width = double.IsNaN(width) || width <= 0 ? double.NaN : width;
            _cells[i].MinWidth = columns[i].MinWidth;
        }
    }

    public void ApplySelectionState(bool rowSelected, RepeaterDataGridCellInfo? selectedCell)
    {
        Background = rowSelected ? SelectedRowBrush : TransparentBrush;

        for (var i = 0; i < _cells.Count; ++i)
        {
            var isSelectedCell = selectedCell is { RowIndex: var row, ColumnIndex: var column } &&
                                 row == RowIndex &&
                                 column == i;

            _cells[i].Background = isSelectedCell ? SelectedCellBrush : TransparentBrush;

            if (_cells[i].Child is TextBlock textBlock)
                textBlock.Foreground = isSelectedCell
                    ? SelectedCellForegroundBrush
                    : null;
        }
    }

    public double GetDesiredCellWidth(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= _cells.Count)
            return 0;

        var cell = _cells[columnIndex];
        if (cell.Child is null)
        {
            cell.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            return cell.DesiredSize.Width;
        }

        cell.Child.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        var desired = cell.Child.DesiredSize.Width;
        var padding = cell.Padding;
        var thickness = cell.BorderThickness;
        return desired + padding.Left + padding.Right + thickness.Left + thickness.Right;
    }

    private void EnsureCellCount(int count)
    {
        while (_cells.Count < count)
        {
            var cell = new Border
            {
                BorderBrush = GridBorderBrush,
                BorderThickness = new Thickness(1, 0, 1, 1),
                Padding = new Thickness(8, 4, 8, 4),
                Background = TransparentBrush,
            };
            _cells.Add(cell);
            _cellBindingPaths.Add(null);
            _cellUsesTemplate.Add(false);
            _panel.Children.Add(cell);
        }

        while (_cells.Count > count)
        {
            var last = _cells[^1];
            _cells.RemoveAt(_cells.Count - 1);
            _cellBindingPaths.RemoveAt(_cellBindingPaths.Count - 1);
            _cellUsesTemplate.RemoveAt(_cellUsesTemplate.Count - 1);
            _panel.Children.Remove(last);
        }
    }

    private void UpdateRowHeightBinding(string? rowHeightBindingPath)
    {
        if (string.Equals(_rowHeightBindingPath, rowHeightBindingPath, System.StringComparison.Ordinal))
            return;

        _rowHeightBindingPath = rowHeightBindingPath;

        if (string.IsNullOrWhiteSpace(rowHeightBindingPath))
        {
            ClearValue(HeightProperty);
            return;
        }

        SetBinding(
            HeightProperty,
            new Binding
            {
                Path = new PropertyPath(rowHeightBindingPath),
                Mode = BindingMode.OneWay,
            });
    }

    private void UpdateCell(int index, RepeaterDataGridColumn column, object? item)
    {
        var cell = _cells[index];

        if (column.CellTemplate is not null)
        {
            var contentControl = cell.Child as ContentControl;
            if (!_cellUsesTemplate[index] || contentControl is null)
            {
                contentControl = new ContentControl
                {
                    VerticalAlignment = VerticalAlignment.Center,
                };
                cell.Child = contentControl;
                _cellUsesTemplate[index] = true;
                _cellBindingPaths[index] = null;
            }

            if (!ReferenceEquals(contentControl.ContentTemplate, column.CellTemplate))
                contentControl.ContentTemplate = column.CellTemplate;

            if (!ReferenceEquals(contentControl.Content, item))
                contentControl.Content = item;

            return;
        }

        var textBlock = cell.Child as TextBlock;
        if (_cellUsesTemplate[index] || textBlock is null)
        {
            textBlock = new TextBlock
            {
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };
            cell.Child = textBlock;
            _cellUsesTemplate[index] = false;
            _cellBindingPaths[index] = null;
        }

        textBlock.DataContext = item;

        if (string.IsNullOrWhiteSpace(column.BindingPath))
        {
            if (_cellBindingPaths[index] is not null)
            {
                textBlock.ClearValue(TextBlock.TextProperty);
                _cellBindingPaths[index] = null;
            }

            var text = item?.ToString() ?? string.Empty;
            if (!string.Equals(textBlock.Text, text, System.StringComparison.Ordinal))
                textBlock.Text = text;
        }
        else if (!string.Equals(_cellBindingPaths[index], column.BindingPath, System.StringComparison.Ordinal))
        {
            textBlock.SetBinding(
                TextBlock.TextProperty,
                new Binding
                {
                    Path = new PropertyPath(column.BindingPath),
                    Mode = BindingMode.OneWay,
                });
            _cellBindingPaths[index] = column.BindingPath;
        }
    }
}
