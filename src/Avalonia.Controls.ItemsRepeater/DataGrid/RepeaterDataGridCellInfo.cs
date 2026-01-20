using System;

namespace Avalonia.Controls.DataGrid;

public readonly struct RepeaterDataGridCellInfo : IEquatable<RepeaterDataGridCellInfo>
{
    public RepeaterDataGridCellInfo(int rowIndex, int columnIndex)
    {
        RowIndex = rowIndex;
        ColumnIndex = columnIndex;
    }

    public int RowIndex { get; }
    public int ColumnIndex { get; }

    public bool Equals(RepeaterDataGridCellInfo other) =>
        RowIndex == other.RowIndex && ColumnIndex == other.ColumnIndex;

    public override bool Equals(object? obj) =>
        obj is RepeaterDataGridCellInfo other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(RowIndex, ColumnIndex);

    public static bool operator ==(RepeaterDataGridCellInfo left, RepeaterDataGridCellInfo right) => left.Equals(right);

    public static bool operator !=(RepeaterDataGridCellInfo left, RepeaterDataGridCellInfo right) => !left.Equals(right);
}
