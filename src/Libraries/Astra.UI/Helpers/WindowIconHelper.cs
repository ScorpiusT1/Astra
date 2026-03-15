using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Astra.UI.Helpers
{
    /// <summary>
    /// 将资源中的 Visual（如 AppLogoGeometry Canvas）渲染为 ImageSource 并设置为 Window.Icon。
    /// Window.Icon 仅接受 ImageSource，而 AppLogoGeometry 为 Canvas，故需在 Loaded 时渲染。
    /// </summary>
    public static class WindowIconHelper
    {
        public static readonly DependencyProperty IconGeometryKeyProperty =
            DependencyProperty.RegisterAttached(
                "IconGeometryKey",
                typeof(string),
                typeof(WindowIconHelper),
                new PropertyMetadata(null, OnIconGeometryKeyChanged));

        public static string GetIconGeometryKey(DependencyObject obj) => (string)obj.GetValue(IconGeometryKeyProperty);
        public static void SetIconGeometryKey(DependencyObject obj, string value) => obj.SetValue(IconGeometryKeyProperty, value);

        private static void OnIconGeometryKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Window window || string.IsNullOrEmpty((string)e.NewValue))
                return;

            void SetIconFromResource()
            {
                var key = (string)e.NewValue;
                var resource = window.TryFindResource(key) ?? Application.Current?.TryFindResource(key);
                if (resource is Visual visual)
                {
                    var source = RenderVisualToImageSource(visual, 48);
                    if (source != null)
                        window.Icon = source;
                }
            }

            if (window.IsLoaded)
                SetIconFromResource();
            else
                window.Loaded += (_, __) => SetIconFromResource();
        }

        /// <summary>
        /// 将 Visual（如 Canvas）渲染为指定尺寸的 ImageSource，用于 Window.Icon。
        /// </summary>
        private static ImageSource RenderVisualToImageSource(Visual visual, double size)
        {
            try
            {
                var brush = new VisualBrush(visual) { Stretch = Stretch.Uniform };
                var drawingVisual = new DrawingVisual();
                using (var dc = drawingVisual.RenderOpen())
                    dc.DrawRectangle(brush, null, new Rect(0, 0, size, size));

                var bmp = new RenderTargetBitmap((int)size, (int)size, 96, 96, PixelFormats.Pbgra32);
                bmp.Render(drawingVisual);
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }
    }
}
