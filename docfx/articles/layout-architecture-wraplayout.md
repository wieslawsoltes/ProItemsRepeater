# WrapLayout Architecture

This article describes the internal layout pipeline for `WrapLayout` in `ItemsRepeater`. It focuses on wrapping behavior, line caching, and anchor selection.

## Components and responsibilities

- `WrapLayout` (`VirtualizingLayout`, `IFlowLayoutAlgorithmDelegates`)
  - Defines orientation and row/column spacing.
  - Provides wrapping callbacks for `FlowLayoutAlgorithm`.
- `WrapLayoutState`
  - Stores the shared `FlowLayoutAlgorithm`.
  - Records per-item size metrics.
  - Caches line positions and sizes to accelerate anchor selection.
- `FlowLayoutAlgorithm`
  - Implements realization, anchoring, generation, and arranging.

## Initialization sequence

1. `WrapLayout.InitializeForContextCore` ensures a `WrapLayoutState` is assigned to `context.LayoutState`.
2. `WrapLayoutState.InitializeForContext` initializes `FlowLayoutAlgorithm` with callbacks.

## Measure pipeline (step-by-step)

1. Build `UvMeasure` values based on `Orientation`:
   - `U` is the primary layout axis (width for horizontal orientation, height for vertical).
   - `V` is the secondary axis.
2. If the available `U` is infinite, use the realization window's `U` to stabilize measurements.
3. If the algorithm has invalid measures, clear line stats (`WrapLayoutState.ClearLineStats()`).
4. Ensure parameters:
   - `EnsureParameters(Orientation, spacing, availableU)` resets caches if orientation, spacing, or available size changes.
   - `EnsureItemCount(context.ItemCount)` sizes internal arrays.
5. Begin measure:
   - `BeginMeasure()` clears line stats if the line cache was marked dirty.
6. Call `FlowLayoutAlgorithm.Measure(...)` with:
   - `isWrapping = true`
   - `minItemSpacing = spacing.U`
   - `lineSpacing = spacing.V`
   - `maxItemsPerLine = int.MaxValue`
   - `disableVirtualization = false`
7. During generation:
   - `Algorithm_GetProvisionalArrangeSize` uses the desired size (items can be variable).
   - `Algorithm_ShouldBreakLine` breaks when remaining space is negative.
   - `Algorithm_OnElementMeasured` records item sizes into `WrapLayoutState`.

## Anchor selection strategy

`WrapLayout` uses a tiered strategy:

1. If the realization window is still connected, and line cache is valid:
   - `TryGetLineNearViewport` or `TryGetNearestLine` picks a cached line near the viewport.
2. Otherwise, estimate:
   - `TryGetLineMetrics` derives `itemsPerLine` and `lineAdvance` from cached averages or average item size.
   - The anchor line is chosen by projecting the realization offset onto the estimated line grid.
3. If a target index is requested (bring-into-view):
   - `GetAnchorForTargetElement` finds the first item of the line containing the target.

This keeps anchors stable for both smooth scrolling and large jumps.

## Line cache and item metrics

`WrapLayoutState` records:

- Per-item size metrics (`RecordItemSize`), including whether sizes are variable.
- Per-line cache entries (`UpdateLineCache`) with start index, item count, position, and size.

Line caches are updated during arrange, via `Algorithm_OnLineArranged`. If measured item sizes change, the cache is marked dirty and cleared on the next measure pass.

## Extent estimation

`WrapLayout.GetExtent(...)` computes:

- `itemsPerLine` and `lineAdvance` from cached line averages or average item sizes.
- `lineCount = ceil(itemCount / itemsPerLine)`.
- Major extent = `lineCount * lineAdvance - lineSpacing`.
- Minor extent = available minor size (if finite).

If line metrics cannot be estimated yet, the extent is empty and will be refined as items are measured.

## Arrange pipeline (step-by-step)

1. `WrapLayout.ArrangeOverride` calls `FlowLayoutAlgorithm.Arrange(...)` with line alignment `Start`.
2. The algorithm arranges realized items line-by-line.
3. `Algorithm_OnLineArranged` feeds line metrics back into `WrapLayoutState` to improve future anchors.

## Items source changes

`WrapLayout.OnItemsChangedCore`:

1. Forwards changes to `FlowLayoutAlgorithm.OnItemsSourceChanged(...)`.
2. Clears line and item statistics.
3. Invalidates layout to re-run measure.

## Orientation and scroll axis

`WrapLayout` inverts scroll orientation:

- `Orientation.Horizontal` -> vertical scroll orientation.
- `Orientation.Vertical` -> horizontal scroll orientation.

This matches the fact that the wrap direction is orthogonal to the scroll axis.
