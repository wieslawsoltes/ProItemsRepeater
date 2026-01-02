# Prefix Sums and Fenwick Trees

This article explains how prefix sums are used in the virtualization pipeline, with a focus on `StackLayoutState`. It also documents the underlying data structure (Fenwick tree, also known as a Binary Indexed Tree) and why it is used.

## Why prefix sums are needed

Virtualizing layouts cannot measure every element on every pass. However, they still need to:

- Estimate the pixel offset for an item index (for anchoring and scrolling).
- Estimate the total extent of a large list.
- Adjust those estimates as new items are measured.

Prefix sums provide fast cumulative totals, so you can estimate offsets without iterating over all items.

## Where prefix sums are used

In this codebase, prefix sums are used in `StackLayoutState` to support `StackLayout`:

- `StackLayoutState` tracks measured major-axis sizes per item.
- Prefix sums are used to compute estimated offsets for any index.
- Those offsets drive anchor selection and extent estimation.

`WrapLayout` and `UniformGridLayout` use different techniques (line caches and direct arithmetic), so prefix sums are primarily a `StackLayout` optimization.

## Data structure: Fenwick tree (Binary Indexed Tree)

`StackLayoutState` uses two Fenwick trees:

- `FenwickDouble` for the sum of measured sizes.
- `FenwickInt` for the count of measured items.

Both support:

- `Add(index, delta)` in `O(log n)`
- `Sum(index)` (prefix sum over `[0, index)`) in `O(log n)`

The implementation uses 1-based indexing internally and updates indices by `i += i & -i`.

## Core formulas

Let:

- `measuredSum(i)` = sum of measured sizes for indices `[0, i)`
- `measuredCount(i)` = count of measured items in `[0, i)`
- `unknownCount(i) = i - measuredCount(i)`
- `estimatedSize` = current average size for unmeasured items
- `spacing` = uniform spacing between items

Then the estimated offset for index `i` is:

```
offset(i) = measuredSum(i) + unknownCount(i) * estimatedSize + spacing * i
```

This formula is implemented in `StackLayoutState.GetEstimatedOffsetForIndex(...)`.

## How measurements update the prefix sums

When an item is measured:

1. `StackLayoutState.UpdateMeasuredSize(index, majorSize)` checks if the size was previously known.
2. If unknown, it:
   - Stores the size in `_measuredSizes`.
   - Updates the sum Fenwick tree by `+majorSize`.
   - Updates the count Fenwick tree by `+1`.
3. If the size changed, it updates the sum tree by the delta.

This makes all downstream prefix-sum queries reflect the newest measurements.

## Anchor selection with prefix sums

`StackLayout` uses prefix sums in two places:

1. **Realization window anchoring**:
   - `GetAnchorForRealizationRect` estimates the index for a pixel offset.
   - It calls `EstimateIndexForOffset(offset, estimatedSize, spacing, itemCount)`.
   - This method uses binary search over indices and calls `GetEstimatedOffsetForIndex` for each probe.

2. **Target anchoring**:
   - `GetAnchorForTargetElement` estimates the anchor offset for a target index.
   - It uses `GetEstimatedOffsetForIndex` directly if bounds are not available.

The result is stable anchors even when most items have not been measured yet.

## Extent estimation with prefix sums

The total extent is estimated using:

```
total = measuredSum(n) + unknownCount(n) * estimatedSize + spacing * (n - 1)
```

This is implemented in `StackLayoutState.GetEstimatedTotalSize(...)` and used by `StackLayout` in `Algorithm_GetExtent`.

## Average size estimation

The `estimatedSize` used in formulas comes from:

- A rolling estimation buffer that tracks recently measured sizes.
- Prefix-sum derived averages when enough items have been measured.

This allows the layout to converge as more items are realized while keeping estimates stable.

## Complexity and behavior

Using Fenwick trees provides:

- `O(log n)` updates when an item is measured or re-measured.
- `O(log n)` prefix sums for any index.
- `O(log^2 n)` anchor estimation via binary search + prefix sums.

This is a significant improvement over naive `O(n)` accumulation for large lists.

## Summary

Prefix sums are the backbone of `StackLayout`'s estimation logic. They enable fast, incremental updates and stable anchor selection without measuring the entire list, which is critical for large or dynamic item sources.
