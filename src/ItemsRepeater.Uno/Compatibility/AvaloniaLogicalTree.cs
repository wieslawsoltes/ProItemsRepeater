using System;
using Microsoft.UI.Xaml;

namespace Avalonia.LogicalTree
{
    public sealed class ChildIndexChangedEventArgs : EventArgs
    {
        public ChildIndexChangedEventArgs(UIElement child, int index)
        {
            Child = child;
            Index = index;
        }

        public UIElement Child { get; }

        public int Index { get; }
    }

    public interface IChildIndexProvider
    {
        event EventHandler<ChildIndexChangedEventArgs>? ChildIndexChanged;

        int GetChildIndex(UIElement child);

        bool TryGetTotalCount(out int count);
    }
}
