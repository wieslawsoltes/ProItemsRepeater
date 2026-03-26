using System;
using Avalonia.Controls.Templates;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;

namespace Avalonia.Controls;

[ContentProperty(Name = nameof(ItemTemplate))]
public class ItemsRepeater : Microsoft.UI.Xaml.Controls.ItemsRepeater
{
    private ItemsRepeaterElementPreparedEventArgs? _preparedArgs;
    private ItemsRepeaterElementClearingEventArgs? _clearingArgs;
    private ItemsRepeaterElementIndexChangedEventArgs? _indexChangedArgs;
    private object? _itemTemplate;

    public ItemsRepeater()
    {
        base.ElementPrepared += OnNativeElementPrepared;
        base.ElementClearing += OnNativeElementClearing;
        base.ElementIndexChanged += OnNativeElementIndexChanged;
    }

    public new object? ItemTemplate
    {
        get => _itemTemplate;
        set
        {
            _itemTemplate = value;
            base.ItemTemplate = NormalizeItemTemplate(value);
        }
    }

    public new event EventHandler<ItemsRepeaterElementPreparedEventArgs>? ElementPrepared;
    public new event EventHandler<ItemsRepeaterElementClearingEventArgs>? ElementClearing;
    public new event EventHandler<ItemsRepeaterElementIndexChangedEventArgs>? ElementIndexChanged;

    public new UIElement? TryGetElement(int index) => base.TryGetElement(index);

    public new UIElement GetOrCreateElement(int index) => base.GetOrCreateElement(index);

    public new int GetElementIndex(UIElement element) => base.GetElementIndex(element);

    private void OnNativeElementPrepared(Microsoft.UI.Xaml.Controls.ItemsRepeater sender, Microsoft.UI.Xaml.Controls.ItemsRepeaterElementPreparedEventArgs args)
    {
        _preparedArgs ??= new ItemsRepeaterElementPreparedEventArgs(args.Element, args.Index);
        _preparedArgs.Update(args.Element, args.Index);
        ElementPrepared?.Invoke(this, _preparedArgs);
    }

    private void OnNativeElementClearing(Microsoft.UI.Xaml.Controls.ItemsRepeater sender, Microsoft.UI.Xaml.Controls.ItemsRepeaterElementClearingEventArgs args)
    {
        _clearingArgs ??= new ItemsRepeaterElementClearingEventArgs(args.Element);
        _clearingArgs.Update(args.Element);
        ElementClearing?.Invoke(this, _clearingArgs);
    }

    private void OnNativeElementIndexChanged(Microsoft.UI.Xaml.Controls.ItemsRepeater sender, Microsoft.UI.Xaml.Controls.ItemsRepeaterElementIndexChangedEventArgs args)
    {
        _indexChangedArgs ??= new ItemsRepeaterElementIndexChangedEventArgs(args.Element, args.OldIndex, args.NewIndex);
        _indexChangedArgs.Update(args.Element, args.OldIndex, args.NewIndex);
        ElementIndexChanged?.Invoke(this, _indexChangedArgs);
    }

    private static object? NormalizeItemTemplate(object? value)
    {
        return value switch
        {
            null => null,
            IDataTemplate template => new TemplateElementFactory(template),
            _ => value,
        };
    }

    private sealed class TemplateElementFactory : ElementFactory
    {
        private readonly IDataTemplate _template;

        public TemplateElementFactory(IDataTemplate template)
        {
            _template = template;
        }

        protected override UIElement GetElementCore(ElementFactoryGetArgs args)
        {
            var element = _template.Build(args.Data) ?? throw new InvalidOperationException("IDataTemplate.Build returned null.");
            if (element is FrameworkElement frameworkElement)
                frameworkElement.DataContext = args.Data;
            return element;
        }

        protected override void RecycleElementCore(ElementFactoryRecycleArgs args)
        {
            _ = args;
        }
    }
}
