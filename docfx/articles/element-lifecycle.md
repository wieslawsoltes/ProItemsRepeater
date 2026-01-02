# Element Lifecycle and Events

ItemsRepeater exposes events to track realized elements and their indices. These are useful for attaching behaviors or maintaining per-element state.

## Lifecycle Events

- `ElementPrepared`: raised when an element is created or recycled for an item.
- `ElementClearing`: raised when an element is about to be recycled.
- `ElementIndexChanged`: raised when a realized element's item index changes.

```csharp
repeater.ElementPrepared += (_, e) =>
{
    e.Element.Classes.Add("realized");
};

repeater.ElementClearing += (_, e) =>
{
    e.Element.Classes.Remove("realized");
};

repeater.ElementIndexChanged += (_, e) =>
{
    e.Element.Tag = e.NewIndex;
};
```

## Element Lookup Helpers

- `GetElementIndex(element)`: returns the current index for a realized element.
- `TryGetElement(index)`: returns the realized element at an index, or `null`.
- `GetOrCreateElement(index)`: realizes the element (if needed) and returns it.

```csharp
var element = repeater.TryGetElement(10);
if (element == null)
{
    element = repeater.GetOrCreateElement(10);
}
```

Use these APIs when you need to programmatically manipulate or scroll to specific items.
