# ItemsRepeater Uno Port Review Report

Date: 2026-03-26
Branch: `itemsrepeater-uno-port-granular`

## Executive Summary

The current Uno lane is not yet a full source port of `Avalonia.Controls.ItemsRepeater`.

What is in good shape:

- the shared layout engine has been moved onto migrated Avalonia code
- `StackLayout`, `UniformGridLayout`, and `WrapLayout` now follow the migrated flow-layout path
- the library builds and the current unit tests pass

What is still incomplete:

- the repeater realization/runtime layer is still wrapper-based instead of source-ported
- multiple original source files are still missing
- several compatibility surfaces are placeholders or reduced implementations rather than true ports

File coverage snapshot from the audit:

- original Avalonia `.cs` files: `47`
- Uno `.cs` files: `51`
- original files still missing from the Uno lane: `11`
- Uno-only compatibility/adaptation files: `15`

Missing original source files:

- `Controls/ItemTemplateWrapper.cs`
- `Controls/ItemsRepeater.LogicalScrollable.cs`
- `Controls/UniqueIdElementPool.cs`
- `Controls/ViewManager.cs`
- `Controls/ViewportManager.cs`
- `Controls/VirtualizationInfo.cs`
- `DataGrid/RepeaterDataGrid.axaml.cs`
- `Layout/WrapItem.cs`
- `Selection/InternalSelectionModel.cs`
- `Utils/BindingEvaluator.cs`
- `Properties/AssemblyInfo.cs`

## Findings

### [P1] `ItemsRepeater` is still a native Uno wrapper, so the core Avalonia repeater engine has not been ported

Current Uno `ItemsRepeater` is still implemented as a subclass of Uno's native repeater in [ItemsRepeater.cs](/Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/ItemsRepeater.Uno/Controls/ItemsRepeater.cs#L9). The file only adds compatibility properties/events and template normalization in [ItemsRepeater.cs](/Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/ItemsRepeater.Uno/Controls/ItemsRepeater.cs#L11) and [ItemsRepeater.cs](/Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/ItemsRepeater.Uno/Controls/ItemsRepeater.cs#L30). By contrast, the original control owns its own realization, layout, viewport, and child-index logic through [ItemsRepeater.cs](/Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/Avalonia.Controls.ItemsRepeater/Controls/ItemsRepeater.cs#L25), [ItemsRepeater.cs](/Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/Avalonia.Controls.ItemsRepeater/Controls/ItemsRepeater.cs#L72), [ViewManager.cs](/Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/Avalonia.Controls.ItemsRepeater/Controls/ViewManager.cs#L17), [ViewportManager.cs](/Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/Avalonia.Controls.ItemsRepeater/Controls/ViewportManager.cs#L14), [VirtualizationInfo.cs](/Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/Avalonia.Controls.ItemsRepeater/Controls/VirtualizationInfo.cs#L27), and the concrete [RepeaterLayoutContext.cs](/Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/Avalonia.Controls.ItemsRepeater/Controls/RepeaterLayoutContext.cs#L14). The Uno file named [RepeaterLayoutContext.cs](/Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/ItemsRepeater.Uno/Controls/RepeaterLayoutContext.cs#L5) is only a shell with `HasMadeAnchor => false`, so the port currently lacks the control-runtime half of the original implementation.

Impact:

- cache-length behavior, anchor management, viewport tracking, logical scrolling, and realization ownership are still delegated to Uno rather than coming from migrated Avalonia code
- this means the port is not yet architecture-parity with the original control

### [P1] The item-template recycling path is not source-ported and currently drops the original recycle-pool behavior

