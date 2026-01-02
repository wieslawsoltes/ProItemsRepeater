using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using Xunit;
using Xunit.Abstractions;

namespace Avalonia.Controls.UnitTests;

public class ItemsRepeaterDiagnosticsTests
{
    private readonly ITestOutputHelper _output;

    public ItemsRepeaterDiagnosticsTests(ITestOutputHelper output) => _output = output;

    [AvaloniaFact]
    public void Diagnostics_Emit_Metrics_And_Activities_On_Scroll()
    {
        using var listener = new RepeaterDiagnosticsListener();

        var itemCount = ReadIntEnvironmentVariable("REPEATER_DIAGNOSTICS_ITEM_COUNT", 500);
        var scrollOffsets = ReadOffsetsEnvironmentVariable(
            "REPEATER_DIAGNOSTICS_OFFSETS",
            new[] { 1200.0, 2400.0 });
        var heightPattern = ReadOffsetsEnvironmentVariable(
            "REPEATER_DIAGNOSTICS_HEIGHTS",
            new[] { 20.0, 30.0, 40.0, 50.0, 60.0, 70.0, 80.0 });

        var items = Enumerable.Range(0, itemCount)
            .Select(i => new SizedItem(40, heightPattern[i % heightPattern.Length]))
            .ToList();

        var repeater = new ItemsRepeater
        {
            Layout = new WrapLayout
            {
                Orientation = Orientation.Horizontal,
                HorizontalSpacing = 5,
                VerticalSpacing = 5
            },
            ItemsSource = items,
            HorizontalCacheLength = 0,
            VerticalCacheLength = 0,
            ItemTemplate = new FuncDataTemplate<SizedItem>((item, _) => new Border
            {
                Width = item.Width,
                Height = item.Height
            })
        };

        var scroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = repeater
        };

