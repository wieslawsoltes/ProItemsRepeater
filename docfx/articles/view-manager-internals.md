# ViewManager and Element Ownership Internals

This article documents how `ItemsRepeater` realizes, clears, and reuses elements through `ViewManager`, and how ownership is tracked via `VirtualizationInfo`. It complements `element-lifecycle.md` with lower-level behavior.

## Responsibilities

`ViewManager` is responsible for:

- Realizing elements for a requested index.
- Clearing elements and transferring ownership to pools.
- Tracking realized index ranges for fast lookup.
- Managing pinning (focus and manual pinning).
- Handling stable reset flows with unique IDs.

`VirtualizationInfo` is the per-element state that records ownership, index, and pin state.

## Ownership model (VirtualizationInfo)

Each realized element has an owner state:

- `ElementFactory`: element is owned by the template factory (not realized by layout).
- `Layout`: element is realized and held by the layout.
- `PinnedPool`: element is cleared but pinned (kept alive).
- `UniqueIdResetPool`: element is kept across a stable reset when unique IDs are available.
- `Animator`: reserved for animated remove paths (not actively used in this repo).

Ownership transitions are explicit in `VirtualizationInfo` (e.g., `MoveOwnershipToLayoutFromElementFactory`).

## Realization pipeline

`ViewManager.GetElement(index, forceCreate, suppressAutoRecycle)` resolves elements in priority order:

1. **Already held by layout**: return a realized child at that index if available.
2. **Made anchor**: if a bring-into-view anchor was created, reuse it.
3. **UniqueId reset pool**: reuse an element from `UniqueIdElementPool` when a stable reset is pending.
4. **Pinned pool**: reuse a pinned element with matching index.
5. **Element factory**: create or recycle via `ItemTemplateShim` (or default template).

After obtaining an element:

- `AutoRecycleCandidate` is set unless `suppressAutoRecycle` is requested.
- `KeepAlive` is set when auto-recycle is enabled so the layout can keep it for the pass.

## Element factory path and DataContext rules

`GetElementFromElementFactory` handles the template pipeline and DataContext semantics:

1. If no template is provided and the data item is a `Control`, it is returned directly.
2. Otherwise, an `IElementFactory` is used to get an element.
3. If the element is not the data item, the DataContext is set to the data and `MustClearDataContext` is set.

On clear, `MustClearDataContext` causes the DataContext to be cleared to avoid leaks when the repeater set it.

## Clearing pipeline

`ViewManager.ClearElement(element, isClearedDueToCollectionChange)` routes the element to the correct pool:

1. **UniqueId reset pool** (if stable reset pending):
   - Ownership moves to `UniqueIdResetPool`.
2. **Pinned pool** (if pinned and not cleared due to a collection change):
   - Ownership moves to `PinnedPool`.
3. **Element factory** (default):
   - `OnElementClearing` is raised.
   - Element is recycled via `ItemTemplateShim` or removed from `Children`.
   - DataContext is cleared if `MustClearDataContext` is set.

Pinned elements are never kept when the clear is due to a Remove/Replace/Reset; they are forced back to the factory.

## Pinning and focus

Pinning keeps elements alive across layout passes. `ViewManager.UpdatePin`:

- Walks up the visual tree to find the owning `ItemsRepeater`.
- Calls `VirtualizationInfo.AddPin` or `RemovePin`.
- If the pin count reaches zero, the repeater invalidates measure so the element can be cleared.

Focus tracking:

- `ViewManager` listens to `GotFocus`/`LostFocus`.
- The focused element is pinned; the previous focus is unpinned.
- If the focused element is cleared, a focus candidate is selected from adjacent realized elements.

## Stable reset with unique IDs

If the data source supports `ItemsSourceView.HasKeyIndexMapping`:

1. On `Reset`, `_isDataSourceStableResetPending` is set.
2. Auto-recycle candidates are cleared into `UniqueIdElementPool`.
3. On the next arrange (`OnOwnerArranged`), elements in the pool are flushed and recycled.
4. If an element is requested while reset is pending, `GetElementFromUniqueIdResetPool` can reuse it by key.

This reduces churn across resets by reusing containers by unique ID.

## Realized range caching

`ViewManager` keeps `_firstRealizedElementIndexHeldByLayout` and `_lastRealizedElementIndexHeldByLayout` to avoid scanning children on every query. These values are:

- Updated lazily when a realized element is queried.
- Invalidated on collection changes.
- Updated when elements are realized or cleared.

## Interactions with ItemsRepeater

`ItemsRepeater` calls into `ViewManager`:

- `GetElementImpl` -> `ViewManager.GetElement`.
- `ClearElementImpl` -> `ViewManager.ClearElement`.
- `OnElementPrepared`, `OnElementClearing`, `OnElementIndexChanged` are raised by `ItemsRepeater` to external listeners.

`ViewManager` is the low-level engine behind the public lifecycle events.
