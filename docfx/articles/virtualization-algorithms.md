# Virtualization Algorithms

This article explains the virtualization algorithms used by `ItemsRepeater` and its built-in layouts. It focuses on how elements are realized, positioned, recycled, and how anchor selection works during scrolling and collection changes.

If you only need usage guidance, see `virtualization.md` and `layouts.md`.
For a deep dive into prefix sums used by `StackLayout`, see `virtualization-prefix-sums.md`.

## Overview

`ItemsRepeater` delegates virtualization to layouts that derive from `VirtualizingLayout`. The built-in virtualizing layouts are:

- `StackLayout`
- `WrapLayout`
- `UniformGridLayout`

All three share a common engine, `FlowLayoutAlgorithm`, and then customize its behavior via `IFlowLayoutAlgorithmDelegates`. The algorithm consumes a `VirtualizingLayoutContext`, which provides:

- `RealizationRect`: the viewport plus cache buffer.
- `RecommendedAnchorIndex`: a suggested anchor for stable scrolling.
- `GetOrCreateElementAt` / `RecycleElement`: element lifecycle APIs.
- `LayoutOrigin`: estimated content origin for large lists.

Non-virtualizing layouts (such as `NonVirtualizingStackLayout`) bypass these algorithms and measure all items.

## Core building blocks

### VirtualizingLayoutContext

The context is the contract between `ItemsRepeater` and the layout. The layout:

- Requests elements in the realization window.
- Recycles elements that fall outside the window.
- Uses `LayoutOrigin` to correct estimated content offsets as more data is measured.

### FlowLayoutAlgorithm

`FlowLayoutAlgorithm` is the shared engine for linear and wrapping layouts. It is responsible for:

- Choosing an anchor index and anchor position.
- Generating elements forward and backward from the anchor.
- Computing layout bounds for each realized element.
- Discarding realized elements that are outside the realization window.
- Estimating the total extent for scrolling.
- Arranging realized elements with optional line alignment.

### ElementManager

`ElementManager` tracks the realized range and layout bounds:

- Maintains a contiguous realized range mapped to data indices.
- Stores per-element layout bounds.
- Creates elements on demand and recycles them when they leave the window.
- Uses sentinel null entries for replace operations to preserve the range without creating containers.
- Discards items outside the realization window, while tolerating one extra element beyond the edge to keep anchors stable.

## FlowLayoutAlgorithm pipeline

The algorithm runs during measure and arrange:

1. **Anchor selection**
   - If the context is non-virtualizing, use index 0.
   - Otherwise, try the `RecommendedAnchorIndex` if it is realized and inside the realization window.
   - If the realization window has "jumped" (no overlap, or a large delta), ignore suggested anchors and pick one based on the realization window.
   - For wrapping layouts, re-evaluate the anchor if the minor size or spacing changes, or if a collection change occurred.

2. **Anchor realization**
   - If the chosen anchor is not realized, realize it (and possibly a short range leading to it).
   - If the realized range is disconnected from the new window, discard all realized elements and start over from the new anchor.

3. **Forward and backward generation**
   - Generate forward from the anchor until the window is covered.
   - Generate backward to cover the window in the opposite direction.
   - Each generated element is measured and assigned layout bounds.
   - For wrapping layouts, line breaks are controlled by `Algorithm_ShouldBreakLine`.
   - Generation stops once elements fall outside the realization window, unless virtualization is disabled.

4. **Reflow (wrapping only)**
   - If the first realized element is not aligned to the minor start, reflow from index 0 to keep lines aligned.

5. **Extent estimation**
   - The layout provides `Algorithm_GetExtent`, which combines realized bounds and estimates to compute the scroll extent.
   - `LayoutOrigin` is adjusted to keep the extent stable as estimates improve.

6. **Arrange and line alignment**
   - Elements are arranged line-by-line.
   - Line alignment (`Start`, `Center`, `End`, `SpaceBetween`, `SpaceAround`, `SpaceEvenly`) is applied for wrapping layouts.

## Layout-specific behavior

### StackLayout

`StackLayout` uses `FlowLayoutAlgorithm` in non-wrapping mode:

- `Algorithm_ShouldBreakLine` always returns true, so each item is its own line.
- The anchor is chosen using estimated offsets based on measured item sizes.
- `StackLayoutState` caches measured sizes and uses prefix sums (Fenwick trees) to estimate offsets and total extent.
- `DisableVirtualization` forces the algorithm to generate all elements, even outside the realization window.

For the prefix sum and Fenwick tree details, see `virtualization-prefix-sums.md`.

### UniformGridLayout

`UniformGridLayout` uses `FlowLayoutAlgorithm` in wrapping mode but with fixed cell sizes:

- `UniformGridLayoutState` determines `EffectiveItemWidth` and `EffectiveItemHeight`.
  - If `MinItemWidth` / `MinItemHeight` are NaN, the first item is measured and cached.
  - `ItemsStretch` can expand the effective size to fill the available space.
- Items per line are computed from available size, min spacing, and `MaximumRowsOrColumns`.
- The anchor is chosen by row math: the first index in the row that intersects the realization window.
- Line alignment is driven by `ItemsJustification`, which maps directly to the algorithm line alignment options.

### WrapLayout

`WrapLayout` uses `FlowLayoutAlgorithm` with variable item sizes:

- `WrapLayoutState` records item sizes and caches line stats (start index, item count, position, size).
- Anchor selection favors cached lines near the viewport for stability and performance.
- If cached lines are not available, the layout estimates items per line and line advance using average item sizes.
- `Algorithm_OnLineArranged` feeds line metrics back into the cache to improve subsequent anchors.

## Collection changes and anchors

Collection changes can invalidate anchors and cached metrics:

- `ElementManager` updates realized ranges and uses sentinel entries for replace operations to avoid full re-realization.
- `FlowLayoutAlgorithm` marks a collection change as pending, which forces anchor re-evaluation on the next measure.
- For wrapping layouts, this also triggers column re-evaluation to prevent anchors from landing in the wrong line.

## Non-virtualizing layouts

`NonVirtualizingStackLayout` is not part of the virtualization algorithm pipeline. It measures and arranges all items every time and should be used only when virtualization must be disabled.

## Practical guidance

- Use `StackLayout` for linear lists and `UniformGridLayout` for grids with predictable cell sizes.
- Use `WrapLayout` when item sizes vary and the layout must flow to new lines.
- Keep virtualization enabled unless you explicitly need all elements realized.
- For debugging layout behavior, compare the realized range to the realization window and check anchor selection logic.
