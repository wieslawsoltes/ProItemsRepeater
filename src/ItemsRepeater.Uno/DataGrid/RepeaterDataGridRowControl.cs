using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Avalonia.Controls.DataGrid;

internal sealed class RepeaterDataGridRowControl : Border
{
    private readonly StackPanel _panel;
    private readonly List<Border> _cells = new();

    public RepeaterDataGridRowControl()
    {
        BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xD9, 0xD9, 0xD9));
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

        if (string.IsNullOrWhiteSpace(rowHeightBindingPath))
        {
            ClearValue(HeightProperty);
        }
        else
        {
            SetBinding(
                HeightProperty,
                new Binding
                {
                    Path = new PropertyPath(rowHeightBindingPath),
                    Mode = BindingMode.OneWay,
                });
        }

        for (var i = 0; i < columns.Count; ++i)
        {
            var column = columns[i];
            var cell = _cells[i];
            cell.Tag = i;
            cell.Width = double.IsNaN(column.ActualWidth) || column.ActualWidth <= 0 ? double.NaN : column.ActualWidth;
            cell.MinWidth = column.MinWidth;
            cell.Child = CreateCellContent(column, item);
        }
    }

    public void ApplyColumnWidths(IReadOnlyList<RepeaterDataGridColumn> columns)
    {
        for (var i = 0; i < _cells.Count && i < columns.Count; ++i)
        {
            var width = columns[i].ActualWidth;
            _cells[i].Width = double.IsNaN(width) || width <= 0 ? double.NaN : width;
            _cells[i].MinWidth = columns[i].MinWidth;
        }
    }

    public void ApplySelectionState(bool rowSelected, RepeaterDataGridCellInfo? selectedCell)
    {
        Background = rowSelected
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xD9, 0xEC, 0xFF))
            : new SolidColorBrush(Windows.UI.Color.FromArgb(0x00, 0x00, 0x00, 0x00));

        for (var i = 0; i < _cells.Count; ++i)
        {
            var isSelectedCell = selectedCell is { RowIndex: var row, ColumnIndex: var column } &&
                                 row == RowIndex &&
                                 column == i;

            _cells[i].Background = isSelectedCell
                ? new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x25, 0x63, 0xEB))
                : new SolidColorBrush(Windows.UI.Color.FromArgb(0x00, 0x00, 0x00, 0x00));

            if (_cells[i].Child is TextBlock textBlock)
                textBlock.Foreground = isSelectedCell
                    ? new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF))
                    : null;
        }
    }

    public double GetDesiredCellWidth(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= _cells.Count)
            return 0;

        var cell = _cells[columnIndex];
        return cell.ActualWidth > 0 ? cell.ActualWidth : cell.DesiredSize.Width;
    }

    private void EnsureCellCount(int count)
    {
        while (_cells.Count < count)
        {
            var cell = new Border
            {
                BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xD9, 0xD9, 0xD9)),
                BorderThickness = new Thickness(1, 0, 1, 1),
                Padding = new Thickness(8, 4, 8, 4),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x00, 0x00, 0x00, 0x00)),
            };
            _cells.Add(cell);
            _panel.Children.Add(cell);
        }

        while (_cells.Count > count)
        {
            var last = _cells[^1];
            _cells.RemoveAt(_cells.Count - 1);
            _panel.Children.Remove(last);
        }
    }

    private static FrameworkElement CreateCellContent(RepeaterDataGridColumn column, object? item)
    {
        if (column.CellTemplate is not null)
        {
            return new ContentControl
            {
                Content = item,
                ContentTemplate = column.CellTemplate,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        var textBlock = new TextBlock
        {
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            DataContext = item,
        };

        if (string.IsNullOrWhiteSpace(column.BindingPath))
        {
            textBlock.Text = item?.ToString() ?? string.Empty;
        }
        else
        {
            textBlock.SetBinding(
                TextBlock.TextProperty,
                new Binding
                {
                    Path = new PropertyPath(column.BindingPath),
                    Mode = BindingMode.OneWay,
                });
        }

        return textBlock;
    }
}
