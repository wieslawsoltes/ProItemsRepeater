using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Avalonia.Controls.DataGrid;

public class RepeaterDataGridColumn : AvaloniaObject
{
    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<RepeaterDataGridColumn, object?>(nameof(Header));

    public static readonly StyledProperty<GridLength> WidthProperty =
        AvaloniaProperty.Register<RepeaterDataGridColumn, GridLength>(nameof(Width), GridLength.Auto);

    public static readonly StyledProperty<double> MinWidthProperty =
        AvaloniaProperty.Register<RepeaterDataGridColumn, double>(nameof(MinWidth), 0);

    public static readonly DirectProperty<RepeaterDataGridColumn, int> IndexProperty =
        AvaloniaProperty.RegisterDirect<RepeaterDataGridColumn, int>(
            nameof(Index),
            o => o.Index,
            (o, v) => o.Index = v);

    public static readonly DirectProperty<RepeaterDataGridColumn, double> ActualWidthProperty =
        AvaloniaProperty.RegisterDirect<RepeaterDataGridColumn, double>(
            nameof(ActualWidth),
            o => o.ActualWidth,
            (o, v) => o.ActualWidth = v);

    public static readonly StyledProperty<string?> BindingPathProperty =
        AvaloniaProperty.Register<RepeaterDataGridColumn, string?>(nameof(BindingPath));

    public static readonly StyledProperty<IDataTemplate?> CellTemplateProperty =
        AvaloniaProperty.Register<RepeaterDataGridColumn, IDataTemplate?>(nameof(CellTemplate));

    public static readonly StyledProperty<IDataTemplate?> HeaderTemplateProperty =
        AvaloniaProperty.Register<RepeaterDataGridColumn, IDataTemplate?>(nameof(HeaderTemplate));

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public GridLength Width
    {
        get => GetValue(WidthProperty);
        set => SetValue(WidthProperty, value);
    }

    public double MinWidth
    {
        get => GetValue(MinWidthProperty);
        set => SetValue(MinWidthProperty, value);
    }

    private int _index;
    public int Index
    {
        get => _index;
        internal set => SetAndRaise(IndexProperty, ref _index, value);
    }

    private double _actualWidth;
    public double ActualWidth
    {
        get => _actualWidth;
        internal set => SetAndRaise(ActualWidthProperty, ref _actualWidth, value);
    }

    public string? BindingPath
    {
        get => GetValue(BindingPathProperty);
        set => SetValue(BindingPathProperty, value);
    }

    public IDataTemplate? CellTemplate
    {
        get => GetValue(CellTemplateProperty);
        set => SetValue(CellTemplateProperty, value);
    }

    public IDataTemplate? HeaderTemplate
    {
        get => GetValue(HeaderTemplateProperty);
        set => SetValue(HeaderTemplateProperty, value);
    }
}
