# ItemsRepeater Uno Port Plan

## Goal

Add a new Uno Platform implementation in `src/ItemsRepeater.Uno/` and a mirrored Uno sample app in `samples/ItemsRepeaterUnoSample/`, following the TreeDataGrid Uno lane while preserving the current ItemsRepeater sample surfaces:

- `ItemsRepeater`
- `SelectingItemsRepeater`
- `Nested ItemsRepeater`
- `RepeaterDataGrid`

## Porting stance

This port should not re-implement Uno's own WinUI repeater engine when the platform already ships equivalent primitives. The Uno port therefore follows a hybrid strategy:

1. Reuse native Uno/WinUI primitives where they already match the Avalonia control surface closely.
2. Keep the Avalonia-facing namespaces (`Avalonia.Controls`, `Avalonia.Layout`, `Avalonia.Controls.DataGrid`) so the port stays easy to understand and future diffs remain localized.
3. Rebuild only the missing layers that do not exist in Uno out of the box:
   - `SelectingItemsRepeater`
   - `NonVirtualizingStackLayout`
   - `WrapLayout` property aliases over Uno `FlowLayout`
   - `RepeaterDataGrid`

## Source layout

- `src/ItemsRepeater.Uno/`
  - wrapper types over Uno repeater primitives
  - missing layout implementations
  - `SelectingItemsRepeater`
  - `RepeaterDataGrid`
  - Uno `Themes/Generic.xaml`
- `samples/ItemsRepeaterUnoSample/`
  - Uno single-project multi-head sample
  - the same four sample pages as the Avalonia sample
  - shared view models ported from the Avalonia sample

## Main design decisions

### 1. Base repeater surface

Use thin wrapper classes for:

- `Avalonia.Controls.ItemsRepeater`
- `Avalonia.Controls.RecyclePool`
- `Avalonia.Controls.RecyclingElementFactory`
- `Avalonia.Layout.StackLayout`
- `Avalonia.Layout.UniformGridLayout`

These types can directly inherit the Uno/WinUI equivalents because Uno already exposes:

- `ItemsRepeater`
- `StackLayout`
- `UniformGridLayout`
- `RecyclePool`
- `RecyclingElementFactory`

### 2. Missing layouts

- `NonVirtualizingStackLayout` remains custom because Uno exposes the non-virtualizing layout base but not the exact Avalonia helper layout.
- `WrapLayout` should map onto Uno `FlowLayout` with Avalonia-compatible property names:
  - `HorizontalSpacing` -> `MinColumnSpacing`
  - `VerticalSpacing` -> `MinRowSpacing`

### 3. Selection layer

Implement `SelectingItemsRepeater` on top of Uno `ItemsRepeater` plus Uno `SelectionModel`.

Required behavior:

- single and multiple selection
- click to select
- ctrl/cmd-style toggle support where available
- shift-range selection
- `SelectedIndex`
- `SelectedItem`
- `SelectionChanged`
- `AutoScrollToSelectedItem`
- attached `IsSelected` state for realized elements

### 4. RepeaterDataGrid

Keep `RepeaterDataGrid` as a custom Uno control built from:

- a static header presenter
- a scrolling `SelectingItemsRepeater` for rows
- column-width calculation compatible with:
  - absolute widths
  - star widths
  - auto widths
- row selection and cell selection
- row-height binding support

The Uno version should avoid introducing new reflection-heavy code paths. Dynamic cell text and row-height behavior should prefer WinUI bindings and realized-element measurement over custom reflection helpers.

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

## Full parity expansion

The initial Uno port shipped the sample-driven control surface. The parity expansion pass extends that with the broader Avalonia-facing API shape so downstream code is not forced onto raw Uno primitives.

Implemented additions:

- compatibility primitives in `Avalonia.*`:
  - `GridLength`
  - `GridUnitType`
  - `Point`
  - `Rect`
  - `Size`
  - `Vector`
  - `AvaloniaProperty.UnsetValue`
- compatibility collections:
  - `Avalonia.Collections.AvaloniaList<T>`
- compatibility binding and converter contracts:
  - `Avalonia.Data.BindingBase`
  - `Avalonia.Data.Binding`
  - `Avalonia.Data.Converters.IValueConverter`
  - `Avalonia.Data.Converters.IMultiValueConverter`
- compatibility templating contracts:
  - `Avalonia.Controls.Templates.IDataTemplate`
  - `Avalonia.Controls.Templates.FuncDataTemplate`
- compatibility selection contracts:
  - `Avalonia.Controls.SelectionMode`
  - `Avalonia.Controls.SelectionChangedEventArgs`
  - `Avalonia.Controls.Selection.ISelectionModel`
  - `Avalonia.Controls.Selection.SelectionModel<T>`
  - `Avalonia.Controls.Selection.SelectionModelIndexesChangedEventArgs`
  - `Avalonia.Controls.Selection.SelectionModelSelectionChangedEventArgs`
- expanded repeater surface:
  - custom `ItemsRepeater` event wrappers for prepared/clearing/index-changed callbacks
  - `IElementFactory`
  - `ElementFactory`
  - `RecyclePool`
  - `RecyclingElementFactory`
  - `SelectingItemsRepeater.SelectedValue`
  - `SelectingItemsRepeater.SelectedValueBinding`
  - `SelectingItemsRepeater.SelectedItems`
  - `SelectingItemsRepeater.Selection`
  - `SelectingItemsRepeater.WrapSelection`
  - `SelectingItemsRepeater.BeginInit()`
  - `SelectingItemsRepeater.EndInit()`
- expanded layout surface:
  - `Avalonia.Layout.Orientation`
  - `Avalonia.Layout.UniformGridLayoutItemsJustification`
  - `Avalonia.Layout.UniformGridLayoutItemsStretch`
  - Avalonia-facing enum/property mapping on `StackLayout`, `UniformGridLayout`, `WrapLayout`, and `NonVirtualizingStackLayout`
- expanded data grid surface:
  - `RepeaterDataGrid.Columns` now uses `AvaloniaList<T>`
  - `RepeaterDataGridColumn.Width` now uses Avalonia-compatible `GridLength`
  - `RepeaterDataGridConverters` are compiled and available in the Uno package

Tradeoff:

- Uno XAML does not honor the Avalonia-style `GridLength` string conversion automatically, so the Uno sample configures the `RepeaterDataGrid` column widths in code-behind instead of inline XAML strings. The library API remains Avalonia-shaped.

Additional validation:

- `dotnet test tests/ItemsRepeater.Uno.UnitTests/ItemsRepeater.Uno.UnitTests.csproj -c Release`

## Environment notes

- The Uno sample targets Android, iOS, WebAssembly, and desktop.
- Full multi-head validation still depends on local platform workloads and SDKs.
- Desktop is the fastest inner-loop target and should be the primary validation lane in this repository.
