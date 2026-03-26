using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;

namespace Avalonia.Controls
{
    public partial class RecyclePool
    {
        internal static readonly DependencyProperty ReuseKeyProperty =
            DependencyProperty.RegisterAttached(
                "ReuseKey",
                typeof(string),
                typeof(RecyclePool),
                new PropertyMetadata(string.Empty));

        internal static readonly DependencyProperty OriginTemplateProperty =
            DependencyProperty.RegisterAttached(
                "OriginTemplate",
                typeof(object),
                typeof(RecyclePool),
                new PropertyMetadata(null));

        public static readonly DependencyProperty PoolInstanceProperty =
            DependencyProperty.RegisterAttached(
                "PoolInstance",
                typeof(RecyclePool),
                typeof(RecyclePool),
                new PropertyMetadata(null));

        private readonly Dictionary<string, List<ElementInfo>> _elements = new(StringComparer.Ordinal);

        public static RecyclePool? GetPoolInstance(object dataTemplate) =>
            dataTemplate is DependencyObject dependencyObject
                ? (RecyclePool?)dependencyObject.GetValue(PoolInstanceProperty)
                : null;

        public static void SetPoolInstance(object dataTemplate, RecyclePool value)
        {
            if (dataTemplate is not DependencyObject dependencyObject)
                throw new InvalidOperationException("Pool instances can only be attached to dependency objects.");

            dependencyObject.SetValue(PoolInstanceProperty, value);
        }

        public void PutElement(UIElement element, string key, UIElement? owner)
        {
            var ownerPanel = EnsureOwnerIsPanelOrNull(owner);
            var info = new ElementInfo(element, ownerPanel);
            if (!_elements.TryGetValue(key, out var bucket))
            {
                bucket = new List<ElementInfo>();
                _elements.Add(key, bucket);
            }

            bucket.Add(info);
        }

        public UIElement? TryGetElement(string key, UIElement? owner)
        {
            if (!_elements.TryGetValue(key, out var bucket) || bucket.Count == 0)
                return null;

            var ownerPanel = EnsureOwnerIsPanelOrNull(owner);
            var index = bucket.FindIndex(x => ReferenceEquals(x.Owner, ownerPanel) || x.Owner is null);
            if (index < 0)
                index = bucket.Count - 1;

            var info = bucket[index];
            bucket.RemoveAt(index);

            if (info.Owner is Panel parent && info.Owner != ownerPanel)
            {
                parent.Children.Remove(info.Element);
            }

            return info.Element;
        }

        public void Clear() => _elements.Clear();

        internal string GetReuseKey(UIElement element) => (string)element.GetValue(ReuseKeyProperty);

        internal void SetReuseKey(UIElement element, string value) => element.SetValue(ReuseKeyProperty, value);

        private static Panel? EnsureOwnerIsPanelOrNull(UIElement? owner)
        {
            if (owner is null)
                return null;

            if (owner is Panel panel)
                return panel;

            throw new InvalidOperationException("Owner must be a Panel or null.");
        }

        private sealed class ElementInfo
        {
            public ElementInfo(UIElement element, Panel? owner)
            {
                Element = element;
                Owner = owner;
            }

            public UIElement Element { get; }
            public Panel? Owner { get; }
        }
    }
}
