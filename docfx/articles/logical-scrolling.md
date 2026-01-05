# Logical Scrolling (ILogicalScrollable)

This article describes how `ItemsRepeater` implements `ILogicalScrollable` and how that changes the scrolling pipeline when the repeater is hosted inside a `ScrollViewer`.

## Overview

`ItemsRepeater` is still a `Panel` and does not render scrollbars, but it *does* implement `ILogicalScrollable`. When placed inside a `ScrollViewer`, the scroll viewer detects the interface and switches to logical scrolling:

- Scroll offsets, extent, and viewport are read from the repeater.
- Scroll input is quantized using the repeater-provided logical sizes.
- The child is not physically offset; the repeater updates its viewport internally.

This is different from physical scrolling, where the scroll viewer treats content as a large canvas and offsets the child bounds directly.

## Contract mapping in ItemsRepeater

`ILogicalScrollable` extends `IScrollable`. `ItemsRepeater` implements the following behaviors:

- `Extent` and `Viewport`: taken from layout size and available size, expressed in layout coordinates (DIPs).
- `Offset`: stored in the repeater and clamped to `Extent - Viewport`. The offset is interpreted in the same coordinate space as the layout.
- `ScrollSize`: an estimated logical step size based on realized items and layout spacing.
- `PageScrollSize`: equal to the current `Viewport`.
- `CanHorizontallyScroll` / `CanVerticallyScroll`: assigned by the scroll viewer based on scrollbar visibility. Disabled axes force offsets to zero.
- `ScrollInvalidated`: raised whenever any scroll-related value changes.
- `BringIntoView`: computes a target rect and updates offset if the rect is outside the viewport.

## Scroll size calculation

`ItemsRepeater` computes `ScrollSize` from realized elements:

1. Average the realized child bounds (or desired sizes) to estimate item width/height.
2. Cache the value for reuse until invalidated.
3. If no realized elements are available:
   - Use `UniformGridLayout.MinItemWidth/MinItemHeight`, or
   - Fall back to a default of `50 x 50`.
4. Adjust the value for layout-specific spacing:
   - `StackLayout` / `NonVirtualizingStackLayout`: add `Spacing` along the scroll axis.
   - `WrapLayout`: add `VerticalSpacing` or `HorizontalSpacing` depending on orientation.
   - `UniformGridLayout`: add `MinRowSpacing` or `MinColumnSpacing` depending on orientation.

This gives the scroll viewer a stable step size for wheel and gesture input without requiring fully realized content.

## Viewport integration

When the scroll viewer updates the logical offset, the repeater calls:

- `ViewportManager.UpdateViewportFromLogicalScroll(viewport, offset, invalidateMeasure)`

This converts the logical offset into the visible window used by virtualization. Because the viewport is driven by the logical scrollable, `ItemsRepeater` does not subscribe to `EffectiveViewportChanged` for this path.

## Logical vs physical scrolling

| Behavior | Logical scrolling | Physical scrolling |
| --- | --- | --- |
| Offset applied to child bounds | No | Yes |
| Viewport source | Logical offset + viewport | EffectiveViewportChanged |
| Scroll step size | `ScrollSize` / `PageScrollSize` | Fixed pixel delta |
| Used when | Content implements `ILogicalScrollable` | Otherwise |

For `ItemsRepeater`, logical scrolling provides scroll steps tied to item sizing and keeps virtualization in the repeater's layout coordinate space.

## Practical notes

- Logical scrolling is automatic when `ItemsRepeater` is hosted in a `ScrollViewer`.
- You still need a `ScrollViewer` to display scrollbars and manage user input.
- Large changes in extent or layout invalidate the cached scroll size to keep steps accurate.
