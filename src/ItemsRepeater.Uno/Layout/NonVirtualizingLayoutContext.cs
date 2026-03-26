using System.Collections.Generic;
using Microsoft.UI.Xaml;

namespace Avalonia.Layout
{
    public class NonVirtualizingLayoutContext : LayoutContext
    {
        internal NonVirtualizingLayoutContext(Microsoft.UI.Xaml.Controls.NonVirtualizingLayoutContext inner)
            : base(inner)
        {
            Inner = inner;
        }

        internal new Microsoft.UI.Xaml.Controls.NonVirtualizingLayoutContext Inner { get; }

        public IReadOnlyList<UIElement> Children => Inner.Children;
    }
}
