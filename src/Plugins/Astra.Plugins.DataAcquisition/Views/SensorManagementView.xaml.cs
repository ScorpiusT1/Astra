using System.Windows;
using System.Windows.Controls;

namespace Astra.Plugins.DataAcquisition.Views
{
    /// <summary>
    /// SensorManagementView.xaml 的交互逻辑
    /// </summary>
    public partial class SensorManagementView : UserControl
    {
        public SensorManagementView()
        {
            InitializeComponent();
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