        var window = new Window
        {
            Width = 220,
            Height = 140,
            Content = scroller
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        _ = repeater.GetOrCreateElement(50);
        Dispatcher.UIThread.RunJobs();

        foreach (var offset in scrollOffsets)
        {
            scroller.Offset = new Vector(0, offset);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(listener.MeasureCount > 0);
        Assert.True(listener.ArrangeCount > 0);
        Assert.True(listener.GeneratePassCount > 0);
        Assert.True(listener.GenerateMeasuredCount > 0);
        Assert.True(listener.ElementRealized > 0);
        Assert.True(listener.ElementRecycled > 0);
        Assert.True(listener.MakeAnchor > 0);

        Assert.True(listener.MeasureActivities > 0);
        Assert.True(listener.ArrangeActivities > 0);
        Assert.True(listener.GenerateActivities > 0);

        _output.WriteLine(listener.FormatSummary());

        window.Close();
    }

    private static int ReadIntEnvironmentVariable(string name, int fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
    }

    private static double[] ReadOffsetsEnvironmentVariable(string name, double[] fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var parts = value.Split(',');
        var offsets = new double[parts.Length];
        var count = 0;
        foreach (var part in parts)
        {
            if (double.TryParse(part.Trim(), out var parsed))
            {
                offsets[count++] = parsed;
            }
        }

        if (count == 0)
        {
            return fallback;
        }

        if (count != offsets.Length)
        {
            Array.Resize(ref offsets, count);
        }

        return offsets;
    }

    private sealed class RepeaterDiagnosticsListener : IDisposable
    {
        private const string ActivitySourceName = "Avalonia.ItemsRepeater";
        private const string MeterName = "Avalonia.ItemsRepeater";
        private readonly ActivityListener _activityListener;
        private readonly MeterListener _meterListener;
        private readonly object _gate = new();

        public RepeaterDiagnosticsListener()
        {
            _activityListener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == ActivitySourceName,
                Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStopped = activity =>
                {
                    lock (_gate)
                    {
                        switch (activity.OperationName)
                        {
                            case "ItemsRepeater.Measure":
                                MeasureActivities++;
                                break;
                            case "ItemsRepeater.Arrange":
                                ArrangeActivities++;
                                break;
                            case "ItemsRepeater.Generate":
                                GenerateActivities++;
                                break;
                        }
                    }
                }
            };

            ActivitySource.AddActivityListener(_activityListener);

            _meterListener = new MeterListener();
            _meterListener.InstrumentPublished = static (instrument, listener) =>
            {
                if (instrument.Meter.Name == MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };

            _meterListener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
            {
                lock (_gate)
                {
                    switch (instrument.Name)
                    {
                        case "itemsrepeater.measure.count":
                            MeasureCount += measurement;
                            break;
                        case "itemsrepeater.arrange.count":
                            ArrangeCount += measurement;
                            break;
                        case "itemsrepeater.generate.pass":
                            GeneratePassCount += measurement;
                            break;
                        case "itemsrepeater.generate.measured.count":
                            GenerateMeasuredCount += measurement;
                            break;
                        case "itemsrepeater.element.realized":
                            ElementRealized += measurement;
                            break;
                        case "itemsrepeater.element.recycled":
                            ElementRecycled += measurement;
                            break;
                        case "itemsrepeater.anchor.make":
                            MakeAnchor += measurement;
                            break;
                    }
                }
            });

            _meterListener.SetMeasurementEventCallback<double>((instrument, measurement, _, _) =>
            {
                lock (_gate)
                {
                    switch (instrument.Name)
                    {
                        case "itemsrepeater.measure.duration.ms":
                            MeasureDurationTotalMs += measurement;
                            MeasureDurationSamples++;
                            MeasureDurationMaxMs = Math.Max(MeasureDurationMaxMs, measurement);
                            break;
                        case "itemsrepeater.arrange.duration.ms":
                            ArrangeDurationTotalMs += measurement;
                            ArrangeDurationSamples++;
                            ArrangeDurationMaxMs = Math.Max(ArrangeDurationMaxMs, measurement);
                            break;
                        case "itemsrepeater.generate.duration.ms":
                            GenerateDurationTotalMs += measurement;
                            GenerateDurationSamples++;
                            GenerateDurationMaxMs = Math.Max(GenerateDurationMaxMs, measurement);
                            break;
                    }
                }
            });
            _meterListener.Start();
        }

        public int MeasureActivities { get; private set; }
        public int ArrangeActivities { get; private set; }
        public int GenerateActivities { get; private set; }
        public long MeasureCount { get; private set; }
        public long ArrangeCount { get; private set; }
        public long GeneratePassCount { get; private set; }
        public long GenerateMeasuredCount { get; private set; }
        public long ElementRealized { get; private set; }
        public long ElementRecycled { get; private set; }
        public long MakeAnchor { get; private set; }
        public double MeasureDurationTotalMs { get; private set; }
        public double ArrangeDurationTotalMs { get; private set; }
        public double GenerateDurationTotalMs { get; private set; }
        public int MeasureDurationSamples { get; private set; }
        public int ArrangeDurationSamples { get; private set; }
        public int GenerateDurationSamples { get; private set; }
        public double MeasureDurationMaxMs { get; private set; }
        public double ArrangeDurationMaxMs { get; private set; }
        public double GenerateDurationMaxMs { get; private set; }

        public string FormatSummary()
        {
            return string.Join(
                Environment.NewLine,
                $"Measure: count={MeasureCount} avgMs={Average(MeasureDurationTotalMs, MeasureDurationSamples):F3} maxMs={MeasureDurationMaxMs:F3}",
                $"Arrange: count={ArrangeCount} avgMs={Average(ArrangeDurationTotalMs, ArrangeDurationSamples):F3} maxMs={ArrangeDurationMaxMs:F3}",
                $"Generate: passes={GeneratePassCount} measuredTotal={GenerateMeasuredCount} avgMs={Average(GenerateDurationTotalMs, GenerateDurationSamples):F3} maxMs={GenerateDurationMaxMs:F3}",
                $"Elements: realized={ElementRealized} recycled={ElementRecycled} makeAnchor={MakeAnchor}",
                $"Activities: measure={MeasureActivities} arrange={ArrangeActivities} generate={GenerateActivities}");
        }

        private static double Average(double total, int samples) => samples == 0 ? 0 : total / samples;

        public void Dispose()
        {
            _activityListener.Dispose();
            _meterListener.Dispose();
        }
    }

    private sealed class SizedItem
    {
        public SizedItem(double width, double height)
        {
            Width = width;
            Height = height;
        }

        public double Width { get; }
        public double Height { get; }
    }
}
