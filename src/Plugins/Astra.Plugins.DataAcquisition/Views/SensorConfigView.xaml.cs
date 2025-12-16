using System.Windows;
using System.Windows.Controls;
using Astra.Plugins.DataAcquisition.Configs;
using Astra.Plugins.DataAcquisition.ViewModels;

namespace Astra.Plugins.DataAcquisition.Views
{
    /// <summary>
    /// SensorConfigView.xaml 的交互逻辑
    /// </summary>
    public partial class SensorConfigView : UserControl
    {
        public SensorConfigView()
        {
            InitializeComponent();
        }

        private void SingleAxisRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (DataContext is SensorConfigViewModel viewModel && viewModel.SelectedSensor != null)
            {
                viewModel.SelectedSensor.IsThreeAxis = false;
            }
        }

        private void SingleAxisRadioButton_Unchecked(object sender, RoutedEventArgs e)
        {
            // 当单轴取消选中时，不需要处理（三轴会自动选中）
        }

        private void ThreeAxisRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (DataContext is SensorConfigViewModel viewModel && viewModel.SelectedSensor != null)
            {
                viewModel.SelectedSensor.IsThreeAxis = true;
            }
        }

        private void PhysicalUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem != null)
            {
                // 当从下拉列表选择时，同步 Text 属性以显示选中的值
                comboBox.Text = comboBox.SelectedItem.ToString();
            }
        }

        private void PhysicalUnitComboBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                // 当用户直接输入文本时，尝试在 ItemsSource 中查找匹配项
                if (!string.IsNullOrEmpty(comboBox.Text) && comboBox.ItemsSource != null)
                {
                    foreach (var item in comboBox.ItemsSource)
                    {
                        if (item?.ToString() == comboBox.Text)
                        {
                            comboBox.SelectedItem = item;
                            break;
                        }
                    }
                }
            }
        }

        private void SensitivityUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem != null)
            {
                // 当从下拉列表选择时，同步 Text 属性以显示选中的值
                comboBox.Text = comboBox.SelectedItem.ToString();
            }
        }

        private void PhysicalUnitComboBox_TextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                // 当用户直接输入文本时，尝试在 ItemsSource 中查找匹配项
                if (!string.IsNullOrEmpty(comboBox.Text) && comboBox.ItemsSource != null)
                {
                    foreach (var item in comboBox.ItemsSource)
                    {
                        if (item?.ToString() == comboBox.Text)
                        {
                            comboBox.SelectedItem = item;
                            break;
                        }
                    }
                }
            }
        }
    }
}

