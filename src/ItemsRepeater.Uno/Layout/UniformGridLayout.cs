namespace Avalonia.Layout;

public class UniformGridLayout : Microsoft.UI.Xaml.Controls.UniformGridLayout
{
    public new UniformGridLayoutItemsJustification ItemsJustification
    {
        get => (UniformGridLayoutItemsJustification)(int)base.ItemsJustification;
        set => base.ItemsJustification = (Microsoft.UI.Xaml.Controls.UniformGridLayoutItemsJustification)(int)value;
    }

    public new UniformGridLayoutItemsStretch ItemsStretch
    {
        get => (UniformGridLayoutItemsStretch)(int)base.ItemsStretch;
        set => base.ItemsStretch = (Microsoft.UI.Xaml.Controls.UniformGridLayoutItemsStretch)(int)value;
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
}
