using Astra.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Astra.Views
{
    /// <summary>
    /// UserMenuControl.xaml 的交互逻辑
    /// </summary>
    public partial class UserMenuControl : UserControl
    {
        public UserMenuControl()
        {
            InitializeComponent();
        }

        private void MainBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 动态更新裁剪区域，确保圆角完美裁剪
            var border = sender as Border;
            if (border != null)
            {
                border.Clip = new RectangleGeometry
                {
                    RadiusX = 12,
                    RadiusY = 12,
                    Rect = new Rect(0, 0, border.ActualWidth, border.ActualHeight)
                };
            }
        }
    }
}

