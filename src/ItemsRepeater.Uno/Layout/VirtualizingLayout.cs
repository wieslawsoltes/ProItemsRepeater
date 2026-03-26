using System.Collections.Specialized;

namespace Avalonia.Layout
{
    public abstract class VirtualizingLayout : Microsoft.UI.Xaml.Controls.VirtualizingLayout
    {
        public string? LayoutId { get; set; }

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

        protected sealed override void InitializeForContextCore(Microsoft.UI.Xaml.Controls.VirtualizingLayoutContext context)
        {
            InitializeForContextCore(new UnoVirtualizingLayoutContext(context));
        }

        protected sealed override void UninitializeForContextCore(Microsoft.UI.Xaml.Controls.VirtualizingLayoutContext context)
        {
            UninitializeForContextCore(new UnoVirtualizingLayoutContext(context));
        }

        protected sealed override Windows.Foundation.Size MeasureOverride(Microsoft.UI.Xaml.Controls.VirtualizingLayoutContext context, Windows.Foundation.Size availableSize)
        {
            return MeasureOverride(new UnoVirtualizingLayoutContext(context), availableSize.ToAvalonia()).ToNative();
        }

        protected sealed override Windows.Foundation.Size ArrangeOverride(Microsoft.UI.Xaml.Controls.VirtualizingLayoutContext context, Windows.Foundation.Size finalSize)
        {
            return ArrangeOverride(new UnoVirtualizingLayoutContext(context), finalSize.ToAvalonia()).ToNative();
        }

        protected sealed override void OnItemsChangedCore(Microsoft.UI.Xaml.Controls.VirtualizingLayoutContext context, object source, NotifyCollectionChangedEventArgs args)
        {
            OnItemsChangedCore(new UnoVirtualizingLayoutContext(context), source, args);
        }
    }
}
