# SelectingItemsRepeater Internals

This article describes the internal architecture of `SelectingItemsRepeater`. It complements `selection.md` with implementation-level behavior.

## Responsibilities

`SelectingItemsRepeater` adds selection on top of `ItemsRepeater`:

- Manages an `ISelectionModel` (default: `RepeaterSelectionModel`).
- Synchronizes selection properties (`SelectedIndex`, `SelectedItem`, `SelectedItems`, `SelectedValue`).
- Applies selection state to realized containers.
- Handles pointer and keyboard input.
- Optionally scrolls selected items into view.

## Selection model lifecycle

`SelectingItemsRepeater` owns a selection model instance:

1. **Creation**:
   - `GetOrCreateSelectionModel` creates a `RepeaterSelectionModel` if none is set.
   - `SelectionMode` is mapped to `SingleSelect` or `Multiple` on the model.
2. **Initialization**:
   - `InitializeSelectionModel` wires `PropertyChanged`, `SelectionChanged`, and `LostSelection`.
   - The model `Source` is set to `ItemsSourceView.Source`.
3. **Deinitialization**:
   - When `Selection` is replaced, event handlers are removed from the old model.

If a custom `Selection` is provided, it must not already have a `Source`.

## UpdateState batching

When `ItemsSource` changes, selection properties are updated in a batch:

- `BeginUpdating` stores pending changes in `UpdateState`.
- `EndUpdating` replays the pending selection changes in order:
  - `Selection`, `SelectedItems`, `SelectedValue`, `SelectedIndex`, `SelectedItem`.
- This prevents inconsistent intermediate states and avoids recursive updates.

`AlwaysSelected` enforces a selection when the collection is non-empty.

## Container selection pipeline

Selection is applied to realized containers:

1. On `ElementPrepared`:
   - The container is made focusable (if not already).
   - The container is registered for `PropertyChanged`.
   - Selection state is applied (`ApplyContainerSelection`).
2. On `ElementClearing`:
   - The container is unregistered.
   - Selection state is cleared.
3. On `ElementIndexChanged`:
   - Selection state is re-applied using the new index.

`IsSelectedManaged` is an attached property that prevents re-entrant selection updates when the repeater itself is setting `IsSelected`.

## Pseudo-classes and IsSelected

When a container's `IsSelected` property changes:

- `:selected` pseudo-class is set on the container.
- If the change was user-driven (not managed), the selection model is updated.

This keeps template styling and model state in sync.

## Input handling

Selection can be triggered by pointer or keyboard:

- `OnPointerPressed` / `OnPointerReleased` uses `ShouldTriggerSelection` to decide when to act.
- `OnKeyDown` handles:
  - Arrow navigation and range selection.
  - `Ctrl+A` selection when allowed.
  - Space/Enter selection toggles.

Modifiers are resolved using platform hotkey configuration:

- Range selection uses `SelectionModifiers`.
- Toggle selection uses `CommandModifiers`.

## UpdateSelection logic

Selection updates follow a strict order:

1. Determine selection mode flags (multiple, toggle, always selected).
2. If range selection is active, use `Selection.SelectRange`.
3. If toggle is active, select or deselect the target index.
4. Otherwise, clear and select the single index.

Updates use `Selection.BatchUpdate()` to reduce churn and consolidate events.

## Auto scroll to selection

If `AutoScrollToSelectedItem` is true:

- The control listens for `SelectionModel.AnchorIndex` changes.
- It schedules `ScrollIntoView` using the anchor index or selected index.
- `ScrollIntoView` calls `BringIntoView` on the realized container or creates one if needed.

This keeps selection visible during keyboard navigation and programmatic updates.

## SelectionChanged event

`SelectionChangedEvent` is raised after `SelectionModel.SelectionChanged`:

- It uses the routed event system for consistent bubbling.
- It provides arrays of added and removed items.
- It is suppressed during internal selection update loops to avoid double raises.

## Summary

`SelectingItemsRepeater` bridges model-driven selection with container state and input handling. It maintains strict ordering of selection updates, synchronizes styling and model state, and integrates selection with scrolling and virtualization.
