# Layouts

ItemsRepeater delegates measurement and arrangement to a layout. Layout choice controls virtualization, scrolling direction, and how items are positioned.

## StackLayout (virtualizing)

Arranges items in a single line with spacing.

- `Orientation`: `Vertical` (default) or `Horizontal`.
- `Spacing`: space between items along the orientation axis.
- `DisableVirtualization`: set to `true` only when you need all items realized.

```xml
<ItemsRepeater.Layout>
  <StackLayout Orientation="Vertical" Spacing="6" />
</ItemsRepeater.Layout>
```

## WrapLayout (virtualizing)

Wraps items into rows or columns based on available space.

- `Orientation`: `Horizontal` (rows) or `Vertical` (columns).
- `HorizontalSpacing` and `VerticalSpacing`: spacing between items.

```xml
<ItemsRepeater.Layout>
  <WrapLayout Orientation="Horizontal" HorizontalSpacing="8" VerticalSpacing="8" />
</ItemsRepeater.Layout>
```

## UniformGridLayout (virtualizing)

Creates a uniform grid of items, with adaptive sizing and spacing.

- `MinItemWidth` / `MinItemHeight`: base cell size. If NaN, uses the first realized item.
- `MinRowSpacing` / `MinColumnSpacing`: minimum spacing; may expand when justification requires.
- `ItemsJustification`: `Start`, `Center`, `End`, `SpaceAround`, `SpaceBetween`, `SpaceEvenly`.
- `ItemsStretch`: `None`, `Fill`, `Uniform`.
- `MaximumRowsOrColumns`: cap rows or columns for fixed grids.
- `Orientation`: controls the scrolling axis.

```xml
<ItemsRepeater.Layout>
  <UniformGridLayout MinItemWidth="140" MinItemHeight="100"
                     ItemsJustification="SpaceBetween"
                     ItemsStretch="Fill" />
</ItemsRepeater.Layout>
```

## NonVirtualizingStackLayout

Measures and arranges every item. Use only for small collections or when size-to-content is required.

```xml
<ItemsRepeater.Layout>
  <NonVirtualizingStackLayout Orientation="Horizontal" Spacing="4" />
</ItemsRepeater.Layout>
```

## Choosing a Layout

- Use `StackLayout` for lists, logs, and simple rows/columns.
- Use `WrapLayout` for flowing chips, tags, or variable-size tiles.
- Use `UniformGridLayout` for photo grids, dashboards, or icon tiles.
- Use `NonVirtualizingStackLayout` only when virtualization must be disabled.
