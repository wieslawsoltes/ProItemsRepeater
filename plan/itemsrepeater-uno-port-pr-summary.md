# ItemsRepeater Uno Port PR Summary

## Branch

- `itemsrepeater-uno-port-granular`

## Commit Stack

1. `d4c38b1` `Add Uno port plan and solution entries`
2. `c00bfcd` `Add Uno ItemsRepeater library port`
3. `079ed7e` `Add Uno sample app for ItemsRepeater`
4. `0319b01` `Add Uno unit tests for compatibility layer`
5. `2861bfe` `Improve Uno datagrid parity and selection behavior`

## Purpose

This branch ports `Avalonia.Controls.ItemsRepeater` to Uno Platform and shifts the implementation toward a real source migration from the Avalonia codebase instead of relying on thin wrappers over Uno-native repeater/layout controls. The branch includes:

- a new Uno library project under `src/ItemsRepeater.Uno`
- a Uno sample application under `samples/ItemsRepeaterUnoSample`
- compatibility shims for Avalonia-shaped APIs used by the original control set
- source-ported layout engine infrastructure from the Avalonia implementation
- parity work for selection, layouts, and `RepeaterDataGrid`
- unit coverage for the compatibility layer

## High-Level Scope

### 1. Port plan and solution integration

The branch introduces the detailed Uno port plan and wires the new projects into the repository structure:

- `plan/itemsrepeater-uno-port-plan.md`
- `src/ItemsRepeater.Uno/ItemsRepeater.Uno.csproj`
- `samples/ItemsRepeaterUnoSample/ItemsRepeaterUnoSample.csproj`
- solution entries in the repo solution file

### 2. New Uno library

The Uno library mirrors the original control areas:

- `Controls`
  - `ItemsRepeater`
  - `SelectingItemsRepeater`
  - element factory wrappers and event args
- `Layout`
  - stack, non-virtualizing stack, uniform grid, wrap, and base layout abstractions
- `DataGrid`
  - `RepeaterDataGrid`
  - `RepeaterDataGridColumn`
  - `RepeaterDataGridRowControl`
  - supporting converters and cell info
- `Compatibility`
  - Avalonia-shaped collection, data, property, templating, primitive, and selection helpers

The implementation uses Uno/WinUI only at the framework boundary where Avalonia APIs do not exist. The migrated control logic is intended to come from the Avalonia source rather than from native Uno wrappers.

### 3. Source-port corrective work

The branch now includes a real migrated layout-engine slice from the Avalonia implementation:

- `src/ItemsRepeater.Uno/Layout/FlowLayoutAlgorithm.cs`
- `src/ItemsRepeater.Uno/Layout/ElementManager.cs`
- `src/ItemsRepeater.Uno/Layout/StackLayoutState.cs`
- `src/ItemsRepeater.Uno/Layout/UniformGridLayoutState.cs`
- `src/ItemsRepeater.Uno/Layout/WrapLayoutState.cs`
- `src/ItemsRepeater.Uno/Layout/IFlowLayoutAlgorithmDelegates.cs`
- `src/ItemsRepeater.Uno/Layout/UvBounds.cs`
- `src/ItemsRepeater.Uno/Layout/UvMeasure.cs`

And replaces wrapper-based layout implementations with migrated Avalonia logic adapted to Uno APIs:

- `src/ItemsRepeater.Uno/Layout/StackLayout.cs`
- `src/ItemsRepeater.Uno/Layout/UniformGridLayout.cs`
- `src/ItemsRepeater.Uno/Layout/WrapLayout.cs`

This is the main architectural correction on the branch. The layout surface is no longer implemented as thin subclasses of native Uno layout types.

## Parity Work Added After the Initial Port

### ItemsRepeater compatibility surface

`src/ItemsRepeater.Uno/Controls/ItemsRepeater.cs`

- Adds the Avalonia-shaped `IsLogicalScrollEnabled` compatibility property.
- Keeps template normalization so Avalonia-style `IDataTemplate` values can be used on the Uno side.

This control is still on the earlier wrapper-based path and is the next major source-port target together with:

- `ViewManager`
- `ViewportManager`
- `VirtualizationInfo`
- the full `RepeaterLayoutContext`

### SelectingItemsRepeater parity

