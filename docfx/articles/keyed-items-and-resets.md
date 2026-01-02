# Keyed Items and Stable Resets

This article explains how `ItemsRepeater` preserves element identity across collection resets when the data source provides stable keys.

## Why keys matter

A full `Reset` invalidates the item order and typically forces all realized elements to be recycled. If the data source can map items to stable keys, `ItemsRepeater` can reduce churn by reusing elements after a reset.

## Key mapping via ItemsSourceView

`ItemsRepeater` uses `ItemsSourceView` to access items and metadata. When the view supports key mapping:

- `ItemsSourceView.HasKeyIndexMapping` is `true`.
- `ItemsSourceView.KeyFromIndex(index)` returns a stable string key.

You can provide a custom `ItemsSourceView` if you need stable keys for non-standard data sources.

## Stable reset behavior

When a `Reset` occurs and key mapping is available:

1. The repeater marks the reset as "stable".
2. Realized elements are moved into a temporary reset pool keyed by the item key.
3. As new items are realized, the repeater tries to reuse elements from the reset pool by key.
4. After the next arrange pass, any remaining pooled elements are recycled.

This allows elements to survive reorder operations and large refreshes when keys are stable.

If key mapping is not available, a reset clears all realized elements and new ones are created on demand.

## Practical guidance

- If your data source has stable IDs, expose them through a custom `ItemsSourceView`.
- Stable keys are most useful when a reset reorders items but the underlying set is the same.
- If the data set changes completely, stable keys provide less benefit.

## Related docs

- `items-source.md`
- `recycling-pools-internals.md`
