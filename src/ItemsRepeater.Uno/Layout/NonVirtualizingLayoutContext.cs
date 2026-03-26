using System.Collections.Generic;
using Microsoft.UI.Xaml;

namespace Avalonia.Layout
{
    /// <summary>
    /// Represents the base class for layout context types that do not support virtualization.
    /// </summary>
    public abstract class NonVirtualizingLayoutContext : LayoutContext
    {
        private VirtualizingLayoutContext? _contextAdapter;

        /// <summary>
        /// Gets the collection of child controls from the container that provides the context.
        /// </summary>
        public IReadOnlyList<Layoutable> Children => ChildrenCore;

        /// <summary>
        /// Implements the behavior for getting the return value of <see cref="Children"/> in a
        /// derived or custom <see cref="NonVirtualizingLayoutContext"/>.
        /// </summary>
        protected abstract IReadOnlyList<Layoutable> ChildrenCore { get; }

        internal VirtualizingLayoutContext GetVirtualizingContextAdapter() =>
            _contextAdapter ??= new LayoutContextAdapter(this);
    }

    internal sealed class UnoNonVirtualizingLayoutContext : NonVirtualizingLayoutContext
    {
        private readonly Microsoft.UI.Xaml.Controls.NonVirtualizingLayoutContext _inner;
        private IReadOnlyList<Layoutable>? _children;

        public UnoNonVirtualizingLayoutContext(Microsoft.UI.Xaml.Controls.NonVirtualizingLayoutContext inner)
        {
            _inner = inner;
        }

        protected override object? LayoutStateCore
        {
            get => _inner.LayoutState;
            set => _inner.LayoutState = value;
        }

        protected override IReadOnlyList<Layoutable> ChildrenCore => _children ??= new ChildrenCollection(_inner.Children);

        private sealed class ChildrenCollection : IReadOnlyList<Layoutable>
        {
            private readonly IReadOnlyList<UIElement> _children;

            public ChildrenCollection(IReadOnlyList<UIElement> children)
            {
                _children = children;
            }

            public Layoutable this[int index] => (Layoutable)_children[index];

            public int Count => _children.Count;

            public IEnumerator<Layoutable> GetEnumerator()
            {
                for (var i = 0; i < _children.Count; ++i)
                    yield return (Layoutable)_children[i];
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
