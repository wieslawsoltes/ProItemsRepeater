namespace Avalonia.Layout
{
    public abstract class NonVirtualizingLayout : Microsoft.UI.Xaml.Controls.NonVirtualizingLayout
    {
        protected internal virtual void InitializeForContextCore(LayoutContext context)
        {
        }

        protected internal virtual void UninitializeForContextCore(LayoutContext context)
        {
        }

        protected internal abstract Size MeasureOverride(NonVirtualizingLayoutContext context, Size availableSize);

        protected internal virtual Size ArrangeOverride(NonVirtualizingLayoutContext context, Size finalSize) => finalSize;

        protected sealed override void InitializeForContextCore(Microsoft.UI.Xaml.Controls.LayoutContext context)
        {
            InitializeForContextCore(new LayoutContext(context));
        }

        protected sealed override void UninitializeForContextCore(Microsoft.UI.Xaml.Controls.LayoutContext context)
        {
            UninitializeForContextCore(new LayoutContext(context));
        }

        protected sealed override Windows.Foundation.Size MeasureOverride(Microsoft.UI.Xaml.Controls.NonVirtualizingLayoutContext context, Windows.Foundation.Size availableSize)
        {
            return MeasureOverride(new NonVirtualizingLayoutContext(context), availableSize.ToAvalonia()).ToNative();
        }

        protected sealed override Windows.Foundation.Size ArrangeOverride(Microsoft.UI.Xaml.Controls.NonVirtualizingLayoutContext context, Windows.Foundation.Size finalSize)
        {
            return ArrangeOverride(new NonVirtualizingLayoutContext(context), finalSize.ToAvalonia()).ToNative();
        }
    }
}
