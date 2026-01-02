# ViewportManager Anchoring and Shift Internals

This article explains the low-level scroll anchoring and viewport shift logic in `ViewportManager`. It complements `scrolling-internals.md` with implementation details.

## Responsibilities

`ViewportManager` coordinates:

- Effective viewport tracking from the parent scroller.
- Visible and realization window calculation.
- Scroll anchoring via `IScrollAnchorProvider`.
- Expected viewport shifts when layout extent changes.
- Cache buffer growth for virtualization.

## Scroller discovery and subscriptions

`EnsureScroller` walks the visual tree and captures the first `IScrollAnchorProvider`:

1. If a scroller is found, it is stored in `_scroller`.
2. `EffectiveViewportChanged` is subscribed if viewport management is enabled.
3. `_ensuredScroller` prevents redundant scans.

`OnLayoutChanged` toggles viewport management off for non-virtualizing layouts by unsubscribing.

## Visible and realization windows

### Visible window

`GetLayoutVisibleWindow` returns a window in layout coordinates:

- If a `MadeAnchor` exists, the window is pinned to `(0,0)` to protect the anchor.
- Otherwise, the window is adjusted by:
  - `_layoutExtent` (layout origin)
  - `_expectedViewportShift`
  - `_unshiftableShift`

### Realization window

`GetLayoutRealizationWindow` expands the visible window by cache buffers:

- `HorizontalCacheLength` and `VerticalCacheLength` are applied as pixel buffers.
- Buffers grow incrementally during arrange to avoid sudden spikes.

## Cache buffer growth

On `OnOwnerArranged`:

- The cache buffer per side grows by a fixed pixel delta.
- It is clamped to the maximum derived from cache length and viewport size.

This spreads the cost of building cache across frames.

## Extent changes and expected shifts

`SetLayoutExtent` computes expected viewport shifts:

1. Compute `deltaX`/`deltaY` from old extent to new extent.
2. Accumulate into `_expectedViewportShift`.
3. If a shift is expected, subscribe to `LayoutUpdated`.
4. Store `_pendingViewportShift` and invalidate scroller arrange.

If the scroller cannot apply the shift:

- `OnLayoutUpdated` transfers `_pendingViewportShift` into `_unshiftableShift`.
- Measure is invalidated to re-run layout with the new assumption.
- For `WrapLayout`, repeated failure disables anchoring and schedules measure.

## Effective viewport updates

`OnEffectiveViewportChanged`:

1. Updates `_visibleWindow` if the viewport is valid.
2. Calls `UpdateScrollAnchoring` to enable/disable anchoring on large jumps.
3. Invalidates measure (or schedules it for large `WrapLayout` changes).
4. Clears pending shift state.

Viewport deltas are compared to a threshold:

- Default threshold is 0.5 of the viewport size.
- If exceeded, anchoring is disabled until the viewport stabilizes.

## Scroll anchoring

### Registering candidates

During arrange, `ItemsRepeater` registers realized elements:

- `RegisterScrollAnchorCandidate` calls `_scroller.RegisterAnchorCandidate`.
- Anchoring is disabled for `WrapLayout` and in large jump cases.

### Bring-into-view handling

`OnBringIntoViewRequested`:

1. Finds the immediate child of the repeater.
2. Temporarily unregisters all other anchor candidates.
3. Schedules `OnCompositionTargetRendering` to restore anchors after render.

This ensures the requested element remains the anchor during the scroll.

### Restoring anchors

`OnCompositionTargetRendering`:

- Re-registers all eligible realized elements.
- Clears the temporary anchor.
- Invalidates measure to restore a correct realization window.

## Reset behavior

`ResetScrollers` clears all candidates and subscriptions:

- Unregisters anchor candidates from the scroller.
- Clears `_scroller` and shift state.
- Resets anchoring flags and counters.

## Summary

`ViewportManager` stabilizes scrolling by reconciling layout extents, viewport changes, and anchoring. It tolerates missing shifts, disables anchoring during large jumps, and uses incremental cache growth to balance responsiveness and performance.
