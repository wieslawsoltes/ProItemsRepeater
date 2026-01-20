using Avalonia.Controls;

namespace Avalonia.Controls.Samples;

public partial class NestedItemsRepeaterPage : UserControl
{
    public NestedItemsRepeaterPage()
    {
        InitializeComponent();
        DataContext = new NestedItemsRepeaterPageViewModel();
    }
}
