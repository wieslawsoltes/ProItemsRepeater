using System;
using Microsoft.UI.Xaml;

namespace Avalonia.Controls
{
    public class ItemsRepeaterElementClearingEventArgs : EventArgs
    {
        internal ItemsRepeaterElementClearingEventArgs(UIElement element)
        {
            Element = element;
        }

        public UIElement Element { get; private set; }

        internal void Update(UIElement element)
        {
            Element = element;
        }
    }
}
