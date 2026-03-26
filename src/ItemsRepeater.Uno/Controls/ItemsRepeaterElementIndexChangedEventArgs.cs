using System;
using Microsoft.UI.Xaml;

namespace Avalonia.Controls
{
    public class ItemsRepeaterElementIndexChangedEventArgs : EventArgs
    {
        internal ItemsRepeaterElementIndexChangedEventArgs(UIElement element, int oldIndex, int newIndex)
        {
            Element = element;
            OldIndex = oldIndex;
            NewIndex = newIndex;
        }

        public UIElement Element { get; private set; }
        public int NewIndex { get; private set; }
        public int OldIndex { get; private set; }

        internal void Update(UIElement element, int oldIndex, int newIndex)
        {
            Element = element;
            OldIndex = oldIndex;
            NewIndex = newIndex;
        }
    }
}
