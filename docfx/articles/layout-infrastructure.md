# Layout Infrastructure

This article describes the layout infrastructure that connects `ItemsRepeater` to its layouts. It focuses on how layout contexts, state, and lifecycle callbacks are wired, and how virtualizing and non-virtualizing layouts are integrated through a common API.

If you want algorithm details, see `virtualization-algorithms.md`. If you want usage guidance, see `layouts.md`.

## Key roles and types

- `ItemsRepeater`: owns the layout instance, provides layout context, and drives Measure/Arrange.
- `AttachedLayout`: common base for all layouts; routes Measure/Arrange and Initialize/Uninitialize based on layout type.
- `VirtualizingLayout` / `NonVirtualizingLayout`: specialization for virtualizing vs non-virtualizing behaviors.
- `LayoutContext`: carries `LayoutState` for per-container layout state.
- `VirtualizingLayoutContext` / `NonVirtualizingLayoutContext`: context flavors with virtualization APIs vs full-children access.
- `RepeaterLayoutContext`: `ItemsRepeater`'s concrete `VirtualizingLayoutContext` implementation.
- Context adapters:
  - `LayoutContextAdapter` (non-virtualizing -> virtualizing with infinite realization rect).
  - `VirtualLayoutContextAdapter` (virtualizing -> non-virtualizing by exposing `Children` through `GetOrCreateElementAt`).

## Layout lifecycle and state

### Layout assignment and initialization

When `ItemsRepeater.Layout` changes, the control:

1. Calls `UninitializeForContext` on the old layout.
2. Unsubscribes from its layout invalidation weak events.
3. Clears realized elements and resets `LayoutState`.
4. Calls `InitializeForContext` on the new layout.
5. Subscribes to new layout invalidation weak events.

`LayoutState` is stored on the context, not on the layout itself. This allows a single layout instance to be reused across multiple repeaters without sharing state.

### Layout invalidation

`AttachedLayout` exposes protected `InvalidateMeasure()` and `InvalidateArrange()`. Layouts call these in response to property changes or internal state updates.

`ItemsRepeater` listens to the weak events and forwards them to its own `InvalidateMeasure()` / `InvalidateArrange()`, keeping layout invalidation centralized.

## Measure/Arrange integration

### Measure pass (virtualizing)

High-level call flow:

1. `ItemsRepeater.MeasureOverride` calls `ViewportManager.OnOwnerMeasuring()`.
2. `ItemsRepeater` calls `AttachedLayout.Measure(context, availableSize)`.
3. `AttachedLayout` detects layout type and calls:
   - `VirtualizingLayout.MeasureOverride(...)` or
   - `NonVirtualizingLayout.MeasureOverride(...)`.
4. Virtualizing layouts use `context.RealizationRect` to choose which items to realize.
5. `ItemsRepeater` records the resulting extent using `LayoutOrigin` and the returned size.
6. `ViewportManager.SetLayoutExtent(...)` receives the extent for scroll/viewport coordination.

Key infrastructure points:

- `RepeaterLayoutContext.RealizationRect` is derived from `ViewportManager`'s realization window.
- `LayoutOrigin` allows the layout to adjust estimated content origin as measurements improve.

### Arrange pass (virtualizing)

High-level call flow:

1. `ItemsRepeater.ArrangeOverride` calls `AttachedLayout.Arrange(...)`.
2. The layout arranges realized elements.
3. `ItemsRepeater` arranges recycled elements off-screen and registers realized elements as anchor candidates.
4. `ViewportManager.OnOwnerArranged()` updates cache growth and anchoring behavior.

### Items source changes

`ItemsRepeater` forwards collection changes to virtualizing layouts:

1. The view manager updates realized element bookkeeping.
2. If the layout is virtualizing, `VirtualizingLayout.OnItemsChanged(...)` is called.
3. Otherwise, the repeater invalidates measure for non-virtualizing layouts.

Virtualizing layouts typically update caches and invalidate measure; non-virtualizing layouts usually invalidate measure directly.

## Context adaptation

`AttachedLayout` accepts a base `LayoutContext` and adapts it if the layout type does not match the context type.

### VirtualizingLayout with a non-virtualizing context

If a virtualizing layout is hosted on a `NonVirtualizingLayoutContext`, `AttachedLayout` wraps it with `LayoutContextAdapter`:

- `RealizationRect` becomes infinite.
- `LayoutOrigin` is fixed at `(0,0)`.
- `GetOrCreateElementAt` maps directly to the non-virtualizing children.

This allows a virtualizing layout to operate in a non-virtualized host without special-case logic.

### NonVirtualizingLayout with a virtualizing context

If a non-virtualizing layout is hosted on a `VirtualizingLayoutContext`, `AttachedLayout` wraps it with `VirtualLayoutContextAdapter`:

- `Children` is exposed as a virtual list backed by `GetOrCreateElementAt`.
- Iterating `Children` forces realization of every item.
- `LayoutState` is still stored on the original virtualizing context.

This is the path used by `ItemsRepeater` when `Layout` is non-virtualizing, because its native context is virtualizing.

## RepeaterLayoutContext specifics

`RepeaterLayoutContext` is the concrete `VirtualizingLayoutContext` implementation used by `ItemsRepeater`. It supplies:

- `RealizationRect`: from `ItemsRepeater.RealizationWindow`, which is computed by `ViewportManager`.
- `RecommendedAnchorIndex`: derived from the current scroll anchor (`SuggestedAnchor`).
- `LayoutState`: stored on the repeater instance.
- `LayoutOrigin`: forwarded to and from `ItemsRepeater.LayoutOrigin`.
- Element lifecycle methods that delegate to `ItemsRepeater` (realize, recycle, index lookup).

This context is the primary bridge between layout algorithms and the repeater control.

## Where virtualization plugs in

Layout infrastructure does not implement virtualization itself. Instead, it provides a stable contract:

- Virtualizing layouts request elements by index (`GetOrCreateElementAt`).
- Layouts decide which items to realize based on `RealizationRect`.
- Layouts recycle elements when they fall outside that window.
- Layouts report size and origin estimates for scrolling.

The actual realization strategy lives in layout code and shared helpers like `FlowLayoutAlgorithm` and `ElementManager`.

## Summary call sequence

The following summarizes a typical virtualizing pass:

1. `ItemsRepeater.MeasureOverride` -> `AttachedLayout.Measure(...)`.
2. `AttachedLayout` dispatches to `VirtualizingLayout.MeasureOverride`.
3. Layout uses `context.RealizationRect` to realize items and returns desired size.
4. `ItemsRepeater` updates extent and informs `ViewportManager`.
5. `ItemsRepeater.ArrangeOverride` -> `AttachedLayout.Arrange(...)`.
6. Layout arranges realized items; repeater registers anchors and updates viewport state.

This separation keeps layout algorithms focused on geometry and realization, while `ItemsRepeater` handles viewport integration and element lifecycle.
