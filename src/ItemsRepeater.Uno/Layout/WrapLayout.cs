using Microsoft.UI.Xaml;

namespace Avalonia.Layout;

public class WrapLayout : Microsoft.UI.Xaml.Controls.FlowLayout
{
    public static readonly DependencyProperty HorizontalSpacingProperty =
        DependencyProperty.Register(
            nameof(HorizontalSpacing),
            typeof(double),
            typeof(WrapLayout),
            new PropertyMetadata(0d, OnHorizontalSpacingChanged));

    public static readonly DependencyProperty VerticalSpacingProperty =
        DependencyProperty.Register(
            nameof(VerticalSpacing),
            typeof(double),
            typeof(WrapLayout),
            new PropertyMetadata(0d, OnVerticalSpacingChanged));

    public double HorizontalSpacing
    {
        get => (double)GetValue(HorizontalSpacingProperty);
        set => SetValue(HorizontalSpacingProperty, value);
    }

    public double VerticalSpacing
    {
        get => (double)GetValue(VerticalSpacingProperty);
        set => SetValue(VerticalSpacingProperty, value);
    }

    public new Orientation Orientation
    {
        get => base.Orientation == Microsoft.UI.Xaml.Controls.Orientation.Horizontal
            ? Orientation.Horizontal
            : Orientation.Vertical;
        set => base.Orientation = value == Orientation.Horizontal
            ? Microsoft.UI.Xaml.Controls.Orientation.Horizontal
            : Microsoft.UI.Xaml.Controls.Orientation.Vertical;
    }

    private static void OnHorizontalSpacingChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        ((WrapLayout)dependencyObject).MinColumnSpacing = (double)args.NewValue;
    }

    private static void OnVerticalSpacingChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        ((WrapLayout)dependencyObject).MinRowSpacing = (double)args.NewValue;
    }
}
