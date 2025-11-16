using System;

namespace Astra.Core.Nodes.Geometry
{
    /// <summary>
    /// 跨平台二维矩形结构，替代 System.Windows.Rect
    /// </summary>
    public readonly struct Rect2D : IEquatable<Rect2D>
    {
        public double X { get; }
        public double Y { get; }
        public double Width { get; }
        public double Height { get; }

        public Rect2D(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public Rect2D(Point2D location, Size2D size)
        {
            X = location.X;
            Y = location.Y;
            Width = size.Width;
            Height = size.Height;
        }

        public double Left => X;
        public double Top => Y;
        public double Right => X + Width;
        public double Bottom => Y + Height;

        public Point2D TopLeft => new Point2D(Left, Top);
        public Point2D TopRight => new Point2D(Right, Top);
        public Point2D BottomLeft => new Point2D(Left, Bottom);
        public Point2D BottomRight => new Point2D(Right, Bottom);
        public Point2D Center => new Point2D(X + Width / 2, Y + Height / 2);

        public Size2D Size => new Size2D(Width, Height);

        public bool IsEmpty => Width <= 0 || Height <= 0;

        public static Rect2D Empty => new Rect2D(0, 0, 0, 0);

        public bool Contains(Point2D point)
        {
            return point.X >= Left && point.X <= Right && 
                   point.Y >= Top && point.Y <= Bottom;
        }

        public bool Contains(Rect2D rect)
        {
            return Left <= rect.Left && Right >= rect.Right &&
                   Top <= rect.Top && Bottom >= rect.Bottom;
        }

        public bool IntersectsWith(Rect2D rect)
        {
            return !(rect.Left >= Right || rect.Right <= Left ||
                     rect.Top >= Bottom || rect.Bottom <= Top);
        }

        public Rect2D Inflate(double width, double height)
        {
            return new Rect2D(X - width, Y - height, Width + 2 * width, Height + 2 * height);
        }

        public Rect2D Union(Rect2D rect)
        {
            if (IsEmpty) return rect;
            if (rect.IsEmpty) return this;

            var left = Math.Min(Left, rect.Left);
            var top = Math.Min(Top, rect.Top);
            var right = Math.Max(Right, rect.Right);
            var bottom = Math.Max(Bottom, rect.Bottom);

            return new Rect2D(left, top, right - left, bottom - top);
        }

        public bool Equals(Rect2D other)
        {
            return Math.Abs(X - other.X) < double.Epsilon &&
                   Math.Abs(Y - other.Y) < double.Epsilon &&
                   Math.Abs(Width - other.Width) < double.Epsilon &&
                   Math.Abs(Height - other.Height) < double.Epsilon;
        }

        public override bool Equals(object? obj)
        {
            return obj is Rect2D other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Width, Height);
        }

        public static bool operator ==(Rect2D left, Rect2D right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Rect2D left, Rect2D right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"({X:F2}, {Y:F2}, {Width:F2}, {Height:F2})";
        }
    }
}
