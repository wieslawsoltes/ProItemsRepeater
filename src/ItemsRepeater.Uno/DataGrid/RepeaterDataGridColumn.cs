using System.ComponentModel;
using Microsoft.UI.Xaml;

namespace Avalonia.Controls.DataGrid;

public partial class RepeaterDataGridColumn : DependencyObject, INotifyPropertyChanged
{
    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(
            nameof(Header),
            typeof(object),
            typeof(RepeaterDataGridColumn),
            new PropertyMetadata(null, OnDependencyPropertyChanged));

    public static readonly DependencyProperty WidthProperty =
        DependencyProperty.Register(
            nameof(Width),
            typeof(GridLength),
            typeof(RepeaterDataGridColumn),
            new PropertyMetadata(GridLength.Auto, OnDependencyPropertyChanged));

    public static readonly DependencyProperty MinWidthProperty =
        DependencyProperty.Register(
            nameof(MinWidth),
            typeof(double),
            typeof(RepeaterDataGridColumn),
            new PropertyMetadata(0d, OnDependencyPropertyChanged));

    public static readonly DependencyProperty BindingPathProperty =
        DependencyProperty.Register(
            nameof(BindingPath),
            typeof(string),
            typeof(RepeaterDataGridColumn),
            new PropertyMetadata(null, OnDependencyPropertyChanged));

    public static readonly DependencyProperty CellTemplateProperty =
        DependencyProperty.Register(
            nameof(CellTemplate),
            typeof(DataTemplate),
            typeof(RepeaterDataGridColumn),
            new PropertyMetadata(null, OnDependencyPropertyChanged));

    public static readonly DependencyProperty HeaderTemplateProperty =
        DependencyProperty.Register(
            nameof(HeaderTemplate),
            typeof(DataTemplate),
            typeof(RepeaterDataGridColumn),
            new PropertyMetadata(null, OnDependencyPropertyChanged));

    private int _index;
    private double _actualWidth;

    public event PropertyChangedEventHandler? PropertyChanged;

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

    public int Index
    {
        get => _index;
        internal set
        {
            if (_index == value)
                return;

            _index = value;
            RaisePropertyChanged(nameof(Index));
        }
    }

    public double ActualWidth
    {
        get => _actualWidth;
        internal set
        {
            if (_actualWidth.Equals(value))
                return;

            _actualWidth = value;
            RaisePropertyChanged(nameof(ActualWidth));
        }
    }

    private static void OnDependencyPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not RepeaterDataGridColumn column || args.Property is null)
            return;

        var propertyName =
            ReferenceEquals(args.Property, HeaderProperty) ? nameof(Header) :
            ReferenceEquals(args.Property, WidthProperty) ? nameof(Width) :
            ReferenceEquals(args.Property, MinWidthProperty) ? nameof(MinWidth) :
            ReferenceEquals(args.Property, BindingPathProperty) ? nameof(BindingPath) :
            ReferenceEquals(args.Property, CellTemplateProperty) ? nameof(CellTemplate) :
            ReferenceEquals(args.Property, HeaderTemplateProperty) ? nameof(HeaderTemplate) :
            null;

        if (propertyName is not null)
            column.RaisePropertyChanged(propertyName);
    }

    private void RaisePropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
