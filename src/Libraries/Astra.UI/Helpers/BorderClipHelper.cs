using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Astra.UI.Helpers
{
    public static class BorderClipHelper
    {
        public static bool GetClipToBounds(DependencyObject obj)
        {
            return (bool)obj.GetValue(ClipToBoundsProperty);
        }

        public static void SetClipToBounds(DependencyObject obj, bool value)
        {
            obj.SetValue(ClipToBoundsProperty, value);
        }

        public static readonly DependencyProperty ClipToBoundsProperty =
            DependencyProperty.RegisterAttached("ClipToBounds", typeof(bool),
                typeof(BorderClipHelper),
                new PropertyMetadata(false, OnClipToBoundsChanged));

        private static void OnClipToBoundsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Border border)
            {
                if ((bool)e.NewValue)
                {
                    border.Loaded += Border_Loaded;
                    border.SizeChanged += Border_SizeChanged;
                }
                else
                {
                    border.Loaded -= Border_Loaded;
                    border.SizeChanged -= Border_SizeChanged;
                }
            }
        }

        private static void Border_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateClip((Border)sender);
        }

        private static void Border_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateClip((Border)sender);
        }

        private static void UpdateClip(Border border)
        {
            if (border.ActualWidth > 0 && border.ActualHeight > 0)
            {
                var radius = border.CornerRadius.TopLeft;
                border.Clip = new RectangleGeometry
                {
                    RadiusX = radius,
                    RadiusY = radius,
                    Rect = new Rect(0, 0, border.ActualWidth, border.ActualHeight)
                };
            }
        }
    }
}
