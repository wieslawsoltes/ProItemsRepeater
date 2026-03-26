// This source file is adapted from the WinUI project.
// (https://github.com/microsoft/microsoft-ui-xaml)
//
// Licensed to The Avalonia Project under MIT License, courtesy of The .NET Foundation.

using System;
using Avalonia.Controls.Templates;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;

namespace Avalonia.Controls
{
    internal sealed class ItemTemplateWrapper : IElementFactory
    {
        private readonly object _template;

        public ItemTemplateWrapper(IDataTemplate dataTemplate) => _template = dataTemplate;

        public ItemTemplateWrapper(DataTemplate dataTemplate) => _template = dataTemplate;

        public UIElement Build(object? param) => GetElement(null, param);
        public bool Match(object? data) => _template is IDataTemplate dataTemplate ? dataTemplate.Match(data) : true;

        public UIElement GetElement(ElementFactoryGetArgs args)
        {
            return GetElement(args.Parent, args.Data);
        }

        public void RecycleElement(ElementFactoryRecycleArgs args)
        {
            RecycleElement(args.Parent, args.Element!);
        }

        private UIElement GetElement(UIElement? parent, object? data)
        {
            var selectedTemplate = _template;
            var recyclePool = RecyclePool.GetPoolInstance(selectedTemplate);
            UIElement? element = null;

            if (recyclePool != null)
            {
                // try to get an element from the recycle pool.
                element = recyclePool.TryGetElement(string.Empty, parent);
            }

            if (element == null)
            {
                // no element was found in recycle pool, create a new element
                element = selectedTemplate switch
                {
                    IDataTemplate dataTemplate => dataTemplate.Build(data),
                    DataTemplate nativeTemplate => nativeTemplate.LoadContent() as UIElement,
                    _ => throw new InvalidOperationException("Unsupported item template.")
                };

                if (element is null)
                {
                    element = new Rectangle
                    {
                        Width = 0,
                        Height = 0,
                    };
                }

                // Associate template with element
                element.SetValue(RecyclePool.OriginTemplateProperty, selectedTemplate);
            }

            return element;
        }

        private void RecycleElement(UIElement? parent, UIElement element)
        {
            var selectedTemplate = _template is DataTemplate ? element.GetValue(RecyclePool.OriginTemplateProperty) ?? _template : _template;
            var recyclePool = RecyclePool.GetPoolInstance(selectedTemplate);
            if (recyclePool == null)
            {
                // No Recycle pool in the template, create one.
                recyclePool = new RecyclePool();
                RecyclePool.SetPoolInstance(selectedTemplate, recyclePool);
            }

            recyclePool.PutElement(element, "" /* key */, parent);
        }
    }
}
