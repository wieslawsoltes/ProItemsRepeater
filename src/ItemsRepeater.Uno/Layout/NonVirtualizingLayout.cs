namespace Avalonia.Layout
{
    public abstract class NonVirtualizingLayout : AttachedLayout
    {
        protected internal virtual void InitializeForContextCore(LayoutContext context)
        {
        }

        protected internal virtual void UninitializeForContextCore(LayoutContext context)
        {
        }

        protected internal abstract Size MeasureOverride(NonVirtualizingLayoutContext context, Size availableSize);

        protected internal virtual Size ArrangeOverride(NonVirtualizingLayoutContext context, Size finalSize) => finalSize;
    }
}
