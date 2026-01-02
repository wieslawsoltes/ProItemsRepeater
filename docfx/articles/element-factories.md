# Element Factories (IElementFactory)

This article explains how to use `IElementFactory` to control element creation and recycling in `ItemsRepeater`.

## What is an element factory

`IElementFactory` is a template that can both create and recycle elements. It extends `IDataTemplate` and adds:

- `GetElement(ElementFactoryGetArgs args)`
- `RecycleElement(ElementFactoryRecycleArgs args)`

`ItemsRepeater` calls these methods when `ItemTemplate` implements `IElementFactory`.

## ElementFactory arguments

`ElementFactoryGetArgs` provides:

- `Data`: the data item for the index.
- `Parent`: the `ItemsRepeater` that will host the element.
- `Index`: the item index.

`ElementFactoryRecycleArgs` provides:

- `Element`: the element being recycled.
- `Parent`: the `ItemsRepeater` that owned it.

## Minimal implementation

```csharp
public sealed class SimpleElementFactory : IElementFactory
{
    public bool Match(object? data) => true;

    public Control Build(object? data)
    {
        return new TextBlock();
    }

    public Control GetElement(ElementFactoryGetArgs args)
    {
        var element = new TextBlock();
        element.Text = args.Data?.ToString();
        return element;
    }

    public void RecycleElement(ElementFactoryRecycleArgs args)
    {
        // Reset state here if needed.
    }
}
```

`IDataTemplate.Match` and `Build` are still required because `IElementFactory` derives from `IDataTemplate`, but `ItemsRepeater` uses `GetElement` / `RecycleElement` for element lifetime.

## DataContext behavior

After `GetElement` returns an element:

- If the element is not the data item itself, `ItemsRepeater` sets `DataContext` to the data item.
- If it is the data item, DataContext is left unchanged.

On recycle, `ItemsRepeater` clears DataContext only if it set it.

## When to use IElementFactory

Use a custom element factory when you need:

- Fine control over reuse.
- Template selection based on index or data type.
- Direct access to parent repeater during creation.

For key-based reuse, consider using `RecyclingElementFactory` and `RecyclePool`.

## Related docs

- `templates-and-recycling.md`
- `item-template-resolution.md`
