# Physical Scrolling (Non-Logical Mode)

This article describes the non-logical scrolling path used by `ScrollViewer` when its content does **not** participate in `ILogicalScrollable` or has logical scrolling disabled. It also documents the legacy behavior `ItemsRepeater` used before logical scrolling was implemented.

## When physical scrolling is used

`ScrollViewer` uses physical scrolling when:

- The content does not implement `ILogicalScrollable`, **or**
- The content implements `ILogicalScrollable` but `IsLogicalScrollEnabled` is `false`.

In this mode, the scroll viewer treats the content as a large canvas and offsets the child bounds directly.

## How physical scrolling works

The scroll viewer drives scrolling through `ScrollContentPresenter`:

- **Extent/Viewport** are computed from content size and viewport size.
- **Offset** is applied by arranging the child at negative coordinates (`-Offset.X`, `-Offset.Y`).
- **EffectiveViewportChanged** updates are used for viewport notifications and virtualization.

The content does not receive logical scroll events; it only sees layout changes as its bounds are shifted by the presenter.

## Implications for virtualization

When the content does not implement `ILogicalScrollable`, viewport updates come from `EffectiveViewportChanged` rather than a logical offset. Virtualizing panels and controls must interpret the effective viewport themselves to decide what to realize.

This is how `ItemsRepeater` historically participated in scrolling:

- The repeater did not implement `ILogicalScrollable`.
- The parent `ScrollViewer` used physical scrolling.
- The repeater derived its viewport from `EffectiveViewportChanged`.

With the current logical scrolling implementation, this legacy path is still relevant for older versions or for custom content that opts out of logical scrolling.

## Comparison with logical scrolling

| Behavior | Physical scrolling | Logical scrolling |
| --- | --- | --- |
| Offset applied to child bounds | Yes | No |
| Viewport source | EffectiveViewportChanged | Logical offset + viewport |
| Scroll step size | Fixed pixel delta | `ScrollSize` / `PageScrollSize` |
| Used when | No logical scroll support | `ILogicalScrollable` enabled |

For how `ItemsRepeater` maps the logical scroll contract, see [Logical Scrolling (ILogicalScrollable)](logical-scrolling.md).
