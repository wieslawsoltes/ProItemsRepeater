using Avalonia.Controls;

namespace Avalonia.Controls.Samples;

public partial class RepeaterDataGridPage : UserControl
{
    private readonly RepeaterDataGridPageViewModel _viewModel;

    public RepeaterDataGridPage()
    {
        InitializeComponent();
        DataContext = _viewModel = new RepeaterDataGridPageViewModel();
        randomizeHeights.Click += OnRandomizeHeights;
    }

    private void OnRandomizeHeights(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewModel.RandomizeHeights();
        grid.InvalidateMeasure();
    }
}
