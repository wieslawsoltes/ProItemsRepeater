using Microsoft.UI.Xaml;

namespace Avalonia.Controls
{
    public class ItemsRepeaterElementPreparedEventArgs
    {
        internal ItemsRepeaterElementPreparedEventArgs(UIElement element, int index)
        {
            Element = element;
            Index = index;
        }

        public UIElement Element { get; private set; }
        public int Index { get; private set; }

        internal void Update(UIElement element, int index)
        {
            Element = element;
            Index = index;
        }
    }
}
