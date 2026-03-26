using System.Collections.Specialized;

namespace Avalonia.Layout
{
    public abstract class VirtualizingLayout : AttachedLayout
    {
        protected internal virtual void InitializeForContextCore(VirtualizingLayoutContext context)
        {
        }

        protected internal virtual void UninitializeForContextCore(VirtualizingLayoutContext context)
        {
        }

        protected internal abstract Size MeasureOverride(VirtualizingLayoutContext context, Size availableSize);

        protected internal virtual Size ArrangeOverride(VirtualizingLayoutContext context, Size finalSize) => finalSize;

        protected internal virtual void OnItemsChangedCore(VirtualizingLayoutContext context, object? source, NotifyCollectionChangedEventArgs args)
        {
            _ = source;
            _ = args;
            InvalidateMeasure();
        }
    }
}
