# Introduction

ProItemsRepeater brings the WinUI ItemsRepeater control to Avalonia. It is a hard fork of the retired `Avalonia.Controls.ItemsRepeater` and targets Avalonia 11+. The core idea is simple: a data-driven panel delegates measure/arrange to a layout while virtualizing and recycling item elements.

## Why ItemsRepeater

ItemsRepeater is built for scenarios where you want:

- Full control over layout and virtualization behavior.
- High performance for large or fast-changing data sets.
- Reuse of visual elements across scrolls and collection updates.
- Selection behavior without the heavier ItemsControl templating pipeline.

## Key Concepts

- **Layout-first**: you assign a layout (`StackLayout`, `WrapLayout`, `UniformGridLayout`, or a custom layout) that sizes and positions items.
- **Virtualization**: only items inside the realization window are created; cache length expands around the viewport.
- **Recycling**: templates can be backed by `RecyclingElementFactory` and `RecyclePool` to reuse elements efficiently.
- **Selection**: `SelectingItemsRepeater` layers selection on top of ItemsRepeater with `SelectedItem`, `SelectedItems`, and `SelectionMode`.
- **Diagnostics**: OpenTelemetry-friendly ActivitySource and Meter events expose measure/arrange/generate timing and realized counts.

## Where It Fits

Use ItemsRepeater when you need a custom layout or high-performance virtualization. Use ItemsControl (or controls like ListBox) when you want built-in styling and behaviors with less manual layout configuration.
