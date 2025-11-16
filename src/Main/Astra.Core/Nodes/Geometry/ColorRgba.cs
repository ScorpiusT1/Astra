using System;

namespace Astra.Core.Nodes.Geometry
{
    /// <summary>
    /// 跨平台RGBA颜色结构，替代 System.Windows.Media.Color
    /// </summary>
    public readonly struct ColorRgba : IEquatable<ColorRgba>
    {
        public byte R { get; }
        public byte G { get; }
        public byte B { get; }
        public byte A { get; }

        public ColorRgba(byte r, byte g, byte b, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public static ColorRgba Transparent => new ColorRgba(0, 0, 0, 0);
        public static ColorRgba Black => new ColorRgba(0, 0, 0, 255);
        public static ColorRgba White => new ColorRgba(255, 255, 255, 255);
        public static ColorRgba Red => new ColorRgba(255, 0, 0, 255);
        public static ColorRgba Green => new ColorRgba(0, 255, 0, 255);
        public static ColorRgba Blue => new ColorRgba(0, 0, 255, 255);

        public bool IsTransparent => A == 0;

        public ColorRgba WithAlpha(byte alpha)
        {
            return new ColorRgba(R, G, B, alpha);
        }

        public ColorRgba WithOpacity(double opacity)
        {
            var alpha = (byte)(255 * Math.Clamp(opacity, 0, 1));
            return WithAlpha(alpha);
        }

        public static ColorRgba FromHex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Transparent;
            
            hex = hex.TrimStart('#');
            if (hex.Length == 3)
            {
                // RGB format
                var r = Convert.ToByte(hex.Substring(0, 1) + hex.Substring(0, 1), 16);
                var g = Convert.ToByte(hex.Substring(1, 1) + hex.Substring(1, 1), 16);
                var b = Convert.ToByte(hex.Substring(2, 1) + hex.Substring(2, 1), 16);
                return new ColorRgba(r, g, b);
            }
            else if (hex.Length == 6)
            {
                // RRGGBB format
                var r = Convert.ToByte(hex.Substring(0, 2), 16);
                var g = Convert.ToByte(hex.Substring(2, 2), 16);
                var b = Convert.ToByte(hex.Substring(4, 2), 16);
                return new ColorRgba(r, g, b);
            }
            else if (hex.Length == 8)
            {
                // AARRGGBB format
                var a = Convert.ToByte(hex.Substring(0, 2), 16);
                var r = Convert.ToByte(hex.Substring(2, 2), 16);
                var g = Convert.ToByte(hex.Substring(4, 2), 16);
                var b = Convert.ToByte(hex.Substring(6, 2), 16);
                return new ColorRgba(r, g, b, a);
            }
            
            return Transparent;
        }

        public string ToHex()
        {
            return $"#{R:X2}{G:X2}{B:X2}{(A == 255 ? string.Empty : A.ToString("X2"))}";
        }

        public bool Equals(ColorRgba other)
        {
            return R == other.R && G == other.G && B == other.B && A == other.A;
        }

        public override bool Equals(object? obj)
        {
            return obj is ColorRgba other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(R, G, B, A);
        }

        public static bool operator ==(ColorRgba left, ColorRgba right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ColorRgba left, ColorRgba right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"RGBA({R}, {G}, {B}, {A})";
        }
    }
}