`src/ItemsRepeater.Uno/Controls/SelectingItemsRepeater.cs`

- Adds right-click selection behavior closer to the Avalonia control.
- Adds `Ctrl+A` handling for multi-selection scenarios.
- Preserves the Avalonia-style selection model surface (`Selection`, `SelectedItems`, `SelectedValue`, `SelectedValueBinding`, `WrapSelection`).

### RepeaterDataGrid parity and behavior

`src/ItemsRepeater.Uno/DataGrid/RepeaterDataGrid.cs`
`src/ItemsRepeater.Uno/DataGrid/RepeaterDataGridColumn.cs`
`src/ItemsRepeater.Uno/DataGrid/RepeaterDataGridRowControl.cs`

This remains the main custom-control adaptation area. The branch includes:

- tracked realized-row bookkeeping instead of loose row scanning
- column property observation through `INotifyPropertyChanged`
- recalculation of column indices and actual widths when columns change
- explicit auto-column measurement queueing
- header height calculation and deferred header-width refresh
- auto-width refresh based on realized row content
- row and cell selection visual updates for realized rows
- reduced per-row bind churn through cached cell structure and cached selection state

The port keeps the Avalonia-style top-level design:

- header stays separate from body scrolling
- body uses a single `ScrollViewer`
- rows are still realized through `SelectingItemsRepeater`

## Sample Application

`samples/ItemsRepeaterUnoSample`

The sample mirrors the Avalonia sample surfaces:

- `ItemsRepeaterPage`
- `RepeaterDataGridPage`
- `NestedItemsRepeaterPage`
- `SelectingItemsRepeaterPage`

It includes desktop, WebAssembly, Android, and iOS project scaffolding in the Uno sample structure, with the desktop target used for local validation in this branch.

## Compatibility Infrastructure

The corrective source-port work also adds the minimum compatibility needed to host migrated Avalonia code on Uno:

- `src/ItemsRepeater.Uno/Compatibility/AvaloniaPrimitives.cs`
- `src/ItemsRepeater.Uno/Compatibility/LayoutableExtensions.cs`
- `src/ItemsRepeater.Uno/Compatibility/AvaloniaLogging.cs`
- `src/ItemsRepeater.Uno/Diagnostics/ItemsRepeaterDiagnostics.cs`
- `src/ItemsRepeater.Uno/Controls/RepeaterLayoutContext.cs`

## Tests

`tests/ItemsRepeater.Uno.UnitTests`

Coverage currently focuses on the compatibility layer and selection/model behaviors that can be exercised safely in plain xUnit without requiring a full Uno UI dispatcher-hosted control tree.

## Validation Performed

The following commands were run successfully on this branch:

```bash
dotnet build /Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/src/ItemsRepeater.Uno/ItemsRepeater.Uno.csproj -c Release
dotnet test /Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/tests/ItemsRepeater.Uno.UnitTests/ItemsRepeater.Uno.UnitTests.csproj -c Release
dotnet build /Users/wieslawsoltes/GitHub/Avalonia.Controls.ItemsRepeater/samples/ItemsRepeaterUnoSample/ItemsRepeaterUnoSample.csproj -c Release -f net9.0-desktop
dotnet run -c Release -f net9.0-desktop -- --exit
```

The final `dotnet run` command was executed from:

- `samples/ItemsRepeaterUnoSample`

## Known Review Focus Areas

These areas deserve extra scrutiny during PR review:

1. The remaining wrapper-based repeater/runtime layer in `ItemsRepeater` and `SelectingItemsRepeater`.
2. `RepeaterDataGrid` scroll/perf behavior on Uno desktop and iOS heads.
3. The Avalonia compatibility layer shape versus the truly required public API surface.
4. Whether additional UI-threaded integration tests should be added for control-level runtime behavior beyond the current xUnit compatibility coverage.

## Suggested PR Narrative

Recommended framing for the PR:

- add the initial Uno port lane and sample app
- add compatibility infrastructure needed to preserve the Avalonia-shaped API
- replace wrapper-based layout implementations with migrated Avalonia layout engine code
- add parity fixes for selection and datagrid behavior
- validate the port through library build, unit tests, sample build, and desktop sample launch

## Notes

- This markdown file is intentionally left uncommitted.
