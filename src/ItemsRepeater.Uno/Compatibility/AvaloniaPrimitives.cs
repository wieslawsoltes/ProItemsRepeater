using System;
using System.ComponentModel;
using System.Globalization;
using Windows.Foundation;

namespace Avalonia
{
    public enum GridUnitType
    {
        Auto,
        Pixel,
        Star,
    }

    [TypeConverter(typeof(GridLengthTypeConverter))]
    public readonly struct GridLength : IEquatable<GridLength>
    {
        public GridLength(double value, GridUnitType gridUnitType = GridUnitType.Pixel)
        {
            Value = value;
            GridUnitType = gridUnitType;
        }

        public double Value { get; }
        public GridUnitType GridUnitType { get; }
        public bool IsAbsolute => GridUnitType == GridUnitType.Pixel;
        public bool IsAuto => GridUnitType == GridUnitType.Auto;
        public bool IsStar => GridUnitType == GridUnitType.Star;

        public static GridLength Auto { get; } = new(1d, GridUnitType.Auto);

        public bool Equals(GridLength other) => Value.Equals(other.Value) && GridUnitType == other.GridUnitType;

        public override bool Equals(object? obj) => obj is GridLength other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Value, (int)GridUnitType);

        public override string ToString()
        {
            return GridUnitType switch
            {
                GridUnitType.Auto => "Auto",
                GridUnitType.Star => Value.Equals(1d) ? "*" : $"{Value.ToString(CultureInfo.InvariantCulture)}*",
                _ => Value.ToString(CultureInfo.InvariantCulture),
            };
        }

        public static bool operator ==(GridLength left, GridLength right) => left.Equals(right);
        public static bool operator !=(GridLength left, GridLength right) => !left.Equals(right);
    }

    public readonly struct Size : IEquatable<Size>
    {
        public Size(double width, double height)
        {
            Width = width;
            Height = height;
        }

        public double Width { get; }
        public double Height { get; }

        public bool Equals(Size other) => Width.Equals(other.Width) && Height.Equals(other.Height);
        public override bool Equals(object? obj) => obj is Size other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Width, Height);
        public static bool operator ==(Size left, Size right) => left.Equals(right);
        public static bool operator !=(Size left, Size right) => !left.Equals(right);
    }

    public readonly struct Point : IEquatable<Point>
    {
        public Point(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }

        public bool Equals(Point other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object? obj) => obj is Point other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public static bool operator ==(Point left, Point right) => left.Equals(right);
        public static bool operator !=(Point left, Point right) => !left.Equals(right);
    }

    public readonly struct Vector : IEquatable<Vector>
    {
        public Vector(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }

        public bool Equals(Vector other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object? obj) => obj is Vector other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public static bool operator ==(Vector left, Vector right) => left.Equals(right);
        public static bool operator !=(Vector left, Vector right) => !left.Equals(right);
    }

    public readonly struct Rect : IEquatable<Rect>
    {
        public Rect(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public Rect(Point position, Size size)
            : this(position.X, position.Y, size.Width, size.Height)
        {
        }

        public Rect(Size size)
            : this(0d, 0d, size.Width, size.Height)
        {
        }

        public double X { get; }
        public double Y { get; }
        public double Width { get; }
        public double Height { get; }
        public double Right => X + Width;
        public double Bottom => Y + Height;
        public Size Size => new(Width, Height);
        public Point TopLeft => new(X, Y);

        public bool Equals(Rect other) =>
            X.Equals(other.X) &&
            Y.Equals(other.Y) &&
            Width.Equals(other.Width) &&
            Height.Equals(other.Height);

        public override bool Equals(object? obj) => obj is Rect other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
        public static bool operator ==(Rect left, Rect right) => left.Equals(right);
        public static bool operator !=(Rect left, Rect right) => !left.Equals(right);
    }

    internal static class PrimitiveConversionExtensions
    {
        public static Windows.Foundation.Size ToNative(this Size size) => new(size.Width, size.Height);

        public static Windows.Foundation.Point ToNative(this Point point) => new(point.X, point.Y);

        public static Windows.Foundation.Rect ToNative(this Rect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

        public static Point ToAvalonia(this Windows.Foundation.Point point) => new(point.X, point.Y);

        public static Size ToAvalonia(this Windows.Foundation.Size size) => new(size.Width, size.Height);

        public static Rect ToAvalonia(this Windows.Foundation.Rect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
    }

    public sealed class GridLengthTypeConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
            sourceType == typeof(string) || sourceType == typeof(double) || base.CanConvertFrom(context, sourceType);

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            culture ??= CultureInfo.InvariantCulture;

            if (value is double number)
                return new GridLength(number);

            if (value is string text)
            {
                var trimmed = text.Trim();
                if (string.Equals(trimmed, "Auto", StringComparison.OrdinalIgnoreCase))
                    return GridLength.Auto;

                if (trimmed.EndsWith("*", StringComparison.Ordinal))
                {
                    var magnitude = trimmed.Length == 1 ? 1d : double.Parse(trimmed[..^1], culture);
                    return new GridLength(magnitude, GridUnitType.Star);
                }

                return new GridLength(double.Parse(trimmed, culture), GridUnitType.Pixel);
            }

            return base.ConvertFrom(context, culture, value);
        }
    }
}
