# ItemsRepeater Uno Port Plan

## Goal

Add a new Uno Platform implementation in `src/ItemsRepeater.Uno/` and a mirrored Uno sample app in `samples/ItemsRepeaterUnoSample/`, following the TreeDataGrid Uno lane while preserving the current ItemsRepeater sample surfaces:

- `ItemsRepeater`
- `SelectingItemsRepeater`
- `Nested ItemsRepeater`
- `RepeaterDataGrid`

## Porting stance

This port must be a true source migration of the Avalonia implementation, not a wrapper layer over Uno's existing repeater controls.

Required rules for the Uno lane:

1. Start from the original source files in `src/Avalonia.Controls.ItemsRepeater/`.
2. Copy the original Avalonia code into `src/ItemsRepeater.Uno/` on a one-to-one basis wherever practical.
3. Replace only the framework-boundary pieces that are incompatible with Uno:
   - Avalonia property system calls
   - Avalonia visual tree and input APIs
   - Avalonia layout invalidation and scrolling APIs
   - Avalonia template/style application APIs
4. Do not keep thin subclasses over `Microsoft.UI.Xaml.Controls.ItemsRepeater`, `StackLayout`, `UniformGridLayout`, `FlowLayout`, or related native repeater primitives as the implementation strategy.
5. Uno-native types may still be used at the boundary where the port must integrate with the platform, but the control logic, layout logic, realization logic, and repeater state management should come from the migrated Avalonia source.

This means the Uno project needs to carry the original repeater infrastructure:

- `ItemsRepeater`
- `ViewManager`
- `ViewportManager`
- `RepeaterLayoutContext`
- `VirtualizationInfo`
- source-ported layout algorithms and state objects
- source-ported selection internals where the original control depends on them

## Source layout

- `src/ItemsRepeater.Uno/`
  - migrated repeater control source from the Avalonia project
  - migrated layout engine source from the Avalonia project
  - migrated selection and virtualization helper source as needed
  - Uno compatibility shims only where direct Avalonia APIs do not exist
  - Uno `Themes/Generic.xaml`
- `samples/ItemsRepeaterUnoSample/`
  - Uno single-project multi-head sample
  - the same four sample pages as the Avalonia sample
  - shared view models ported from the Avalonia sample

## Migration sequence

### 1. Port missing core source files first

Bring over the original Avalonia files that currently do not exist in the Uno project:

- `Controls/ItemTemplateWrapper.cs`
- `Controls/ItemsRepeater.LogicalScrollable.cs`
- `Controls/RepeaterLayoutContext.cs`
- `Controls/UniqueIdElementPool.cs`
- `Controls/ViewManager.cs`
- `Controls/ViewportManager.cs`
- `Controls/VirtualizationInfo.cs`
- `Diagnostics/ItemsRepeaterDiagnostics.cs`
- `Layout/ElementManager.cs`
- `Layout/FlowLayoutAlgorithm.cs`
- `Layout/IFlowLayoutAlgorithmDelegates.cs`
- `Layout/LayoutContextAdapter.cs`
- `Layout/OrientationBasedMeasures.cs`
- `Layout/StackLayoutState.cs`
- `Layout/UniformGridLayoutState.cs`
- `Layout/Utils/ListUtils.cs`
- `Layout/UvBounds.cs`
- `Layout/UvMeasure.cs`
- `Layout/VirtualLayoutContextAdapter.cs`
- `Layout/WrapItem.cs`
- `Layout/WrapLayoutState.cs`
- `Selection/InternalSelectionModel.cs`
- `Utils/BindingEvaluator.cs`

### 2. Replace wrapper-based layout types

The current Uno implementations of these types should be replaced with migrated Avalonia logic:

- `ItemsRepeater`
- `NonVirtualizingStackLayout`
- `SelectingItemsRepeater`

Status after the current corrective pass:

- `StackLayout` is now on the migrated Avalonia flow-layout path.
- `UniformGridLayout` is now on the migrated Avalonia flow-layout path.
- `WrapLayout` is now on the migrated Avalonia flow-layout path.
- `ItemsRepeater` and its realization/runtime infrastructure still need the same source-port treatment.

### 3. Adapt Avalonia API boundaries to Uno

Expected adaptation zones:

- `AvaloniaProperty` / `StyledProperty` / `DirectProperty` to Uno dependency properties or narrow compatibility helpers
- `Layoutable`, `Control`, `Panel`, and templated-control lifecycle to Uno equivalents
- `InvalidateMeasure`, `InvalidateArrange`, `SizeChanged`, and viewport updates to Uno layout lifecycle
- `ScrollViewer`, bring-into-view, and scroll offset handling to Uno scrolling APIs
- Avalonia visual tree traversal to Uno visual tree traversal
- Avalonia pointer/keyboard events to Uno input events

### 4. Keep the sample and DataGrid on top of the ported repeater

`RepeaterDataGrid` and the sample pages should consume the migrated Uno repeater implementation, not a wrapper over Uno-native repeater controls.

## Sample scope

The Uno sample must include the same user-facing pages and actions as the Avalonia sample:

1. `ItemsRepeater`
   - add/remove/reset items
   - randomize widths/heights
   - switch between stack, non-virtualizing stack, uniform grid, and wrap layouts
   - scroll to last/random/selected
2. `SelectingItemsRepeater`
   - the same layout switching
   - click and range selection behavior
   - scroll-to-selection actions
3. `Nested ItemsRepeater`
   - outer scrolling with nested repeater surfaces
4. `RepeaterDataGrid`
   - large dataset
   - row selection
   - cell selection
   - row-height randomization

## Validation targets

- `dotnet build src/ItemsRepeater.Uno/ItemsRepeater.Uno.csproj -c Release`
- `dotnet build samples/ItemsRepeaterUnoSample/ItemsRepeaterUnoSample.csproj -c Release -f net9.0-desktop`
- `cd samples/ItemsRepeaterUnoSample && dotnet run -c Release -f net9.0-desktop -- --exit`

## Current corrective direction

The existing Uno branch already added compatibility helpers and sample coverage, but it still diverges from the required source-port architecture because several core repeater and layout files are missing and some public types are thin wrappers over Uno-native implementations.

The corrective work must now:

1. Keep the migrated layout engine as the active implementation path.
2. Replace the remaining wrapper-based repeater/runtime implementations with migrated Avalonia source.
3. Expand the compatibility layer only where the migrated source genuinely needs it.
4. Keep validation green while moving one subsystem at a time:
   - repeater realization engine
   - selection layer
   - datagrid and sample consumers

## Environment notes

- The Uno sample targets Android, iOS, WebAssembly, and desktop.
- Full multi-head validation still depends on local platform workloads and SDKs.
- Desktop is the fastest inner-loop target and should be the primary validation lane in this repository.
