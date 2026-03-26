namespace Avalonia
{
    internal static class LayoutableExtensions
    {
        public static void Measure(this Layoutable element, Size availableSize)
        {
            element.Measure(availableSize.ToNative());
        }

        public static void Arrange(this Layoutable element, Rect finalRect)
        {
            element.Arrange(finalRect.ToNative());
        }
    }
}
