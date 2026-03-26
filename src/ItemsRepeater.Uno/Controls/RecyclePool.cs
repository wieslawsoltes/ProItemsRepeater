using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Avalonia.Controls.Templates;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Avalonia.Controls
{
    public class RecyclePool
    {
        private static readonly ConditionalWeakTable<IDataTemplate, RecyclePool> Pools = new();
        private readonly Dictionary<string, List<ElementInfo>> _elements = new(StringComparer.Ordinal);
        private readonly ConditionalWeakTable<UIElement, ReuseKeyHolder> _reuseKeys = new();

        public static RecyclePool? GetPoolInstance(IDataTemplate dataTemplate)
        {
            Pools.TryGetValue(dataTemplate, out var pool);
            return pool;
        }

        public static void SetPoolInstance(IDataTemplate dataTemplate, RecyclePool value)
        {
            Pools.Remove(dataTemplate);
            Pools.Add(dataTemplate, value);
        }

        public void PutElement(UIElement element, string key, UIElement? owner)
        {
            var info = new ElementInfo(element, owner as Panel);
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

            var ownerPanel = owner as Panel;
            var index = bucket.FindIndex(x => ReferenceEquals(x.Owner, ownerPanel));
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

        internal string GetReuseKey(UIElement element)
        {
            return _reuseKeys.TryGetValue(element, out var holder) ? holder.Key : string.Empty;
        }

        internal void SetReuseKey(UIElement element, string value)
        {
            _reuseKeys.Remove(element);
            _reuseKeys.Add(element, new ReuseKeyHolder(value));
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

        private sealed class ReuseKeyHolder
        {
            public ReuseKeyHolder(string key)
            {
                Key = key;
            }

            public string Key { get; }
        }
    }
}
