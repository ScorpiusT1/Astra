using Astra.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace Astra.Views
{
    /// <summary>
    /// 多类型配置新增选择弹窗。
    /// </summary>
    public partial class ConfigTypeSelectionDialog : Window
    {
        public static readonly DependencyProperty ItemsProperty =
            DependencyProperty.Register(
                nameof(Items),
                typeof(ObservableCollection<ConfigTypeSelectionItem>),
                typeof(ConfigTypeSelectionDialog),
                new PropertyMetadata(new ObservableCollection<ConfigTypeSelectionItem>()));

        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(
                nameof(SelectedItem),
                typeof(ConfigTypeSelectionItem),
                typeof(ConfigTypeSelectionDialog),
                new PropertyMetadata(null));

        public static readonly DependencyProperty PromptTextProperty =
            DependencyProperty.Register(
                nameof(PromptText),
                typeof(string),
                typeof(ConfigTypeSelectionDialog),
                new PropertyMetadata(string.Empty));

        public ObservableCollection<ConfigTypeSelectionItem> Items
        {
            get => (ObservableCollection<ConfigTypeSelectionItem>)GetValue(ItemsProperty);
            set => SetValue(ItemsProperty, value);
        }

        public ConfigTypeSelectionItem? SelectedItem
        {
            get => (ConfigTypeSelectionItem?)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public string PromptText
        {
            get => (string)GetValue(PromptTextProperty);
            set => SetValue(PromptTextProperty, value);
        }

        public ConfigTypeSelectionDialog(IEnumerable<ConfigTypeSelectionItem> items, string parentHeader)
        {
            InitializeComponent();
            Items = new ObservableCollection<ConfigTypeSelectionItem>(items ?? Enumerable.Empty<ConfigTypeSelectionItem>());
            SelectedItem = Items.FirstOrDefault();
            PromptText = $"请选择要新增到 \"{parentHeader}\" 的配置类型：";
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem == null)
            {
                return;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void RootBorder_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}
