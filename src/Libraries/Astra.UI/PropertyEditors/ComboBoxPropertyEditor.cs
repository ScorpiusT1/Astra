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
                MinWidth = 0,
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

                    if (!string.IsNullOrEmpty(itemsSourceAttribute.SelectedValuePath))
                    {
                        comboBox.SelectedValuePath = itemsSourceAttribute.SelectedValuePath;
                    }

                    if (itemsSourceAttribute.IsEditable)
                    {
                        comboBox.IsEditable = true;
                        comboBox.StaysOpenOnEdit = true;
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
                var itemsSourceAttribute = GetItemsSourceAttribute(propertyDescriptor);
                var binding = new Binding("Value")
                {
                    Source = propertyDescriptor,
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                };

                if (itemsSourceAttribute?.IsEditable == true)
                {
                    comboBox.SetBinding(ComboBox.TextProperty, binding);
                    return;
                }

                if (!string.IsNullOrEmpty(itemsSourceAttribute?.SelectedValuePath))
                {
                    comboBox.SetBinding(ComboBox.SelectedValueProperty, binding);
                }
                else
                {
                    comboBox.SetBinding(ComboBox.SelectedItemProperty, binding);
                }
            }
        }

        public override DependencyProperty GetDependencyProperty()
        {
            return ComboBox.SelectedItemProperty;
        }

        private void SetItemsSource(ComboBox comboBox, PropertyDescriptor propertyDescriptor, ItemsSourceAttribute attribute)
        {
            var targetObject = GetTargetObject(propertyDescriptor);

            // [ItemsSource(nameof(SomeOptions))] 仅指定实例属性名时：用绑定，以便源对象上 PropertyChanged(SomeOptions) 后下拉项会刷新
            if (targetObject != null
                && !string.IsNullOrEmpty(attribute.PropertyName)
                && attribute.StaticType == null
                && attribute.ItemsSourceType == null
                && string.IsNullOrEmpty(attribute.MethodName))
            {
                comboBox.SetBinding(
                    ComboBox.ItemsSourceProperty,
                    new Binding(attribute.PropertyName)
                    {
                        Source = targetObject,
                        Mode = BindingMode.OneWay,
                    });
                return;
            }

            if (ItemsSourceResolver.TryResolve(attribute, targetObject, out var itemsSource))
            {
                comboBox.ItemsSource = itemsSource;
            }
        }
    }
}

