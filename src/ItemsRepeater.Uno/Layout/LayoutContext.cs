namespace Avalonia.Layout
{
    public class LayoutContext
    {
        internal LayoutContext(Microsoft.UI.Xaml.Controls.LayoutContext inner)
        {
            Inner = inner;
        }

        internal Microsoft.UI.Xaml.Controls.LayoutContext Inner { get; }

        public object? LayoutState
        {
            get => Inner.LayoutState;
            set => Inner.LayoutState = value;
        }
    }
}
