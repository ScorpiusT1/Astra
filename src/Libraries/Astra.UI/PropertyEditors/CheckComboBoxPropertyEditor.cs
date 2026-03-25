using Astra.UI.Abstractions.Attributes;
using Astra.UI.Abstractions.Interfaces;
using Astra.UI.Abstractions.Models;
using Astra.UI.Controls;
using Astra.UI.Services;
using HandyControl.Controls;
using System;
using System.Collections;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Astra.UI.PropertyEditors
{
    /// <summary>
    /// CheckComboBox 属性编辑器
    /// 示例实现，展示如何使用 PropertyEditorBase
    /// </summary>
    public class CheckComboBoxPropertyEditor : PropertyEditorBase
    {
        private static readonly IItemsSourceResolver ItemsSourceResolver = DefaultItemsSourceResolver.Instance;

        public override FrameworkElement CreateElement(PropertyDescriptor propertyDescriptor)
        {
            var comboBox = new CheckComboBox
            {
                MinHeight = 44,
                Padding = new Thickness(10, 6, 10, 6),
                FontSize = 13,
                ShowSelectAllButton = true,
              
                BorderThickness = new Thickness(1.5)
            };
     
            var cornerRadius = new CornerRadius(10);
            BorderElement.SetCornerRadius(comboBox, cornerRadius);
         
            try
            {
                // 获取 ItemsSourceAttribute 特性
                var itemsSourceAttribute = GetItemsSourceAttribute(propertyDescriptor);

                // 设置数据源
                if (itemsSourceAttribute != null)
                {
                    SetItemsSource(comboBox, propertyDescriptor, itemsSourceAttribute);

                    // 设置显示成员路径
                    if (!string.IsNullOrEmpty(itemsSourceAttribute.DisplayMemberPath))
                    {
                        comboBox.DisplayMemberPath = itemsSourceAttribute.DisplayMemberPath;
                    }
                }
                // 如果没有 ItemsSourceAttribute 但属性是枚举类型，则使用枚举值
                else if (propertyDescriptor.PropertyType != null && propertyDescriptor.PropertyType.IsEnum)
                {
                    comboBox.ItemsSource = Enum.GetValues(propertyDescriptor.PropertyType);
                }

                // 设置 PropertyDescriptor 到附加属性（用于同步 SelectedItems）
                CheckComboBoxSelectedItemsBehavior.SetPropertyDescriptor(comboBox, propertyDescriptor);
            }
            catch (Exception)
            {
                comboBox.IsEnabled = false;
            }

            // 返回 CheckComboBox（已通过 BorderElement.CornerRadius 设置圆角）
            return comboBox;
        }

        public override void CreateBinding(PropertyDescriptor propertyDescriptor, DependencyObject element)
        {
            // CheckComboBox 的绑定通过 CheckComboBoxSelectedItemsBehavior 处理
            // 这里不需要额外的绑定
        }

        public override DependencyProperty GetDependencyProperty()
        {
            // CheckComboBox 的 SelectedItems 通过附加属性同步
            // 返回一个占位符属性（实际不会使用）
            return FrameworkElement.DataContextProperty;
        }

        private void SetItemsSource(CheckComboBox comboBox, PropertyDescriptor propertyDescriptor, ItemsSourceAttribute attribute)
        {
            var targetObject = GetTargetObject(propertyDescriptor);
            if (ItemsSourceResolver.TryResolve(attribute, targetObject, out var itemsSource))
            {
                comboBox.ItemsSource = itemsSource;
            }
        }
    }
}

