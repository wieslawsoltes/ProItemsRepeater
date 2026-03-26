namespace Avalonia.Layout;

public class StackLayout : Microsoft.UI.Xaml.Controls.StackLayout
{
    public new Orientation Orientation
    {
        get => ToAvalonia(base.Orientation);
        set => base.Orientation = ToNative(value);
    }

    private static Orientation ToAvalonia(Microsoft.UI.Xaml.Controls.Orientation value) =>
        value == Microsoft.UI.Xaml.Controls.Orientation.Horizontal
            ? Orientation.Horizontal
            : Orientation.Vertical;

    private static Microsoft.UI.Xaml.Controls.Orientation ToNative(Orientation value) =>
        value == Orientation.Horizontal
            ? Microsoft.UI.Xaml.Controls.Orientation.Horizontal
            : Microsoft.UI.Xaml.Controls.Orientation.Vertical;
}
