using System.Collections.ObjectModel;
using System.Collections;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls.Selection;
using Avalonia.Data;
using Xunit;

namespace ItemsRepeater.Uno.UnitTests;

public class CompatibilityTests
{
    private sealed class TestBinding : BindingBase
    {
        public object? Value { get; set; }

        protected override object? EvaluateCore(object? dataContext) => Value ?? dataContext;
    }

    [Theory]
    [InlineData("Auto", 1d, Avalonia.GridUnitType.Auto)]
    [InlineData("*", 1d, Avalonia.GridUnitType.Star)]
    [InlineData("2*", 2d, Avalonia.GridUnitType.Star)]
    [InlineData("90", 90d, Avalonia.GridUnitType.Pixel)]
    public void GridLengthTypeConverter_Parses_Common_Forms(string text, double expectedValue, Avalonia.GridUnitType expectedUnitType)
    {
        var converter = new GridLengthTypeConverter();

        var result = Assert.IsType<GridLength>(converter.ConvertFrom(text));

        Assert.Equal(expectedValue, result.Value);
        Assert.Equal(expectedUnitType, result.GridUnitType);
    }

    [Fact]
    public void SelectionModel_Tracks_Source_Index_Changes_By_Selected_Item()
    {
        var source = new ObservableCollection<string> { "alpha", "beta", "gamma" };
        var model = new SelectionModel<string> { Source = source };
        model.Select(1);

        source.Insert(0, "zero");

        Assert.Equal(2, model.SelectedIndex);
        Assert.Equal("beta", model.SelectedItem);
        Assert.Equal(new[] { 2 }, model.SelectedIndexes);
    }

    [Fact]
    public void SelectionModel_Raises_PropertyChanged_And_SelectionChanged()
    {
        var source = new[] { "alpha", "beta", "gamma" };
        var model = new SelectionModel<string> { Source = source, SingleSelect = false };
        var changedProperties = new List<string>();
        var selectionChanged = 0;

        model.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is not null)
                changedProperties.Add(args.PropertyName);
        };
        model.SelectionChanged += (_, _) => ++selectionChanged;

        using (model.BatchUpdate())
        {
            model.Select(0);
            model.Select(2);
        }

        Assert.Equal(1, selectionChanged);
        Assert.Contains(nameof(ISelectionModel.SelectedIndexes), changedProperties);
        Assert.Contains(nameof(ISelectionModel.Count), changedProperties);
        Assert.Equal(new[] { 0, 2 }, model.SelectedIndexes);
    }

    [Fact]
    public void BindingBase_Can_Be_Extended_Out_Of_Assembly()
    {
        var binding = new TestBinding { Value = 42 };

        Assert.IsAssignableFrom<BindingBase>(binding);
    }

    [Fact]
    public void RepeaterSelectionModel_Raises_PropertyChanged_When_SelectedItems_List_Is_Replaced()
    {
        var model = new RepeaterSelectionModel();
        var changedProperties = new List<string>();
        model.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is not null)
                changedProperties.Add(args.PropertyName);
        };

        model.WritableSelectedItems = new ArrayList { "alpha" };

        Assert.Contains(nameof(RepeaterSelectionModel.WritableSelectedItems), changedProperties);
    }

    [Fact]
    public void RepeaterSelectionModel_Rejects_Fixed_Size_SelectedItems_List()
    {
        var model = new RepeaterSelectionModel();

        Assert.Throws<NotSupportedException>(() => model.WritableSelectedItems = ArrayList.FixedSize(new ArrayList { "alpha" }));
    }

    [Fact]
    public void RepeaterSelectionModel_Removes_Missing_SelectedItems_When_Source_Is_Applied()
    {
        var model = new RepeaterSelectionModel();
        var selectedItems = new ArrayList { "alpha", "missing" };
        model.WritableSelectedItems = selectedItems;
        model.Source = new[] { "alpha", "beta" };

        Assert.Equal(new[] { "alpha" }, selectedItems.Cast<string>());
        Assert.Equal(0, model.SelectedIndex);
        Assert.Equal("alpha", model.SelectedItem);
    }
}
