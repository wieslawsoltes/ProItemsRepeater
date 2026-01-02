# ItemsSource and ItemsSourceView

`ItemsRepeater.ItemsSource` accepts any `IEnumerable`. Internally, it wraps the source in `ItemsSourceView` to provide indexed access, counts, and collection change notifications.

## Recommended Sources

- `ObservableCollection<T>` for dynamic collections.
- `IReadOnlyList<T>` or arrays for fixed-size collections.
- `ItemsSourceView` when you need custom indexing or key mapping.

## ItemsSourceView Basics

`ItemsSourceView` provides:

- `Count` for total item count.
- `GetAt(index)` for indexed access.
- `CollectionChanged` for incremental updates.
- Optional key-index mapping to keep element identity stable across reorder operations.

When you assign a plain `IEnumerable`, ItemsRepeater calls `ItemsSourceView.GetOrCreate(source)` automatically. You can also supply an `ItemsSourceView` directly if you need to customize how items are indexed or keyed.

## Collection Change Behavior

ItemsRepeater listens to `CollectionChanged` and forwards changes to the layout. Virtualizing layouts keep realized elements stable where possible; non-virtualizing layouts clear and rebuild elements on resets.

## Key Mapping and Stable Elements

If the source (or view) supports stable keys, ItemsRepeater can keep element identity aligned with items during inserts, removes, and moves. This reduces visual churn when items reorder.
