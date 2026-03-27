using Astra.UI.Abstractions.Attributes;
using Astra.UI.Abstractions.Interfaces;
using Astra.UI.Abstractions.Models;
using Astra.UI.Services;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Astra.UI.PropertyEditors
{
    /// <summary>
    /// ComboBox 属性编辑器（单选）
    /// </summary>
    public class ComboBoxPropertyEditor : PropertyEditorBase
    {
        private static readonly IItemsSourceResolver ItemsSourceResolver = DefaultItemsSourceResolver.Instance;

        public override FrameworkElement CreateElement(PropertyDescriptor propertyDescriptor)
        {
            var comboBox = new ComboBox
            {
                MinHeight = 44,
                Padding = new Thickness(10, 6, 10, 6),
                FontSize = 13,
                BorderThickness = new Thickness(1.5),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            // 统一样式（如果资源不存在不抛异常）
            comboBox.SetResourceReference(FrameworkElement.StyleProperty, "CompactComboBoxStyle");

            try
            {
                var itemsSourceAttribute = GetItemsSourceAttribute(propertyDescriptor);
                if (itemsSourceAttribute != null)
                {
                    SetItemsSource(comboBox, propertyDescriptor, itemsSourceAttribute);
                    if (!string.IsNullOrEmpty(itemsSourceAttribute.DisplayMemberPath))
                    {
                        comboBox.DisplayMemberPath = itemsSourceAttribute.DisplayMemberPath;
                    }
                }
                else if (propertyDescriptor.PropertyType != null && propertyDescriptor.PropertyType.IsEnum)
                {
                    comboBox.ItemsSource = Enum.GetValues(propertyDescriptor.PropertyType);
                }
            }
            catch (Exception)
            {
                comboBox.IsEnabled = false;
            }

            return comboBox;
        }

        public override void CreateBinding(PropertyDescriptor propertyDescriptor, DependencyObject element)
        {
            if (element is ComboBox comboBox)
            {
                var binding = new Binding("Value")
                {
                    Source = propertyDescriptor,
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                };

                comboBox.SetBinding(ComboBox.SelectedItemProperty, binding);
            }
        }

        public override DependencyProperty GetDependencyProperty()
        {
            return ComboBox.SelectedItemProperty;
        }

        private void SetItemsSource(ComboBox comboBox, PropertyDescriptor propertyDescriptor, ItemsSourceAttribute attribute)
        {
            var targetObject = GetTargetObject(propertyDescriptor);
            if (ItemsSourceResolver.TryResolve(attribute, targetObject, out var itemsSource))
            {
                comboBox.ItemsSource = itemsSource;
            }
        }
    }
}

