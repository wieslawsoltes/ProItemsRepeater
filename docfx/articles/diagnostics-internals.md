# Diagnostics Internals

This article describes how `ItemsRepeater` emits diagnostic events and metrics. It complements `diagnostics.md` with implementation details.

## ActivitySource and Meter

`ItemsRepeaterDiagnostics` declares:

- `ActivitySourceName`: `Avalonia.ItemsRepeater`
- `MeterName`: `Avalonia.ItemsRepeater`

Both are created only on .NET 6+ builds (`NET6_0_OR_GREATER`).

## Measure and arrange activities

### Measure

`ItemsRepeater.MeasureOverride` calls:

- `ItemsRepeaterDiagnostics.StartMeasure(...)`

Activity tags include:

- `layout.id`
- `items.count`
- `available.width`, `available.height`
- `realization.x`, `realization.y`, `realization.width`, `realization.height`
- `visible.x`, `visible.y`, `visible.width`, `visible.height`

### Arrange

`ItemsRepeater.ArrangeOverride` calls:

- `ItemsRepeaterDiagnostics.StartArrange(...)`

Activity tags include:

- `layout.id`
- `items.count`
- `final.width`, `final.height`

## Generate activity

`FlowLayoutAlgorithm.Generate` wraps each forward/backward generation pass with:

- `ItemsRepeaterDiagnostics.StartGenerate(...)`

Activity tags include:

- `layout.id`
- `direction` (`forward` / `backward`)
- `wrapping` (bool)
- `anchor.index`

## Metrics and tags

`ItemsRepeaterDiagnostics` records metrics via counters and histograms:

- `itemsrepeater.measure.count`
- `itemsrepeater.measure.duration.ms`
- `itemsrepeater.arrange.count`
- `itemsrepeater.arrange.duration.ms`
- `itemsrepeater.generate.pass`
- `itemsrepeater.generate.duration.ms`
- `itemsrepeater.generate.measured.count`
- `itemsrepeater.element.realized`
- `itemsrepeater.element.recycled`
- `itemsrepeater.anchor.make`

### Measure/arrange tags

Both measure and arrange metrics include:

- `layout.id`
- `items.count`
- `realized.count`

### Generate tags

Generate metrics include:

- `layout.id`
- `direction`
- `wrapping`
- `disable.virtualization`

## When metrics are recorded

The control records metrics at well-defined points:

- **Measure/Arrange**: after the pass completes and the realized count is computed.
- **Generate**: after a forward/backward pass, with the measured element count.
- **Element realized**: when a new element is created by the view manager.
- **Element recycled**: when an element is cleared by the repeater.
- **Make anchor**: when an explicit anchor is created or when the algorithm makes one.

## Timing helpers

`ItemsRepeaterDiagnostics` uses `Stopwatch.GetTimestamp()` and converts to milliseconds with:

```
(Stopwatch.GetTimestamp() - start) * 1000 / Stopwatch.Frequency
```

This provides high-resolution timings without allocations.

## Listener behavior

Activities are only created if the `ActivitySource` has listeners. This avoids the overhead of tag allocation and activity creation when tracing is disabled.

## Summary

Diagnostics are split into tracing (activities) and metrics (counters/histograms). Tags are attached consistently so you can correlate layout passes, generation behavior, and element churn with layout and scroll behavior.
