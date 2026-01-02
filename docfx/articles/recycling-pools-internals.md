# Recycling Pools and Template Reuse Internals

This article documents the low-level recycling system used by `ItemsRepeater`. It complements `templates-and-recycling.md` with internal details and control flow.

## RecyclePool data model

`RecyclePool` is a per-template pool that stores reusable elements:

- Each pool is keyed by `IDataTemplate` in a `ConditionalWeakTable`.
- Elements are grouped by a reuse key (string) into lists.
- Each entry tracks the element and its last owning `Panel`.

This enables reuse across repeaters and reduces reparenting when possible.

## Attached properties

`RecyclePool` uses two attached properties on realized controls:

- `OriginTemplateProperty`: the template that created the element.
- `ReuseKeyProperty`: the key used to choose the pool bucket.

These are normally managed by `RecyclingElementFactory`.

## Put and get semantics

### Putting an element

`RecyclePool.PutElement(element, key, owner)`:

1. Validates that `owner` is a `Panel` (or null).
2. Appends the element to the list for the given key.
3. Stores the element with its owner to allow owner affinity.

### Getting an element

`RecyclePool.TryGetElement(key, owner)`:

1. Looks up the list for the key.
2. Prefers elements whose stored owner matches the current owner.
3. If the element has a different owner, removes it from the old `Panel.Children`.
4. Returns the element for reuse or `null` if none are available.

Owner affinity avoids unnecessary visual tree detach/attach costs.

## Element factory integration

`ItemsRepeater` uses `ItemTemplateShim` (`IElementFactory`) to realize elements:

- If no template is provided, `FuncDataTemplate.Default` is used.
- If the data item is a `Control`, it may be returned directly.
- Otherwise, the element factory creates or recycles an element.

When `ItemsRepeater` sets the DataContext, it marks `VirtualizationInfo.MustClearDataContext` so the DataContext can be cleared on recycle.

## UniqueIdElementPool (stable reset)

`UniqueIdElementPool` is a separate mechanism used only for stable resets:

- Activated when `ItemsSourceView.HasKeyIndexMapping` is true.
- Elements are keyed by `ItemsSourceView.KeyFromIndex`.
- On `Reset`, elements are moved into the pool instead of being fully recycled.
- On the next realization pass, elements can be rehydrated by key.

This pool is managed by `ViewManager`, not by `RecyclePool`.

## Pinned elements and reuse

Pinned elements are never placed into `RecyclePool`:

- If an element is pinned and the clear is not due to a collection change, it moves to the pinned pool.
- If the clear is due to remove/replace/reset, pinned elements are forced back through the factory.

This prevents pinned elements from being reused incorrectly.

## Reuse across repeaters

Because `RecyclePool` is associated with a template (not a repeater), pools can be shared across repeaters by using the same template instance or a shared `RecyclePool`.

## Summary

`RecyclePool` handles template-keyed element reuse, while `UniqueIdElementPool` supports stable resets by key. Together with `ItemTemplateShim` and `VirtualizationInfo`, these pools provide efficient container reuse without breaking ownership invariants.
