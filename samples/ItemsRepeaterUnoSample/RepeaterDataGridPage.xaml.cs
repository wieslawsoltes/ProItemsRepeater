using Avalonia;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ItemsRepeaterUnoSample;

public sealed partial class RepeaterDataGridPage : UserControl
{
    private readonly RepeaterDataGridPageViewModel _viewModel = new();

    public RepeaterDataGridPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        ConfigureColumns();
    }

    private void RandomizeHeightsClick(object sender, RoutedEventArgs e)
    {
        _viewModel.RandomizeHeights();
        grid.InvalidateMeasure();
    }

    private void ConfigureColumns()
    {
        if (grid.Columns.Count < 5)
            return;

        grid.Columns[0].Width = Avalonia.GridLength.Auto;
        grid.Columns[1].Width = new Avalonia.GridLength(2, Avalonia.GridUnitType.Star);
        grid.Columns[2].Width = new Avalonia.GridLength(1, Avalonia.GridUnitType.Star);
        grid.Columns[3].Width = new Avalonia.GridLength(90);
        grid.Columns[4].Width = Avalonia.GridLength.Auto;
    }
}
