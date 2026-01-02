# Selection

`SelectingItemsRepeater` adds selection handling on top of ItemsRepeater. It integrates with Avalonia's `ISelectionModel` and supports single or multiple selection.

## Basic Usage

```xml
<SelectingItemsRepeater ItemsSource="{Binding Items}"
                        SelectionMode="Multiple"
                        SelectedItem="{Binding SelectedItem}">
  <SelectingItemsRepeater.Layout>
    <StackLayout Orientation="Vertical" Spacing="4" />
  </SelectingItemsRepeater.Layout>
</SelectingItemsRepeater>
```

## Selection Properties

- `SelectedIndex` and `SelectedItem`: two-way bindings for the current selection.
- `SelectedItems`: list for multi-selection scenarios.
- `SelectedValue` and `SelectedValueBinding`: bind to a specific property on the selected item.
- `Selection`: supply a custom `ISelectionModel` when you manage selection externally.
- `SelectionMode`: flags that control single vs. multiple selection and toggle behavior.
- `WrapSelection`: wrap keyboard navigation at the ends.
- `AutoScrollToSelectedItem`: automatically bring newly selected items into view (default `true`).

## Styling Selected Items

Selection uses the `:selected` pseudo-class and the `SelectingItemsRepeater.IsSelected` attached property on the container element.

```xml
<Style Selector="SelectingItemsRepeater Border:selected">
  <Setter Property="Background" Value="#2563EB" />
</Style>

<Style Selector="SelectingItemsRepeater Border:selected TextBlock">
  <Setter Property="Foreground" Value="White" />
</Style>
```

## Input Behavior

SelectingItemsRepeater handles pointer and keyboard input by default. Shift-click extends range selection and modifier keys toggle selection when `SelectionMode` enables multiple selection.
