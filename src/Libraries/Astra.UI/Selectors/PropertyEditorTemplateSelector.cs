using Astra.UI.Controls;
using Astra.UI.Converters;
using Astra.UI.Abstractions.Attributes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using HandyControl.Controls;
using ComboBox = System.Windows.Controls.ComboBox;
using Astra.UI.PropertyEditors;

namespace Astra.UI.Selectors
{
    /// <summary>
    /// 属性编辑器模板选择器
    /// 统一流程：获取模板 → 获取数据源 → 创建/克隆模板 → 设置数据源
    /// </summary>
    public class PropertyEditorTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is Astra.UI.Abstractions.Models.PropertyDescriptor property && container is FrameworkElement element)
            {
                // 1. 自定义编辑器优先（统一流程）
                if (property.EditorType != null)
                {
                    // 检查是否是 PropertyEditorBase 类型
                    if (typeof(PropertyEditors.PropertyEditorBase).IsAssignableFrom(property.EditorType))
                    {
                        return CreatePropertyEditorBaseTemplate(property);
                    }

                    // 其他自定义编辑器（原有逻辑）
                    var propertyInfo = GetPropertyInfo(property);
                    if (propertyInfo != null)
                    {
                        return CreateCustomEditorTemplate(property, propertyInfo, element);
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

                // 4. 日期时间类型（如果没有指定自定义编辑器，自动使用 DateTimePickerPropertyEditor）
                if (property.PropertyType == typeof(DateTime) || property.PropertyType == typeof(DateTime?))
                {
                    if (property.EditorType == null)
                    {
                        // 临时设置 EditorType，用于创建模板
                        property.EditorType = typeof(DateTimePickerPropertyEditor);
                        return CreatePropertyEditorBaseTemplate(property);
                    }
                }

                // 5. 数字类型
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
            comboFactory.SetValue(ComboBox.HeightProperty, 44.0);
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

        #region PropertyEditorBase 支持

        /// <summary>
        /// 创建 PropertyEditorBase 类型的模板
        /// </summary>
        private DataTemplate CreatePropertyEditorBaseTemplate(
            Astra.UI.Abstractions.Models.PropertyDescriptor property)
        {
            var template = new DataTemplate { DataType = typeof(Astra.UI.Abstractions.Models.PropertyDescriptor) };

            // 创建 PropertyEditorHost 工厂
            var hostFactory = new FrameworkElementFactory(typeof(PropertyEditorHost));

            // 绑定 EditorType（从 PropertyDescriptor 获取）
            hostFactory.SetBinding(
                PropertyEditorHost.EditorTypeProperty,
                new Binding("EditorType") { Mode = BindingMode.OneWay });

            // 绑定 PropertyDescriptor（绑定到整个 DataContext，即 PropertyDescriptor 本身）
            // 不指定路径，直接绑定到 DataContext
            hostFactory.SetBinding(
                PropertyEditorHost.PropertyDescriptorProperty,
                new Binding { Mode = BindingMode.OneWay });

            template.VisualTree = hostFactory;
            return template;
        }

        #endregion

        #region 统一的自定义编辑器创建流程

        /// <summary>
        /// 创建自定义编辑器模板（统一流程）
        /// </summary>
        private DataTemplate CreateCustomEditorTemplate(
            Astra.UI.Abstractions.Models.PropertyDescriptor property,
            PropertyInfo propertyInfo,
            FrameworkElement element)
        {
            // 步骤1: 获取 Editor 模板（从资源中查找）
            var editorTemplate = GetEditorTemplate(element, property.EditorType);

            // 步骤2: 创建或克隆模板
            var template = CreateOrCloneTemplate(editorTemplate, property.EditorType);

            // 步骤3: 应用数据源配置（如果有）
            ApplyDataSourceConfiguration(template, property, propertyInfo);

            return template;
        }

        /// <summary>
        /// 创建或克隆模板
        /// </summary>
        private DataTemplate CreateOrCloneTemplate(DataTemplate editorTemplate, Type editorType)
        {
            if (editorTemplate != null)
            {
                // 克隆模板以便后续修改（避免影响原始模板）
                return DeepCloneTemplate(editorTemplate);
            }

            // 如果没有找到模板，创建默认模板
            return CreateDefaultEditorTemplate(editorType);
        }

        /// <summary>
        /// 应用数据源配置到模板
        /// </summary>
        private void ApplyDataSourceConfiguration(
            DataTemplate template,
            Astra.UI.Abstractions.Models.PropertyDescriptor property,
            PropertyInfo propertyInfo)
        {
            var dataSourceAttributes = ExtractDataSourceAttributes(propertyInfo);
            if (dataSourceAttributes.ItemsSourceAttribute == null)
            {
                return;
            }

            var itemsSource = GetItemsSource(property, dataSourceAttributes.ItemsSourceAttribute);
            var displayMemberPath = dataSourceAttributes.DisplayMemberPath
                ?? GetDefaultDisplayMemberPath(property);

            ApplyItemsSourceToTemplate(template, itemsSource, displayMemberPath, property);
        }

        /// <summary>
        /// 提取数据源相关特性
        /// </summary>
        private (ItemsSourceAttribute ItemsSourceAttribute, string DisplayMemberPath) ExtractDataSourceAttributes(PropertyInfo propertyInfo)
        {
            var itemsSourceAttribute = propertyInfo?.GetCustomAttribute<ItemsSourceAttribute>();

            // 使用 ItemsSourceAttribute.DisplayMemberPath（已包含此功能，无需单独的 DisplayMemberAttribute）
            return (itemsSourceAttribute, itemsSourceAttribute?.DisplayMemberPath);
        }

        #endregion

        #region 步骤1: 获取 Editor 模板

        /// <summary>
        /// 获取 Editor 模板（支持多种查找策略）
        /// </summary>
        private DataTemplate GetEditorTemplate(FrameworkElement element, Type editorType)
        {
            // 策略1: 使用 EditorType 的完整名称
            var template = element.TryFindResource(editorType.FullName) as DataTemplate;
            if (template != null)
                return template;

            // 策略2: 使用 EditorType 的简单名称
            template = element.TryFindResource(editorType.Name) as DataTemplate;
            if (template != null)
                return template;

            // 策略3: 移除 "Editor" 后缀后查找
            if (editorType.Name.EndsWith("Editor"))
            {
                var nameWithoutEditor = editorType.Name.Substring(0, editorType.Name.Length - 6);
                template = element.TryFindResource(nameWithoutEditor) as DataTemplate;
                if (template != null)
                    return template;
            }

            // 策略4: 查找命名空间相关的资源
            var namespaceParts = editorType.Namespace?.Split('.');
            if (namespaceParts != null && namespaceParts.Length > 0)
            {
                var lastNamespace = namespaceParts[namespaceParts.Length - 1];
                var resourceKey = $"{lastNamespace}.{editorType.Name}";
                template = element.TryFindResource(resourceKey) as DataTemplate;
                if (template != null)
                    return template;
            }

            return null;
        }

        #endregion

        #region 步骤2: 获取数据源

        /// <summary>
        /// 获取数据源
        /// </summary>
        private IEnumerable GetItemsSource(
            Astra.UI.Abstractions.Models.PropertyDescriptor property,
            ItemsSourceAttribute attribute)
        {

            // 静态类型方法/属性
            if (attribute.StaticType != null)
            {
                return GetStaticItemsSource(attribute);
            }
            else
            {
                // 实例属性/方法
                var targetObject = GetTargetObject(property);
                if (targetObject != null)
                {
                    return GetInstanceItemsSource(targetObject, attribute);
                }
            }

            return null;
        }

        /// <summary>
        /// 获取静态数据源
        /// </summary>
        private IEnumerable GetStaticItemsSource(ItemsSourceAttribute attribute)
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
                    return result as IEnumerable;
                }
            }
            else if (!string.IsNullOrEmpty(attribute.PropertyName))
            {
                var prop = attribute.StaticType.GetProperty(
                    attribute.PropertyName,
                    BindingFlags.Public | BindingFlags.Static);
                if (prop != null)
                {
                    return prop.GetValue(null) as IEnumerable;
                }
            }

            return null;
        }

        /// <summary>
        /// 获取实例数据源
        /// </summary>
        private IEnumerable GetInstanceItemsSource(object targetObject, ItemsSourceAttribute attribute)
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
                    return result as IEnumerable;
                }
            }
            else if (!string.IsNullOrEmpty(attribute.PropertyName))
            {
                var prop = targetObject.GetType().GetProperty(
                    attribute.PropertyName,
                    BindingFlags.Public | BindingFlags.Instance);
                if (prop != null)
                {
                    return prop.GetValue(targetObject) as IEnumerable;
                }
            }

            return null;
        }

        /// <summary>
        /// 获取默认显示成员路径
        /// </summary>
        private string GetDefaultDisplayMemberPath(Astra.UI.Abstractions.Models.PropertyDescriptor property)
        {
            // 根据属性类型推断默认显示路径
            var itemType = property.CollectionItemType ?? property.PropertyType;
            if (itemType != null)
            {
                // 常见的显示属性名
                var commonNames = new[] { "DisplayName", "Name", "Title", "Text", "Label" };
                foreach (var name in commonNames)
                {
                    var prop = itemType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                    if (prop != null)
                        return name;
                }
            }

            return "Name"; // 默认值
        }

        #endregion

        #region 步骤3: 创建/克隆模板

        /// <summary>
        /// 克隆模板（创建新模板并复制关键属性）
        /// </summary>
        private DataTemplate DeepCloneTemplate(DataTemplate sourceTemplate)
        {
            var clonedTemplate = new DataTemplate
            {
                DataType = sourceTemplate.DataType,
                VisualTree = sourceTemplate.VisualTree
            };

            if (sourceTemplate.Triggers != null)
            {
                foreach (var trigger in sourceTemplate.Triggers)
                {
                    clonedTemplate.Triggers.Add(trigger);
                }
            }

            return clonedTemplate;
        }

        /// <summary>
        /// 创建默认编辑器模板（当找不到资源模板时）
        /// </summary>
        private DataTemplate CreateDefaultEditorTemplate(Type editorType)
        {
            var template = new DataTemplate { DataType = typeof(Astra.UI.Abstractions.Models.PropertyDescriptor) };
            var factory = new FrameworkElementFactory(editorType);
            factory.SetBinding(FrameworkElement.DataContextProperty, new Binding());
            template.VisualTree = factory;
            return template;
        }

        #endregion

        #region 步骤4: 应用数据源到模板

        /// <summary>
        /// 将数据源应用到模板
        /// </summary>
        private void ApplyItemsSourceToTemplate(
            DataTemplate template,
            IEnumerable itemsSource,
            string displayMemberPath,
            Astra.UI.Abstractions.Models.PropertyDescriptor property)
        {
            if (template.VisualTree == null)
                return;

            // 查找支持 ItemsSource 的控件
            var itemsSourceControl = FindItemsSourceControl(template.VisualTree);
            if (itemsSourceControl != null)
            {
                // 设置 ItemsSource
                if (itemsSource != null)
                {
                    var itemsSourceProperty = GetItemsSourceProperty(itemsSourceControl.Type);
                    if (itemsSourceProperty != null)
                    {
                        itemsSourceControl.SetValue(itemsSourceProperty, itemsSource);
                    }
                }

                // 设置 DisplayMemberPath（如果控件支持）
                var displayMemberProperty = GetDisplayMemberPathProperty(itemsSourceControl.Type);
                if (displayMemberProperty != null && !string.IsNullOrEmpty(displayMemberPath))
                {
                    itemsSourceControl.SetValue(displayMemberProperty, displayMemberPath);
                }
            }
        }

        /// <summary>
        /// 在 VisualTree 中查找支持 ItemsSource 的控件
        /// 注意：由于 FrameworkElementFactory 的限制，我们只能检查根节点
        /// 对于嵌套结构，需要在 XAML 模板中确保 ItemsSource 控件在根级别
        /// </summary>
        private FrameworkElementFactory FindItemsSourceControl(FrameworkElementFactory root)
        {
            if (root == null)
                return null;

            // 检查当前节点是否支持 ItemsSource
            var itemsSourceProperty = GetItemsSourceProperty(root.Type);
            if (itemsSourceProperty != null)
            {
                return root;
            }

            return null;
        }

        /// <summary>
        /// 获取 ItemsSource 依赖属性
        /// </summary>
        private DependencyProperty GetItemsSourceProperty(Type controlType)
        {
            // 常见的支持 ItemsSource 的控件
            var commonControls = new Dictionary<Type, DependencyProperty>
            {
                { typeof(ComboBox), ComboBox.ItemsSourceProperty },
                { typeof(ListBox), ListBox.ItemsSourceProperty },
                { typeof(ListView), ListView.ItemsSourceProperty },
                { typeof(DataGrid), DataGrid.ItemsSourceProperty },
                { typeof(ItemsControl), ItemsControl.ItemsSourceProperty },
                { typeof(CheckComboBox), CheckComboBox.ItemsSourceProperty }
            };

            if (commonControls.TryGetValue(controlType, out var dp))
                return dp;

            // 尝试通过反射查找 ItemsSourceProperty
            var field = controlType.GetField("ItemsSourceProperty", BindingFlags.Public | BindingFlags.Static);
            if (field != null)
            {
                return field.GetValue(null) as DependencyProperty;
            }

            return null;
        }

        /// <summary>
        /// 获取 DisplayMemberPath 依赖属性
        /// </summary>
        private DependencyProperty GetDisplayMemberPathProperty(Type controlType)
        {
            var commonControls = new Dictionary<Type, DependencyProperty>
            {
                { typeof(ComboBox), ComboBox.DisplayMemberPathProperty },
                { typeof(ListBox), ListBox.DisplayMemberPathProperty },
                { typeof(ListView), ListView.DisplayMemberPathProperty },
                { typeof(CheckComboBox), CheckComboBox.DisplayMemberPathProperty }
            };

            if (commonControls.TryGetValue(controlType, out var dp))
                return dp;

            // 尝试通过反射查找 DisplayMemberPathProperty
            var field = controlType.GetField("DisplayMemberPathProperty", BindingFlags.Public | BindingFlags.Static);
            if (field != null)
            {
                return field.GetValue(null) as DependencyProperty;
            }

            return null;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取 PropertyInfo（使用内部属性，避免反射）
        /// </summary>
        private PropertyInfo GetPropertyInfo(Astra.UI.Abstractions.Models.PropertyDescriptor property)
        {
            return property?.PropertyInfo;
        }

        /// <summary>
        /// 获取目标对象（使用内部属性，避免反射）
        /// </summary>
        private object GetTargetObject(Astra.UI.Abstractions.Models.PropertyDescriptor property)
        {
            return property?.TargetObject;
        }

        #endregion

        private bool IsNumericType(Type type)
        {
            return type == typeof(int) || type == typeof(long) ||
                   type == typeof(float) || type == typeof(double) ||
                   type == typeof(decimal) || type == typeof(short) ||
                   type == typeof(byte);
        }
    }
}
