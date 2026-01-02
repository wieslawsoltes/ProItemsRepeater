# Scrolling Internals

This article explains how `ItemsRepeater` participates in scrolling without being a scrollable control itself. It focuses on the internal coordination between `ItemsRepeater`, the layout system, and the parent scroller.

## ItemsRepeater is not a scroller

`ItemsRepeater` derives from `Panel` and does not implement `ILogicalScrollable` or `IScrollable`. It relies on an ancestor scroller (usually `ScrollViewer`) to provide scroll offsets and viewport notifications. Because of that:

- You must wrap `ItemsRepeater` in a scroll viewer to get scrolling.
- `ItemsRepeater` supplies virtualization and anchoring, not scrollbars or offset management.

## Viewport-driven virtualization

When scrolling is available, `ItemsRepeater` tracks the *effective viewport* and uses it to compute:

- The **visible window**, which represents the on-screen viewport in layout coordinates.
- The **realization window**, which is the visible window expanded by cache buffers.

The realization window is the primary input to virtualization-aware layouts. Only items that intersect this window are realized (created and measured), which keeps UI generation proportional to what is visible.

## How the scroller is discovered

`ItemsRepeater` delegates viewport management to `ViewportManager`. The manager walks up the visual tree and looks for an `IScrollAnchorProvider` (usually `ScrollViewer`). When one is found, it:

- Subscribes to `EffectiveViewportChanged` to receive viewport updates.
- Uses scroll anchoring to stabilize content across layout changes.

If no scroller is found, the repeater still performs layout, but viewport-driven virtualization and anchoring are limited.

## Measure/arrange integration

The scrolling pipeline is integrated into layout passes:

1. **Measure**:
   - `ViewportManager` is notified at the start of measure.
   - The visible and realization windows are queried.
   - The layout measures only what intersects the realization window.
   - The layout extent is reported back to `ViewportManager`.

2. **Arrange**:
   - Realized elements are arranged.
   - `ViewportManager` registers realized elements as scroll anchor candidates.
   - Cache buffers are gradually expanded toward the configured cache lengths.

This keeps realized content stable while scrolling and ensures virtualization is consistent with the current viewport.

## Cache length and buffer growth

`ItemsRepeater.HorizontalCacheLength` and `ItemsRepeater.VerticalCacheLength` are multipliers of the current viewport size. They define how far the realization window extends beyond the visible window.

Instead of jumping directly to the full cache size, `ViewportManager` inflates the cache buffers in small steps per arrange. This reduces layout thrash while the user scrolls quickly and keeps memory usage predictable.

## Scroll anchoring and stability

Scroll anchoring keeps the user's content from jumping when items are inserted, removed, or resized. `ViewportManager` registers realized children as anchor candidates with the scroller. The scroller chooses one anchor to keep stable as it adjusts offsets.

Anchoring is automatically disabled in some cases:

- Certain layouts (notably `WrapLayout`) can produce unstable anchoring; it is disabled to avoid incorrect jumps.
- Large viewport jumps also disable anchoring temporarily to prevent locking to a stale anchor.

## Bring-into-view behavior

`ItemsRepeater` listens for `RequestBringIntoViewEvent` and forwards it to `ViewportManager`. The manager:

- Finds the immediate child of the repeater that is being brought into view.
- Temporarily registers only that element as an anchor candidate.
- Restores normal anchoring after rendering completes.

This ensures the item being brought into view stays stable and becomes visible without being displaced by other anchors.

## Non-virtualizing layouts

When the layout is non-virtualizing, viewport management is disabled. In that mode:

- Effective viewport updates are ignored.
- The realization window is not used to drive element generation.
- The repeater behaves like a normal panel that realizes all items.

## Practical usage

To enable virtualization and scrolling, combine `ItemsRepeater` with a scroll viewer and a virtualizing layout:

```xml
<ScrollViewer HorizontalScrollBarVisibility="Auto"
              VerticalScrollBarVisibility="Auto">
  <ItemsRepeater ItemsSource="{Binding Items}">
    <ItemsRepeater.Layout>
      <StackLayout Orientation="Vertical" />
    </ItemsRepeater.Layout>
  </ItemsRepeater>
</ScrollViewer>
```

To programmatically bring an item into view:

```csharp
var element = repeater.GetOrCreateElement(index);
element.BringIntoView();
```

`GetOrCreateElement` realizes the item if needed, and the subsequent layout pass uses the viewport system to scroll it into view.
