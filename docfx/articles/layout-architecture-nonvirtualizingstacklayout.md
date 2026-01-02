# NonVirtualizingStackLayout Architecture

This article describes the internal layout pipeline for `NonVirtualizingStackLayout`. Unlike virtualizing layouts, it always measures and arranges every child.

## Components and responsibilities

- `NonVirtualizingStackLayout` (`NonVirtualizingLayout`)
  - Measures and arranges all children sequentially.
  - Applies spacing and alignment.
- `NonVirtualizingLayoutContext`
  - Provides `Children`, the full realized list.

## Initialization sequence

`NonVirtualizingStackLayout` does not maintain custom layout state. It relies on the base `NonVirtualizingLayout` lifecycle without additional initialization logic.

When used in `ItemsRepeater`, the context is provided through a `VirtualLayoutContextAdapter`, which exposes all items as `Children`.

## Measure pipeline (step-by-step)

1. Determine the stacking axis based on `Orientation`.
2. Set an infinite constraint on the stacking axis (height for vertical, width for horizontal).
3. Iterate through all `context.Children`:
   - Skip children that are not visible.
   - Measure each element with the constrained size.
   - Accumulate major-axis size plus spacing.
   - Track the maximum minor-axis size.
4. Return the total size:
   - Major size = sum of desired sizes plus spacing.
   - Minor size = max of desired sizes.

## Arrange pipeline (step-by-step)

1. Iterate through all `context.Children` in order.
2. Compute a `Rect` for each element based on:
   - Current offset on the stacking axis.
   - Alignment rules (`HorizontalAlignment` / `VerticalAlignment`).
3. Arrange the element into that rect.
4. Advance the offset by the arranged size plus spacing.

The final size is the maximum of the accumulated bounds and the provided `finalSize`.

## Items source changes

`NonVirtualizingStackLayout` does not track state and relies on the control to invalidate measure when items change. In `ItemsRepeater`, item changes trigger a new measure pass.

## When to use

Use `NonVirtualizingStackLayout` only when all items must be realized (size-to-content, small collections, or custom behaviors that require full realization).