The original Avalonia item-template path uses [ItemTemplateWrapper.cs](/Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/Avalonia.Controls.ItemsRepeater/Controls/ItemTemplateWrapper.cs#L10) together with [RecyclePool.cs](/Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/Avalonia.Controls.ItemsRepeater/Controls/RecyclePool.cs#L16) so template-created elements can be reused and associated with their originating template. The Uno port does not include `ItemTemplateWrapper.cs`, and the current normalization path in [ItemsRepeater.cs](/Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/ItemsRepeater.Uno/Controls/ItemsRepeater.cs#L77) creates a `TemplateElementFactory` whose recycle hook is a no-op in [ItemsRepeater.cs](/Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/ItemsRepeater.Uno/Controls/ItemsRepeater.cs#L104). The Uno [RecyclePool.cs](/Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/ItemsRepeater.Uno/Controls/RecyclePool.cs#L10) also no longer carries the original attached `OriginTemplateProperty`, so the original template ownership path is gone.

Impact:

- template-generated elements are not recycled through the original Avalonia path
- this is a behavior and performance regression compared to the original repeater

### [P1] `WrapLayout` now depends on invalid-measure detection that has been stubbed out

The migrated `WrapLayout` still uses `FlowLayoutAlgorithm.HasInvalidMeasure()` during measure in [WrapLayout.cs](/Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/ItemsRepeater.Uno/Layout/WrapLayout.cs#L101) to decide when to clear cached line stats. In the current Uno `ElementManager`, [ElementManager.cs](/Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/ItemsRepeater.Uno/Layout/ElementManager.cs#L145) returns `false` unconditionally. The original Avalonia implementation checks realized elements and returns `true` when a realized element's measure is invalid in [ElementManager.cs](/Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/Avalonia.Controls.ItemsRepeater/Layout/ElementManager.cs#L158).

Impact:

- `WrapLayout` can keep stale cached line metrics after item template or size invalidation
- that can produce incorrect anchors, extents, or scroll-position estimation after content changes

### [P1] `SelectedValueBinding` is running on a reduced reflection helper instead of the original binding-evaluator path

The original project has a dedicated evaluator in [BindingEvaluator.cs](/Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/Avalonia.Controls.ItemsRepeater/Utils/BindingEvaluator.cs#L10) that binds a value property and reuses Avalonia binding semantics. The Uno lane does not include that file, and the current compatibility `BindingBase` only exposes a direct `Evaluate` method in [AvaloniaData.cs](/Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/ItemsRepeater.Uno/Compatibility/AvaloniaData.cs#L21), with [Binding.EvaluateCore](/Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/ItemsRepeater.Uno/Compatibility/AvaloniaData.cs#L41) delegating to a property-path helper. `SelectingItemsRepeater` relies on that simplified evaluation in [SelectingItemsRepeater.cs](/Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/ItemsRepeater.Uno/Controls/SelectingItemsRepeater.cs#L570) and [SelectingItemsRepeater.cs](/Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/ItemsRepeater.Uno/Controls/SelectingItemsRepeater.cs#L592).

Impact:

- `SelectedValueBinding` is not running on a real migrated binding evaluator
- binding behavior is narrower than the original control and will diverge as soon as callers rely on more than simple property-path evaluation

### [P2] `RepeaterDataGrid` is still a custom Uno rewrite rather than a close source port of the original control

The original datagrid uses a templated `HeaderRepeater`/`RowsRepeater` structure in [RepeaterDataGrid.axaml.cs](/Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/Avalonia.Controls.ItemsRepeater/DataGrid/RepeaterDataGrid.axaml.cs#L21) and wires the template parts in [RepeaterDataGrid.axaml.cs](/Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/Avalonia.Controls.ItemsRepeater/DataGrid/RepeaterDataGrid.axaml.cs#L131). The Uno version is a manual rewrite that uses a `StackPanel` header host and builds header cells directly in code in [RepeaterDataGrid.cs](/Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/ItemsRepeater.Uno/DataGrid/RepeaterDataGrid.cs#L161), [RepeaterDataGrid.cs](/Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/ItemsRepeater.Uno/DataGrid/RepeaterDataGrid.cs#L407), and later manual header-cell creation. It also introduces a dedicated `RepeaterDataGridRowControl` path instead of staying close to the original row border/data-template path.

Impact:

- the datagrid may be functionally close, but it is not yet a “only Uno boundary changes” port
- parity and perf issues here will be harder to reason about because the implementation shape no longer closely matches the original control

### [P3] Logging and diagnostics are placeholders, not real ports

The Uno compatibility logger is a no-op in [AvaloniaLogging.cs](/Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/ItemsRepeater.Uno/Compatibility/AvaloniaLogging.cs#L13), and all diagnostics entry points are stubbed in [ItemsRepeaterDiagnostics.cs](/Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/ItemsRepeater.Uno/Diagnostics/ItemsRepeaterDiagnostics.cs#L6). The original project has actual measurement/generation instrumentation in [ItemsRepeaterDiagnostics.cs](/Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/Avalonia.Controls.ItemsRepeater/Diagnostics/ItemsRepeaterDiagnostics.cs#L14).

Impact:

- not user-visible in normal behavior
- but it removes the original instrumentation used to reason about layout/generation cost and makes perf regressions harder to diagnose

## What Is Properly Ported

The strongest part of the current Uno lane is the shared layout engine. These files are now close to the original Avalonia design and are reasonable Uno boundary adaptations:

- `Layout/FlowLayoutAlgorithm.cs`
- `Layout/ElementManager.cs`
- `Layout/StackLayoutState.cs`
- `Layout/UniformGridLayoutState.cs`
- `Layout/WrapLayoutState.cs`
- `Layout/StackLayout.cs`
- `Layout/UniformGridLayout.cs`
- `Layout/WrapLayout.cs`
- `Layout/LayoutContext*.cs`
- `Layout/VirtualizingLayoutContext.cs`
- `Layout/NonVirtualizingLayoutContext.cs`

The main Uno-specific change there is the hosting boundary: the base layout types still derive from Uno layout primitives in [VirtualizingLayout.cs](/Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/ItemsRepeater.Uno/Layout/VirtualizingLayout.cs#L5) and [NonVirtualizingLayout.cs](/Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/ItemsRepeater.Uno/Layout/NonVirtualizingLayout.cs#L3). That is a reasonable platform integration boundary and not, by itself, a porting problem.

## Validation During Review

Successful during this review:

- `dotnet build /Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/ItemsRepeater.Uno/ItemsRepeater.Uno.csproj -c Release`
- `dotnet test /Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/tests/ItemsRepeater.Uno.UnitTests/ItemsRepeater.Uno.UnitTests.csproj -c Release`

One parallel build attempt hit a transient file lock on `obj/Release/net9.0/ref/ItemsRepeater.Uno.dll`; rerunning the build sequentially succeeded.

## Recommended Next Porting Order

1. Port the repeater runtime proper:
   - `ViewManager`
   - `ViewportManager`
   - `VirtualizationInfo`
   - the full `RepeaterLayoutContext`
2. Replace the current `ItemsRepeater` native-wrapper implementation with the migrated Avalonia control logic.
3. Port the missing templating/binding pieces:
   - `ItemTemplateWrapper`
   - `BindingEvaluator`
4. Replace the current selection shim with the original `InternalSelectionModel` path where the control depends on it.
5. Bring `RepeaterDataGrid` back closer to the original structure once the repeater/runtime layer is real.

## Bottom Line

This branch has a real layout-engine port, but it does not yet have a full `ItemsRepeater` port. The remaining gap is not cosmetic; it is the core repeater realization/viewport/template/runtime layer.
