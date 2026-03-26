using Avalonia.Layout;

namespace Avalonia.Controls
{
    internal abstract class RepeaterLayoutContext : VirtualizingLayoutContext
    {
        internal virtual bool HasMadeAnchor => false;
    }
}
