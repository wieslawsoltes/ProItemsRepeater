using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Avalonia.Controls.DataGrid;

internal sealed class RepeaterDataGridHeaderCell : Border
{
    private static readonly SolidColorBrush GridBorderBrush = new(Color.FromArgb(0xFF, 0xD9, 0xD9, 0xD9));
    private static readonly SolidColorBrush HeaderBackgroundBrush = new(Color.FromArgb(0xFF, 0xF4, 0xF4, 0xF4));

    private readonly ContentControl _content;
    private readonly TextBlock _text;

    public RepeaterDataGridHeaderCell()
    {
        BorderBrush = GridBorderBrush;
        BorderThickness = new Thickness(1, 1, 1, 1);
        Padding = new Thickness(8, 6, 8, 6);
        Background = HeaderBackgroundBrush;
        HorizontalAlignment = HorizontalAlignment.Left;

        var grid = new Grid();
        _content = new ContentControl
        {
            VerticalAlignment = VerticalAlignment.Center,
        };
        _text = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
        };

        grid.Children.Add(_content);
        grid.Children.Add(_text);
        Child = grid;
    }

    public void Bind(RepeaterDataGridColumn column)
    {
        Width = column.ActualWidth;

        if (column.HeaderTemplate is not null)
        {
            _content.Content = column.Header;
            _content.ContentTemplate = column.HeaderTemplate;
            _content.Visibility = Visibility.Visible;
            _text.Visibility = Visibility.Collapsed;
        }
        else
        {
            _content.Content = null;
            _content.ContentTemplate = null;
            _content.Visibility = Visibility.Collapsed;
            _text.Text = column.Header?.ToString() ?? string.Empty;
            _text.Visibility = Visibility.Visible;
        }
    }
}
