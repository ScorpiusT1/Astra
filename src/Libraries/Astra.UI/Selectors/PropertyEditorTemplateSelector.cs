using Astra.UI.Controls;
using Astra.UI.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Astra.UI.Selectors
{
    /// <summary>
    /// 属性编辑器模板选择器
    /// </summary>
    public class PropertyEditorTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is Astra.UI.Abstractions.Models.PropertyDescriptor property && container is FrameworkElement element)
            {
                // 1. 自定义编辑器优先
                if (property.EditorType != null)
                {
                    try
                    {
                        var template = new DataTemplate { DataType = typeof(Astra.UI.Abstractions.Models.PropertyDescriptor) };
                        var factory = new FrameworkElementFactory(property.EditorType);
                        factory.SetBinding(FrameworkElement.DataContextProperty, new Binding());
                        template.VisualTree = factory;
                        return template;
                    }
                    catch
                    {
                        // 降级到默认编辑器
                    }
                }

                // 2. 布尔类型
                if (property.PropertyType == typeof(bool))
                {
                    return element.TryFindResource("BoolPropertyEditor") as DataTemplate;
                }

                // 3. 枚举类型
                if (property.PropertyType.IsEnum)
                {
                    return CreateEnumTemplate(property.PropertyType);
                }

                // 4. 数字类型
                if (IsNumericType(property.PropertyType))
                {
                    return element.TryFindResource("NumericPropertyEditor") as DataTemplate
                           ?? element.TryFindResource("DefaultPropertyEditor") as DataTemplate;
                }

                // 6. 默认编辑器
                return element.TryFindResource("DefaultPropertyEditor") as DataTemplate;
            }

            return base.SelectTemplate(item, container);
        }
        private DataTemplate CreateEnumTemplate(Type enumType)
        {
            var template = new DataTemplate { DataType = typeof(Astra.UI.Abstractions.Models.PropertyDescriptor) };
            var comboFactory = new FrameworkElementFactory(typeof(ComboBox));

            // 设置 ItemsSource（枚举值列表）
            comboFactory.SetValue(ComboBox.ItemsSourceProperty, Enum.GetValues(enumType));

            // 设置数据绑定（选中值）
            comboFactory.SetBinding(ComboBox.SelectedItemProperty,
                new Binding("Value") { Mode = BindingMode.TwoWay });
            
            // 设置只读状态
            comboFactory.SetBinding(ComboBox.IsEnabledProperty,
                new Binding("IsReadOnly") { Converter = new InverseBoolConverter() });

            // 🎨 设置样式（从资源中查找）
            comboFactory.SetResourceReference(ComboBox.StyleProperty, "CompactComboBoxStyle");
            
            // 设置固定高度和垂直对齐
            comboFactory.SetValue(ComboBox.HeightProperty, 40.0);
            comboFactory.SetValue(ComboBox.VerticalAlignmentProperty, VerticalAlignment.Center);
            comboFactory.SetValue(ComboBox.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);

            // 🌐 创建 ItemTemplate，使用转换器显示中文
            var itemTemplate = new DataTemplate { DataType = enumType };
            var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
            textBlockFactory.SetBinding(TextBlock.TextProperty,
                new Binding { Converter = EnumToDisplayTextConverter.Instance });
            itemTemplate.VisualTree = textBlockFactory;
            
            // 设置 ComboBox 的 ItemTemplate
            comboFactory.SetValue(ComboBox.ItemTemplateProperty, itemTemplate);

            template.VisualTree = comboFactory;
            return template;
        }

        private bool IsNumericType(Type type)
        {
            return type == typeof(int) || type == typeof(long) ||
                   type == typeof(float) || type == typeof(double) ||
                   type == typeof(decimal) || type == typeof(short) ||
                   type == typeof(byte);
        }
    }
}
