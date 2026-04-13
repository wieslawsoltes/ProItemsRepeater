using System;

namespace Avalonia.Layout
{
    /// <summary>
    /// Defines constants that specify whether to suppress automatic recycling of the retrieved
    /// element or force creation of a new element.
    /// </summary>
    [Flags]
    public enum ElementRealizationOptions
    {
        None = 0x0,
        ForceCreate = 0x1,
        SuppressAutoRecycle = 0x2,
    }

    /// <summary>
    /// Represents the base class for layout context types that support virtualization.
    /// </summary>
    public abstract class VirtualizingLayoutContext : LayoutContext
    {
        private NonVirtualizingLayoutContext? _contextAdapter;

        public int ItemCount => ItemCountCore();

        public Point LayoutOrigin
        {
            get => LayoutOriginCore;
            set => LayoutOriginCore = value;
        }

        public Rect RealizationRect => RealizationRectCore();

        public int RecommendedAnchorIndex => RecommendedAnchorIndexCore;

        protected abstract Point LayoutOriginCore { get; set; }

        protected virtual int RecommendedAnchorIndexCore { get; }

        public object GetItemAt(int index) => GetItemAtCore(index);

        public Layoutable GetOrCreateElementAt(int index) =>
            GetOrCreateElementAtCore(index, ElementRealizationOptions.None);

        public Layoutable GetOrCreateElementAt(int index, ElementRealizationOptions options) =>
            GetOrCreateElementAtCore(index, options);

        public void RecycleElement(Layoutable element) => RecycleElementCore(element);

        protected abstract int ItemCountCore();

        protected abstract object GetItemAtCore(int index);

        protected abstract Rect RealizationRectCore();

        protected abstract Layoutable GetOrCreateElementAtCore(int index, ElementRealizationOptions options);

        protected abstract void RecycleElementCore(Layoutable element);

        internal NonVirtualizingLayoutContext GetNonVirtualizingContextAdapter() =>
            _contextAdapter ??= new VirtualLayoutContextAdapter(this);
    }

    internal sealed class UnoVirtualizingLayoutContext : VirtualizingLayoutContext
    {
        private readonly Microsoft.UI.Xaml.Controls.VirtualizingLayoutContext _inner;

        public UnoVirtualizingLayoutContext(Microsoft.UI.Xaml.Controls.VirtualizingLayoutContext inner)
        {
            _inner = inner;
        }

        protected override object? LayoutStateCore
        {
            get => _inner.LayoutState;
            set => _inner.LayoutState = value;
        }

        protected override Point LayoutOriginCore
        {
            get => _inner.LayoutOrigin.ToAvalonia();
            set => _inner.LayoutOrigin = value.ToNative();
        }

        protected override int RecommendedAnchorIndexCore => _inner.RecommendedAnchorIndex;

        protected override int ItemCountCore() => _inner.ItemCount;

        protected override object GetItemAtCore(int index) => _inner.GetItemAt(index);

        protected override Rect RealizationRectCore() => _inner.RealizationRect.ToAvalonia();

        protected override Layoutable GetOrCreateElementAtCore(int index, ElementRealizationOptions options) =>
            (Layoutable)_inner.GetOrCreateElementAt(index, (Microsoft.UI.Xaml.Controls.ElementRealizationOptions)(int)options);

        protected override void RecycleElementCore(Layoutable element) => _inner.RecycleElement(element);
    }
}
