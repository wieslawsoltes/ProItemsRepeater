# Focus and Keyboard Navigation

This article describes focus behavior and keyboard navigation in `ItemsRepeater` and `SelectingItemsRepeater`.

## Tab navigation behavior

`ItemsRepeater` sets `KeyboardNavigation.TabNavigation` to `Once`. This means:

- The repeater participates as a single tab stop.
- Once focus enters the repeater, arrow keys and selection behavior determine navigation within items.

If you need different behavior, you can override `KeyboardNavigation.TabNavigation` in your template or style.

## Focusable containers

`ItemsRepeater` does not force item containers to be focusable. In `SelectingItemsRepeater`, containers are made focusable on `ElementPrepared` if they do not explicitly set `Focusable`.

To control focus behavior:

- Set `Focusable="False"` on your item template root to opt out.
- Set `Focusable="True"` to ensure keyboard navigation can land on items.

## Focus pinning and virtualization

Focused elements are pinned so they are not recycled while focused:

- The view manager tracks the focused element.
- When focus changes, the new element is pinned and the old one is unpinned.
- Unpinning triggers a measure invalidation so the element can be recycled.

This prevents focus from being lost due to virtualization, but it can keep off-screen items alive while focused.

## Selection and keyboard input

`SelectingItemsRepeater` handles keyboard input:

- Arrow keys move selection and focus.
- Shift modifies range selection.
- Toggle selection uses the platform command modifier.

Selection logic works best when containers are focusable and can receive key events.

## Related docs

- `selection.md`
- `selecting-itemsrepeater-internals.md`
