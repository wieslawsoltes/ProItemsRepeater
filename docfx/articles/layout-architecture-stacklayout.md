# StackLayout Architecture

This article describes the internal layout pipeline for `StackLayout` as used by `ItemsRepeater`. It assumes familiarity with `VirtualizingLayout` and `FlowLayoutAlgorithm`.

## Components and responsibilities

- `StackLayout` (`VirtualizingLayout`, `IFlowLayoutAlgorithmDelegates`)
  - Defines orientation and spacing.
  - Supplies algorithm callbacks for measurement, anchoring, and extent estimation.
- `StackLayoutState`
  - Stores the shared `FlowLayoutAlgorithm`.
  - Tracks measured sizes using a rolling estimation buffer and prefix sums.
  - Tracks the maximum minor size to compute the cross-axis extent.
- `FlowLayoutAlgorithm`
  - Implements realization, anchor selection, generation, and arranging.

## Initialization sequence

1. `StackLayout.InitializeForContextCore` checks `context.LayoutState`.
2. If no state exists, it creates `StackLayoutState`.
3. `StackLayoutState.InitializeForContext` sets up `FlowLayoutAlgorithm` with the context and delegate callbacks.

`LayoutState` is stored on the layout context (per repeater instance), not on the layout object.

## Measure pipeline (step-by-step)

1. `StackLayout.MeasureOverride` prepares the state:
   - `EnsureLineCacheParameters(Orientation, Spacing)`
   - `EnsureItemCount(context.ItemCount)`
   - Clears line and size caches
   - `OnMeasureStart()` resets the cross-axis maximum
2. Calls `FlowLayoutAlgorithm.Measure(...)` with:
   - `isWrapping = false`
   - `minItemSpacing = 0`
   - `lineSpacing = Spacing`
   - `maxItemsPerLine = int.MaxValue`
   - `disableVirtualization = DisableVirtualization`
3. `FlowLayoutAlgorithm` selects an anchor:
   - Uses `RecommendedAnchorIndex` if realized and still in the realization window.
   - Otherwise calls `Algorithm_GetAnchorForRealizationRect` (StackLayout implementation).
4. `Algorithm_GetAnchorForRealizationRect` estimates an anchor based on:
   - The realization rect offset inside the last extent.
   - The estimated average item size from `StackLayoutState`.
   - A clamped offset to avoid invalid indices.
5. The algorithm generates items forward and backward from the anchor:
   - `Algorithm_GetMeasureSize` returns the provided available size.
   - `Algorithm_GetProvisionalArrangeSize` enforces the minor-axis size to be at least the desired size (and respects finite available size).
   - `Algorithm_OnElementMeasured` updates `StackLayoutState` with measured sizes and updates the estimation buffer and prefix sums.
6. The algorithm computes the extent via `Algorithm_GetExtent`:
   - The major size uses `StackLayoutState.GetEstimatedTotalSize(...)`.
   - The minor size uses `StackLayoutState.MaxArrangeBounds`.
   - If realized items exist, the extent origin is aligned to the first realized item's offset.

## Anchor strategy details

`StackLayout` anchors by index rather than by pixel data:

- The estimated offset for an index is derived from the prefix sums of measured items plus estimated sizes for unknown items.
- When no measured items exist, the layout measures index `0` to seed the estimator.

This approach allows fast jumps while still producing a stable anchor.

## Arrange pipeline (step-by-step)

1. `StackLayout.ArrangeOverride` calls `FlowLayoutAlgorithm.Arrange(...)`.
2. Because `isWrapping` is false:
   - Each item occupies its own "line".
   - The algorithm aligns lines to the start.
   - The minor axis is stretched to at least the available final size.
3. `FlowLayoutAlgorithm` arranges only the realized items.

## Items source changes

`StackLayout.OnItemsChangedCore`:

1. Forwards changes to `FlowLayoutAlgorithm.OnItemsSourceChanged(...)`.
2. Clears line and size caches to prevent stale estimates.
3. Invalidates layout to re-run measure.

## Properties that affect the algorithm

- `Orientation`: defines the major (stacking) axis and scroll orientation.
- `Spacing`: applied as line spacing between items.
- `DisableVirtualization`: forces `FlowLayoutAlgorithm` to generate all items regardless of the realization window.
