using Astra.UI.Abstractions.Attributes;
using Astra.UI.Abstractions.Interfaces;
using Astra.UI.Abstractions.Models;
using Astra.UI.Controls;
using HandyControl.Controls;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
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
            // 1. 如果是枚举类型
            if (attribute.ItemsSourceType?.IsEnum == true)
            {
                comboBox.ItemsSource = Enum.GetValues(attribute.ItemsSourceType);
            }
            // 2. 如果是从静态字段或属性获取
            else if (attribute.ItemsSourceType != null && !string.IsNullOrEmpty(attribute.Path))
            {
                var field = attribute.ItemsSourceType.GetField(attribute.Path,
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    comboBox.ItemsSource = field.GetValue(null) as IEnumerable;
                }
                else
                {
                    var property = attribute.ItemsSourceType.GetProperty(attribute.Path,
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (property != null)
                    {
                        comboBox.ItemsSource = property.GetValue(null, null) as IEnumerable;
                    }
                }
            }
            // 3. 如果是从 IItemsSourceProvider 获取
            else if (attribute.ItemsSourceType != null && typeof(IItemsSourceProvider).IsAssignableFrom(attribute.ItemsSourceType))
            {
                IItemsSourceProvider provider = Activator.CreateInstance(attribute.ItemsSourceType) as IItemsSourceProvider;
                if (provider != null)
                {
                    comboBox.ItemsSource = provider.GetItemsSource();
                }
            }
            // 4. 如果是从静态类型的方法/属性获取
            else if (attribute.StaticType != null)
            {
                if (!string.IsNullOrEmpty(attribute.MethodName))
                {
                    var method = attribute.StaticType.GetMethod(
                        attribute.MethodName,
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        Type.EmptyTypes,
                        null);
                    if (method != null)
                    {
                        var result = method.Invoke(null, null);
                        comboBox.ItemsSource = result as IEnumerable;
                    }
                }
                else if (!string.IsNullOrEmpty(attribute.PropertyName))
                {
                    var property = attribute.StaticType.GetProperty(
                        attribute.PropertyName,
                        BindingFlags.Public | BindingFlags.Static);
                    if (property != null)
                    {
                        comboBox.ItemsSource = property.GetValue(null) as IEnumerable;
                    }
                }
            }
            // 5. 如果是从实例属性/方法获取
            else
            {
                var targetObject = GetTargetObject(propertyDescriptor);
                if (targetObject != null)
                {
                    if (!string.IsNullOrEmpty(attribute.MethodName))
                    {
                        var method = targetObject.GetType().GetMethod(
                            attribute.MethodName,
                            BindingFlags.Public | BindingFlags.Instance,
                            null,
                            Type.EmptyTypes,
                            null);
                        if (method != null)
                        {
                            var result = method.Invoke(targetObject, null);
                            comboBox.ItemsSource = result as IEnumerable;
                        }
                    }
                    else if (!string.IsNullOrEmpty(attribute.PropertyName))
                    {
                        var property = targetObject.GetType().GetProperty(
                            attribute.PropertyName,
                            BindingFlags.Public | BindingFlags.Instance);
                        if (property != null)
                        {
                            comboBox.ItemsSource = property.GetValue(targetObject) as IEnumerable;
                        }
                    }
                }
            }
        }
    }
}

