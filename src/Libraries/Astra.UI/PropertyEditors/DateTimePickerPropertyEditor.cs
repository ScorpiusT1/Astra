using Astra.UI.Abstractions.Models;
using HandyControl.Controls;
using System;
using System.Windows;
using System.Windows.Data;

namespace Astra.UI.PropertyEditors
{
    /// <summary>
    /// DateTimePicker 属性编辑器
    /// 用于编辑 DateTime 和 DateTime? 类型的属性
    /// </summary>
    public class DateTimePickerPropertyEditor : PropertyEditorBase
    {
        public override FrameworkElement CreateElement(PropertyDescriptor propertyDescriptor)
        {
            var dateTimePicker = new DateTimePicker
            {
                MinHeight = 44,
                Padding = new Thickness(10, 6, 10, 6),
                FontSize = 13,
                BorderThickness = new Thickness(1.5)
            };

            // 设置圆角
            var cornerRadius = new CornerRadius(10);
            BorderElement.SetCornerRadius(dateTimePicker, cornerRadius);

            // 根据属性类型设置是否显示时间
            var propertyType = propertyDescriptor.PropertyType;
            if (propertyType == typeof(DateTime) || propertyType == typeof(DateTime?))
            {
                // DateTime 类型默认显示日期和时间
                //dateTimePicker.Tim = true;
            }
            // 如果未来需要支持 DateOnly，可以在这里添加判断

            return dateTimePicker;
        }

        public override void CreateBinding(PropertyDescriptor propertyDescriptor, DependencyObject element)
        {
            if (element is DateTimePicker dateTimePicker)
            {
                var binding = new Binding("Value")
                {
                    Source = propertyDescriptor,
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                };

                dateTimePicker.SetBinding(DateTimePicker.SelectedDateTimeProperty, binding);
            }
        }

        public override DependencyProperty GetDependencyProperty()
        {
            return DateTimePicker.SelectedDateTimeProperty;
        }
    }
}

