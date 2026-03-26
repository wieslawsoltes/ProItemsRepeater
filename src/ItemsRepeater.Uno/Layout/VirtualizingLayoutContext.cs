using System;
using Microsoft.UI.Xaml;

namespace Avalonia.Layout
{
    [Flags]
    public enum ElementRealizationOptions
    {
        None = 0x0,
        ForceCreate = 0x1,
        SuppressAutoRecycle = 0x2,
    }

    public class VirtualizingLayoutContext : LayoutContext
    {
        internal VirtualizingLayoutContext(Microsoft.UI.Xaml.Controls.VirtualizingLayoutContext inner)
            : base(inner)
        {
            Inner = inner;
        }

        internal new Microsoft.UI.Xaml.Controls.VirtualizingLayoutContext Inner { get; }

        public int ItemCount => Inner.ItemCount;

        public Point LayoutOrigin
        {
            get => Inner.LayoutOrigin.ToAvalonia();
            set => Inner.LayoutOrigin = value.ToNative();
        }

        public Rect RealizationRect => Inner.RealizationRect.ToAvalonia();

        public int RecommendedAnchorIndex => Inner.RecommendedAnchorIndex;

        public object GetItemAt(int index) => Inner.GetItemAt(index);

        public UIElement GetOrCreateElementAt(int index) => Inner.GetOrCreateElementAt(index);

        public UIElement GetOrCreateElementAt(int index, ElementRealizationOptions options) =>
            Inner.GetOrCreateElementAt(index, (Microsoft.UI.Xaml.Controls.ElementRealizationOptions)(int)options);

        public void RecycleElement(UIElement element) => Inner.RecycleElement(element);
    }
}
