using Microsoft.UI.Xaml;

namespace Avalonia.Controls.DataGrid;

public partial class RepeaterDataGridColumn : DependencyObject
{
    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(object), typeof(RepeaterDataGridColumn), new PropertyMetadata(null));

    public static readonly DependencyProperty WidthProperty =
        DependencyProperty.Register(nameof(Width), typeof(GridLength), typeof(RepeaterDataGridColumn), new PropertyMetadata(GridLength.Auto));

    public static readonly DependencyProperty MinWidthProperty =
        DependencyProperty.Register(nameof(MinWidth), typeof(double), typeof(RepeaterDataGridColumn), new PropertyMetadata(0d));

    public static readonly DependencyProperty BindingPathProperty =
        DependencyProperty.Register(nameof(BindingPath), typeof(string), typeof(RepeaterDataGridColumn), new PropertyMetadata(null));

    public static readonly DependencyProperty CellTemplateProperty =
        DependencyProperty.Register(nameof(CellTemplate), typeof(DataTemplate), typeof(RepeaterDataGridColumn), new PropertyMetadata(null));

    public static readonly DependencyProperty HeaderTemplateProperty =
        DependencyProperty.Register(nameof(HeaderTemplate), typeof(DataTemplate), typeof(RepeaterDataGridColumn), new PropertyMetadata(null));

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public GridLength Width
    {
        get => (GridLength)GetValue(WidthProperty);
        set => SetValue(WidthProperty, value);
    }

    public double MinWidth
    {
        get => (double)GetValue(MinWidthProperty);
        set => SetValue(MinWidthProperty, value);
    }

    public string? BindingPath
    {
        get => (string?)GetValue(BindingPathProperty);
        set => SetValue(BindingPathProperty, value);
    }

    public DataTemplate? CellTemplate
    {
        get => (DataTemplate?)GetValue(CellTemplateProperty);
        set => SetValue(CellTemplateProperty, value);
    }

    public DataTemplate? HeaderTemplate
    {
        get => (DataTemplate?)GetValue(HeaderTemplateProperty);
        set => SetValue(HeaderTemplateProperty, value);
    }

    public int Index { get; internal set; }

    public double ActualWidth { get; internal set; }
}
