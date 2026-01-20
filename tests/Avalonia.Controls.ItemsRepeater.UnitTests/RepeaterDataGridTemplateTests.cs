using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls.DataGrid;
using Avalonia.Controls.Samples;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.UnitTests;

public class RepeaterDataGridTemplateTests
{
    [AvaloniaFact]
    public void RepeaterDataGrid_Renders_Header_And_Rows()
    {
        var grid = new RepeaterDataGrid
        {
            RowHeightBindingPath = nameof(RepeaterDataGridItem.Height)
        };

        grid.Columns.Add(new RepeaterDataGridColumn
        {
            Header = "#",
            Width = GridLength.Auto,
            MinWidth = 70,
            BindingPath = nameof(RepeaterDataGridItem.Id)
        });

        grid.Columns.Add(new RepeaterDataGridColumn
        {
            Header = "Name",
            Width = new GridLength(1, GridUnitType.Star),
            BindingPath = nameof(RepeaterDataGridItem.Name)
        });

        var items = new ObservableCollection<RepeaterDataGridItem>
        {
            new()
            {
                Id = 1,
                Name = "Item 1",
                Category = "Hardware",
                Price = "1.00",
                Stock = 5,
                Height = 28
            }
        };

        grid.ItemsSource = items;

        if (Application.Current is { } app)
        {
            app.Styles.Add(new StyleInclude(new Uri("avares://Avalonia.Controls.ItemsRepeater/"))
            {
                Source = new Uri("avares://Avalonia.Controls.ItemsRepeater/DataGrid/RepeaterDataGrid.axaml")
            });
        }

        var window = new Window
        {
            Width = 800,
            Height = 600,
            Content = grid
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var headerRepeater = grid.GetVisualDescendants()
            .OfType<ItemsRepeater>()
            .FirstOrDefault(control => control.Name == "PART_HeaderRepeater");

        var rowsRepeater = grid.GetVisualDescendants()
            .OfType<SelectingItemsRepeater>()
            .FirstOrDefault(control => control.Name == "PART_RowsRepeater");

        Assert.NotNull(headerRepeater);
        Assert.NotNull(rowsRepeater);

        Assert.Equal(grid.Columns.Count, headerRepeater!.ItemsSourceView?.Count ?? 0);
        Assert.Equal(items.Count, rowsRepeater!.ItemsSourceView?.Count ?? 0);

        Assert.NotEmpty(headerRepeater.Children);
        Assert.NotEmpty(rowsRepeater.Children);
        Assert.True(grid.Bounds.Width > 0);
        Assert.True(headerRepeater.Bounds.Width > 0);
        Assert.True(rowsRepeater.Bounds.Height > 0);

        window.Close();
    }

    [AvaloniaFact]
    public void RepeaterDataGridPage_Renders_Header_And_Rows_From_Xaml()
    {
        if (Application.Current is { } app)
        {
            app.Styles.Add(new StyleInclude(new Uri("avares://Avalonia.Controls.ItemsRepeater/"))
            {
                Source = new Uri("avares://Avalonia.Controls.ItemsRepeater/DataGrid/RepeaterDataGrid.axaml")
            });
        }

        var page = new RepeaterDataGridPage();
        var grid = page.FindControl<RepeaterDataGrid>("grid");

        Assert.NotNull(grid);

        var window = new Window
        {
            Width = 800,
            Height = 600,
            Content = page
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.NotEmpty(grid!.Columns);
        Assert.NotNull(grid.ItemsSource);

        var headerRepeater = grid.GetVisualDescendants()
            .OfType<ItemsRepeater>()
            .FirstOrDefault(control => control.Name == "PART_HeaderRepeater");

        var rowsRepeater = grid.GetVisualDescendants()
            .OfType<SelectingItemsRepeater>()
            .FirstOrDefault(control => control.Name == "PART_RowsRepeater");

        Assert.NotNull(headerRepeater);
        Assert.NotNull(rowsRepeater);

        Assert.NotEmpty(headerRepeater!.Children);
        Assert.NotEmpty(rowsRepeater!.Children);

        window.Close();
    }
}
