using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRect = Windows.Foundation.Rect;

namespace Avalonia.Layout;

public class NonVirtualizingStackLayout : Avalonia.Layout.NonVirtualizingLayout
{
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation),
            typeof(Orientation),
            typeof(NonVirtualizingStackLayout),
            new PropertyMetadata(Orientation.Vertical, OnLayoutPropertyChanged));

    public static readonly DependencyProperty SpacingProperty =
        DependencyProperty.Register(
            nameof(Spacing),
            typeof(double),
            typeof(NonVirtualizingStackLayout),
            new PropertyMetadata(0d, OnLayoutPropertyChanged));

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    protected internal override Avalonia.Size MeasureOverride(Avalonia.Layout.NonVirtualizingLayoutContext context, Avalonia.Size availableSize)
    {
        var isVertical = Orientation == Orientation.Vertical;
        var extentU = 0d;
        var extentV = 0d;
        var visibleCount = 0;
        var constraint = isVertical
            ? new Avalonia.Size(availableSize.Width, double.PositiveInfinity)
            : new Avalonia.Size(double.PositiveInfinity, availableSize.Height);

        foreach (var element in context.Children)
        {
            if (element.Visibility == Visibility.Collapsed)
                continue;

            if (visibleCount > 0)
                extentU += Spacing;

            element.Measure(constraint.ToNative());

            if (isVertical)
            {
                extentU += element.DesiredSize.Height;
                extentV = Math.Max(extentV, element.DesiredSize.Width);
            }
            else
            {
                extentU += element.DesiredSize.Width;
                extentV = Math.Max(extentV, element.DesiredSize.Height);
            }

            ++visibleCount;
        }

        return isVertical ? new Avalonia.Size(extentV, extentU) : new Avalonia.Size(extentU, extentV);
    }

    protected internal override Avalonia.Size ArrangeOverride(Avalonia.Layout.NonVirtualizingLayoutContext context, Avalonia.Size finalSize)
    {
        var isVertical = Orientation == Orientation.Vertical;
        var u = 0d;
        var lastBounds = new WinRect();

        foreach (var element in context.Children)
        {
            if (element.Visibility == Visibility.Collapsed)
                continue;

            lastBounds = isVertical
                ? ArrangeVertical(element, u, finalSize)
                : ArrangeHorizontal(element, u, finalSize);

            element.Arrange(lastBounds);
            u = (isVertical ? lastBounds.Bottom : lastBounds.Right) + Spacing;
        }

        return new Avalonia.Size(
            Math.Max(finalSize.Width, lastBounds.Width),
            Math.Max(finalSize.Height, lastBounds.Height));
    }

    private static void OnLayoutPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        _ = sender;
        _ = args;
    }

    private static WinRect ArrangeVertical(UIElement element, double y, Avalonia.Size constraint)
    {
        var width = element.DesiredSize.Width;
        var x = 0d;

        if (element is FrameworkElement frameworkElement)
        {
            switch (frameworkElement.HorizontalAlignment)
            {
                case HorizontalAlignment.Center:
                    x += (constraint.Width - width) / 2;
                    break;
                case HorizontalAlignment.Right:
                    x += constraint.Width - width;
                    break;
                case HorizontalAlignment.Stretch:
                    width = constraint.Width;
                    break;
            }
        }

        return new WinRect(x, y, width, element.DesiredSize.Height);
    }

    private static WinRect ArrangeHorizontal(UIElement element, double x, Avalonia.Size constraint)
    {
        var height = element.DesiredSize.Height;
        var y = 0d;

        if (element is FrameworkElement frameworkElement)
        {
            switch (frameworkElement.VerticalAlignment)
            {
                case VerticalAlignment.Center:
                    y += (constraint.Height - height) / 2;
                    break;
                case VerticalAlignment.Bottom:
                    y += constraint.Height - height;
                    break;
                case VerticalAlignment.Stretch:
                    height = constraint.Height;
                    break;
            }
        }

        return new WinRect(x, y, element.DesiredSize.Width, height);
    }
}
