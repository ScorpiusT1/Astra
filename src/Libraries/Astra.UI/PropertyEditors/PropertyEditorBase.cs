using Astra.UI.Abstractions.Models;
using System;
using System.Reflection;
using System.Windows;
using System.Windows.Data;

namespace Astra.UI.PropertyEditors
{
    /// <summary>
    /// 属性编辑器基类
    /// 允许用户创建自定义属性编辑器
    /// </summary>
    public abstract class PropertyEditorBase
    {
        /// <summary>
        /// 创建编辑器元素
        /// </summary>
        /// <param name="propertyDescriptor">属性描述符</param>
        /// <returns>创建的 UI 元素</returns>
        public abstract FrameworkElement CreateElement(PropertyDescriptor propertyDescriptor);

        /// <summary>
        /// 创建数据绑定
        /// </summary>
        /// <param name="propertyDescriptor">属性描述符</param>
        /// <param name="element">UI 元素</param>
        public abstract void CreateBinding(PropertyDescriptor propertyDescriptor, DependencyObject element);

        /// <summary>
        /// 获取要绑定的依赖属性
        /// </summary>
        /// <returns>依赖属性</returns>
        public abstract DependencyProperty GetDependencyProperty();

        /// <summary>
        /// 获取更新源触发器（可选重写）
        /// </summary>
        /// <param name="propertyDescriptor">属性描述符</param>
        /// <returns>更新源触发器</returns>
        protected virtual UpdateSourceTrigger GetUpdateSourceTrigger(PropertyDescriptor propertyDescriptor)
        {
            return UpdateSourceTrigger.PropertyChanged;
        }

        /// <summary>
        /// 获取值转换器（可选重写）
        /// </summary>
        /// <param name="propertyDescriptor">属性描述符</param>
        /// <returns>值转换器</returns>
        protected virtual IValueConverter GetConverter(PropertyDescriptor propertyDescriptor)
        {
            return null;
        }

        /// <summary>
        /// 获取 ItemsSourceAttribute 特性
        /// </summary>
        /// <param name="propertyDescriptor">属性描述符</param>
        /// <returns>ItemsSourceAttribute 特性</returns>
        protected Abstractions.Attributes.ItemsSourceAttribute GetItemsSourceAttribute(PropertyDescriptor propertyDescriptor)
        {
            var propertyInfo = GetPropertyInfo(propertyDescriptor);
            if (propertyInfo != null)
            {
                return propertyInfo.GetCustomAttribute<Abstractions.Attributes.ItemsSourceAttribute>();
            }
            return null;
        }

        /// <summary>
        /// 获取 PropertyInfo（使用内部属性，避免反射）
        /// </summary>
        protected PropertyInfo GetPropertyInfo(PropertyDescriptor propertyDescriptor)
        {
            return propertyDescriptor?.PropertyInfo;
        }

        /// <summary>
        /// 获取目标对象（使用内部属性，避免反射）
        /// </summary>
        protected object GetTargetObject(PropertyDescriptor propertyDescriptor)
        {
            return propertyDescriptor?.TargetObject;
        }
    }
}

