using Microsoft.UI.Xaml.Controls;

namespace ItemsRepeaterUnoSample;

public sealed partial class NestedItemsRepeaterPage : UserControl
{
    public NestedItemsRepeaterPage()
    {
        InitializeComponent();
        DataContext = new NestedItemsRepeaterPageViewModel();
    }
}
