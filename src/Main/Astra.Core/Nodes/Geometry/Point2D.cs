using System;

namespace Astra.Core.Nodes.Geometry
{
    /// <summary>
    /// 跨平台二维点结构，替代 System.Windows.Point
    /// </summary>
    public readonly struct Point2D : IEquatable<Point2D>
    {
        public double X { get; }
        public double Y { get; }

        public Point2D(double x, double y)
        {
            X = x;
            Y = y;
        }

        public static Point2D Zero => new Point2D(0, 0);

        public bool Equals(Point2D other)
        {
            return Math.Abs(X - other.X) < double.Epsilon && Math.Abs(Y - other.Y) < double.Epsilon;
        }

        public override bool Equals(object? obj)
        {
            return obj is Point2D other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        public static bool operator ==(Point2D left, Point2D right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Point2D left, Point2D right)
        {
            return !left.Equals(right);
        }

        public static Point2D operator +(Point2D left, Point2D right)
        {
            return new Point2D(left.X + right.X, left.Y + right.Y);
        }

        public static Point2D operator -(Point2D left, Point2D right)
        {
            return new Point2D(left.X - right.X, left.Y - right.Y);
        }

        public static Point2D operator *(Point2D point, double scalar)
        {
            return new Point2D(point.X * scalar, point.Y * scalar);
        }

        public double DistanceTo(Point2D other)
        {
            var dx = X - other.X;
            var dy = Y - other.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public double ManhattanDistanceTo(Point2D other)
        {
            return Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
        }

        public override string ToString()
        {
            return $"({X:F2}, {Y:F2})";
        }
    }
}
