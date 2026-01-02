# UniformGridLayout Architecture

This article describes the internal layout pipeline for `UniformGridLayout` in `ItemsRepeater`. It focuses on fixed cell sizing, row-based anchoring, and stretch/justification behavior.

## Components and responsibilities

- `UniformGridLayout` (`VirtualizingLayout`, `IFlowLayoutAlgorithmDelegates`)
  - Defines fixed cell sizing rules and justification.
  - Supplies algorithm callbacks for measurement, anchoring, and extent estimation.
- `UniformGridLayoutState`
  - Stores the shared `FlowLayoutAlgorithm`.
  - Computes `EffectiveItemWidth` / `EffectiveItemHeight`.
  - Manages cached ownership of the first element when measuring defaults.
- `FlowLayoutAlgorithm`
  - Implements realization, anchoring, generation, and arranging.

## Initialization sequence

1. `UniformGridLayout.InitializeForContextCore` ensures a `UniformGridLayoutState`.
2. `UniformGridLayoutState.InitializeForContext` initializes `FlowLayoutAlgorithm` with callbacks.

## Measure pipeline (step-by-step)

1. Determine effective item size:
   - If `MinItemWidth` / `MinItemHeight` are NaN, the first item is measured to derive defaults.
   - `UniformGridLayoutState` may cache element `0` if not already realized by the algorithm.
   - `ItemsStretch` expands the minor axis, optionally adjusting the major axis proportionally.
2. Call `FlowLayoutAlgorithm.Measure(...)` with:
   - `isWrapping = true`
   - `minItemSpacing = MinItemSpacing` (row/column spacing depends on orientation)
   - `lineSpacing = LineSpacing`
   - `maxItemsPerLine = MaximumRowsOrColumns`
   - `disableVirtualization = false`
3. The delegate callbacks provide fixed size values:
   - `Algorithm_GetMeasureSize` returns the effective width/height.
   - `Algorithm_GetProvisionalArrangeSize` returns the same fixed size.
4. The algorithm generates items forward and backward from the anchor, using uniform cell sizes.
5. After measure, `EnsureFirstElementOwnership` releases any cached element if the algorithm took ownership.

## Anchor selection strategy

Anchoring is row-based:

- `Algorithm_GetAnchorForRealizationRect` computes:
  - `itemsPerLine` from available minor size and effective item size.
  - The row index intersecting the realization rect.
  - The anchor as the first item in that row.
- `Algorithm_GetAnchorForTargetElement`:
  - Computes the row for the target index.
  - Anchors to the first index in that row.

This provides stable anchors during fast scrolls because item sizes are deterministic.

## Extent estimation

`Algorithm_GetExtent` computes:

- `itemsPerLine` and `lineSize` from effective item size and spacing.
- Major extent = `(itemCount / itemsPerLine) * lineSize - lineSpacing`.
- Minor extent:
  - Uses available minor size when `ItemsStretch = Fill`.
  - Otherwise uses `itemsPerLine * itemSize + spacing`.

If realized items exist, the extent origin is aligned with the first realized line.

## Arrange pipeline (step-by-step)

1. `UniformGridLayout.ArrangeOverride` calls `FlowLayoutAlgorithm.Arrange(...)`.
2. Line alignment maps directly from `ItemsJustification`:
   - `Start`, `Center`, `End`, `SpaceBetween`, `SpaceAround`, `SpaceEvenly`.
3. Items are arranged into a fixed grid using the computed effective item size.

## Items source changes

`UniformGridLayout.OnItemsChangedCore`:

1. Forwards changes to `FlowLayoutAlgorithm.OnItemsSourceChanged(...)`.
2. Invalidates measure to recompute layout.
3. Clears cached element `0` if the change touches index `0`.

## Orientation and scroll axis

`UniformGridLayout` maps orientation to scroll axis inversely:

- `Orientation.Horizontal` -> vertical scroll orientation.
- `Orientation.Vertical` -> horizontal scroll orientation.

This matches grid behavior where rows (or columns) advance orthogonally to the scroll axis.
