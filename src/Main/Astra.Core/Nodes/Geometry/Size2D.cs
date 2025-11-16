using System;

namespace Astra.Core.Nodes.Geometry
{
    /// <summary>
    /// 跨平台二维尺寸结构，替代 System.Windows.Size
    /// </summary>
    public readonly struct Size2D : IEquatable<Size2D>
    {
        public double Width { get; }
        public double Height { get; }

        public Size2D(double width, double height)
        {
            Width = width;
            Height = height;
        }

        public static Size2D Empty => new Size2D(0, 0);

        public bool IsEmpty => Width <= 0 || Height <= 0;

        public double Area => Width * Height;

        public static Size2D operator +(Size2D left, Size2D right)
        {
            return new Size2D(left.Width + right.Width, left.Height + right.Height);
        }

        public static Size2D operator -(Size2D left, Size2D right)
        {
            return new Size2D(left.Width - right.Width, left.Height - right.Height);
        }

        public static Size2D operator *(Size2D size, double scalar)
        {
            return new Size2D(size.Width * scalar, size.Height * scalar);
        }

        public bool Equals(Size2D other)
        {
            return Math.Abs(Width - other.Width) < double.Epsilon &&
                   Math.Abs(Height - other.Height) < double.Epsilon;
        }

        public override bool Equals(object? obj)
        {
            return obj is Size2D other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Width, Height);
        }

        public static bool operator ==(Size2D left, Size2D right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Size2D left, Size2D right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"{Width:F2} x {Height:F2}";
        }
    }
}
