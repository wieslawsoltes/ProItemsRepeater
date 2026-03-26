using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls.Templates;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;

namespace Avalonia.Controls
{
    public class SelectTemplateEventArgs : EventArgs
    {
        public string? TemplateKey { get; set; }
        public object? DataContext { get; internal set; }
        public UIElement? Owner { get; internal set; }
    }

    [ContentProperty(Name = nameof(Templates))]
    public class RecyclingElementFactory : ElementFactory
    {
        private RecyclePool? _recyclePool;
        private IDictionary<string, IDataTemplate>? _templates;
        private SelectTemplateEventArgs? _selectTemplateArgs;

        public RecyclingElementFactory()
        {
            Templates = new Dictionary<string, IDataTemplate>(StringComparer.Ordinal);
        }

        public RecyclePool RecyclePool
        {
            get => _recyclePool ??= new RecyclePool();
            set => _recyclePool = value ?? throw new ArgumentNullException(nameof(value));
        }

        public IDictionary<string, IDataTemplate> Templates
        {
            get => _templates ??= new Dictionary<string, IDataTemplate>(StringComparer.Ordinal);
            set => _templates = value ?? throw new ArgumentNullException(nameof(value));
        }

        public event EventHandler<SelectTemplateEventArgs>? SelectTemplateKey;

        protected override UIElement GetElementCore(ElementFactoryGetArgs args)
        {
            if (Templates.Count == 0)
                throw new InvalidOperationException("Templates cannot be empty.");

            var templateKey = Templates.Count == 1
                ? Templates.First().Key
                : OnSelectTemplateKeyCore(args.Data, args.Parent);

            if (string.IsNullOrWhiteSpace(templateKey))
                throw new InvalidOperationException("Template key cannot be null or empty.");

            var element = RecyclePool.TryGetElement(templateKey, args.Parent);
            if (element is null)
            {
                if (!Templates.TryGetValue(templateKey, out var template))
                    throw new InvalidOperationException($"No templates of key '{templateKey}' were found in the templates collection.");

                element = template.Build(args.Data) ?? throw new InvalidOperationException("Templates must build a non-null UIElement.");
                AssignDataContext(element, args.Data);
                RecyclePool.SetReuseKey(element, templateKey);
            }
            else
            {
                AssignDataContext(element, args.Data);
            }

            return element;
        }

        protected override void RecycleElementCore(ElementFactoryRecycleArgs args)
        {
            if (args.Element is null)
                return;

            var key = RecyclePool.GetReuseKey(args.Element);
            RecyclePool.PutElement(args.Element, key, args.Parent);
        }

        protected virtual string OnSelectTemplateKeyCore(object? dataContext, UIElement? owner)
        {
            if (SelectTemplateKey is not null)
            {
                _selectTemplateArgs ??= new SelectTemplateEventArgs();
                _selectTemplateArgs.TemplateKey = null;
                _selectTemplateArgs.DataContext = dataContext;
                _selectTemplateArgs.Owner = owner;
                SelectTemplateKey(this, _selectTemplateArgs);
            }

            if (string.IsNullOrWhiteSpace(_selectTemplateArgs?.TemplateKey))
                throw new InvalidOperationException("Please provide a valid template identifier in the handler for the SelectTemplateKey event.");

            return _selectTemplateArgs.TemplateKey!;
        }

        private static void AssignDataContext(UIElement element, object? data)
        {
            if (element is FrameworkElement frameworkElement)
                frameworkElement.DataContext = data;
        }
    }
}
