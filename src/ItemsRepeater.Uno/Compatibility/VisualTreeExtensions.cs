using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Avalonia.VisualTree
{
    internal static class VisualTreeExtensions
    {
        public static DependencyObject? GetVisualParent(this DependencyObject element)
        {
            return VisualTreeHelper.GetParent(element);
        }

        public static bool IsAttachedToVisualTree(this UIElement element)
        {
            return element.XamlRoot is not null;
        }
    }
}
