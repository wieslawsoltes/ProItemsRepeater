# Diagnostics and Metrics

ItemsRepeater emits OpenTelemetry-friendly diagnostics on .NET 6+.

## ActivitySource

- Name: `Avalonia.ItemsRepeater`
- Activities: `ItemsRepeater.Measure`, `ItemsRepeater.Arrange`, `ItemsRepeater.Generate`

Each activity includes tags such as layout id, item count, and viewport details.

## Meter

- Name: `Avalonia.ItemsRepeater`
- Metric instruments:
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

## OpenTelemetry Setup Example

```csharp
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

Sdk.CreateTracerProviderBuilder()
    .AddSource("Avalonia.ItemsRepeater")
    .Build();

Sdk.CreateMeterProviderBuilder()
    .AddMeter("Avalonia.ItemsRepeater")
    .Build();
```
