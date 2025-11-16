using Astra.Core.Nodes.Geometry;
using System.Windows;
using System.Windows.Media;
using HorizontalAlignment = Astra.Core.Nodes.Geometry.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;



namespace FlowNodeEditor.Wpf.Adapters
{
    /// <summary>
    /// WPF 类型与 Core 类型之间的转换适配器
    /// </summary>
    public static class WpfTypeMapper
    {
        // Point2D 转换
        public static Point2D ToPoint2D(this Point point)
        {
            return new Point2D(point.X, point.Y);
        }

        public static Point ToWpfPoint(this Point2D point)
        {
            return new Point(point.X, point.Y);
        }

        // Rect2D 转换
        public static Rect2D ToRect2D(this Rect rect)
        {
            return new Rect2D(rect.X, rect.Y, rect.Width, rect.Height);
        }

        public static Rect ToWpfRect(this Rect2D rect)
        {
            return new Rect(rect.X, rect.Y, rect.Width, rect.Height);
        }

        // Size2D 转换
        public static Size2D ToSize2D(this Size size)
        {
            return new Size2D(size.Width, size.Height);
        }

        public static Size ToWpfSize(this Size2D size)
        {
            return new Size(size.Width, size.Height);
        }

        // ColorRgba 转换
        public static ColorRgba ToColorRgba(this Color color)
        {
            return new ColorRgba(color.R, color.G, color.B, color.A);
        }

        public static Color ToWpfColor(this ColorRgba color)
        {
            return Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        public static SolidColorBrush ToWpfBrush(this ColorRgba color)
        {
            return new SolidColorBrush(color.ToWpfColor());
        }

        // Thickness 转换
        public static Astra.Core.Nodes.Geometry.Thickness ToCoreThickness(this System.Windows.Thickness thickness)
        {
            return new Astra.Core.Nodes.Geometry.Thickness(thickness.Left, thickness.Top, thickness.Right, thickness.Bottom);
        }

        public static System.Windows.Thickness ToWpfThickness(this Astra.Core.Nodes.Geometry.Thickness thickness)
        {
            return new System.Windows.Thickness(thickness.Left, thickness.Top, thickness.Right, thickness.Bottom);
        }

        // HorizontalAlignment 转换
        public static HorizontalAlignment ToCoreHorizontalAlignment(this System.Windows.HorizontalAlignment alignment)
        {
            return alignment switch
            {
                System.Windows.HorizontalAlignment.Left => HorizontalAlignment.Left,
                System.Windows.HorizontalAlignment.Center => HorizontalAlignment.Center,
                System.Windows.HorizontalAlignment.Right => HorizontalAlignment.Right,
                System.Windows.HorizontalAlignment.Stretch => HorizontalAlignment.Center,
                _ => HorizontalAlignment.Center
            };
        }

        public static System.Windows.HorizontalAlignment ToWpfHorizontalAlignment(this HorizontalAlignment alignment)
        {
            return alignment switch
            {
                Astra.Core.Nodes.Geometry.HorizontalAlignment.Left => System.Windows.HorizontalAlignment.Left,
                HorizontalAlignment.Center => System.Windows.HorizontalAlignment.Center,
                HorizontalAlignment.Right => System.Windows.HorizontalAlignment.Right,
                _ => System.Windows.HorizontalAlignment.Center
            };
        }

        // VerticalAlignment 转换
        public static VerticalAlignment ToCoreVerticalAlignment(this System.Windows.VerticalAlignment alignment)
        {
            return alignment switch
            {
                System.Windows.VerticalAlignment.Top => VerticalAlignment.Top,
                System.Windows.VerticalAlignment.Center => VerticalAlignment.Center,
                System.Windows.VerticalAlignment.Bottom => VerticalAlignment.Bottom,
                System.Windows.VerticalAlignment.Stretch => VerticalAlignment.Center,
                _ => VerticalAlignment.Center
            };
        }

        public static System.Windows.VerticalAlignment ToWpfVerticalAlignment(this VerticalAlignment alignment)
        {
            return alignment switch
            {
                VerticalAlignment.Top => System.Windows.VerticalAlignment.Top,
                VerticalAlignment.Center => System.Windows.VerticalAlignment.Center,
                VerticalAlignment.Bottom => System.Windows.VerticalAlignment.Bottom,
                _ => System.Windows.VerticalAlignment.Center
            };
        }

        // PortDirection 转换
        public static PortDirection ToCorePortDirection(this System.Windows.Controls.Dock dock)
        {
            return dock switch
            {
                System.Windows.Controls.Dock.Left => PortDirection.Left,
                System.Windows.Controls.Dock.Right => PortDirection.Right,
                System.Windows.Controls.Dock.Top => PortDirection.Top,
                System.Windows.Controls.Dock.Bottom => PortDirection.Bottom,
                _ => PortDirection.Right
            };
        }

        public static System.Windows.Controls.Dock ToWpfDock(this PortDirection direction)
        {
            return direction switch
            {
                PortDirection.Left => System.Windows.Controls.Dock.Left,
                PortDirection.Right => System.Windows.Controls.Dock.Right,
                PortDirection.Top => System.Windows.Controls.Dock.Top,
                PortDirection.Bottom => System.Windows.Controls.Dock.Bottom,
                _ => System.Windows.Controls.Dock.Right
            };
        }

        // Vector 转换
        public static Point2D ToPoint2D(this Vector vector)
        {
            return new Point2D(vector.X, vector.Y);
        }

        public static Vector ToWpfVector(this Point2D point)
        {
            return new Vector(point.X, point.Y);
        }
    }
}
