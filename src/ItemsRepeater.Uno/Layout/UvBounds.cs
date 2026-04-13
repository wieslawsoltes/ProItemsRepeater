namespace Avalonia.Layout
{
    internal struct UvBounds
    {
        public UvBounds(Orientation orientation, Rect rect)
        {
            if (orientation == Orientation.Horizontal)
            {
                UMin = rect.Left;
                UMax = rect.Right;
                VMin = rect.Top;
                VMax = rect.Bottom;
            }
            else
            {
                UMin = rect.Top;
                UMax = rect.Bottom;
                VMin = rect.Left;
                VMax = rect.Right;
            }
        }

        public double UMin { get; }

        public double UMax { get; }

        public double VMin { get; }

        public double VMax { get; }
    }
}
