# Performance Tips

ItemsRepeater is designed for large collections, but template and layout choices still matter. Use the tips below to keep scrolling smooth and CPU usage low.

## Keep Templates Lightweight

- Prefer simple visual trees in item templates.
- Avoid expensive bindings or converters that run for every item.
- Use `RecyclingElementFactory` to reuse element instances.

## Use Virtualizing Layouts

- `StackLayout`, `WrapLayout`, and `UniformGridLayout` virtualize by default.
- Avoid `NonVirtualizingStackLayout` for large collections.
- Use `StackLayout.DisableVirtualization = true` only when required.

## Tune Cache Length

- Increase `HorizontalCacheLength` / `VerticalCacheLength` for smoother fast scrolling.
- Decrease cache length when memory usage is a concern.

```xml
<ItemsRepeater VerticalCacheLength="1.5" />
```

## Stabilize Item Sizes

Uniform item sizes reduce layout churn:

- Set `MinItemWidth` / `MinItemHeight` in `UniformGridLayout`.
- Avoid templates that change size during scrolling.

## Avoid Full Resets

Incremental `INotifyCollectionChanged` updates are cheaper than `Reset`. For large updates, batch changes when possible.
