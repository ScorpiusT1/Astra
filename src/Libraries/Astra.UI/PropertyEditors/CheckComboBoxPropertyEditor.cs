using Astra.UI.Abstractions.Attributes;
using Astra.UI.Abstractions.Interfaces;
using Astra.UI.Abstractions.Models;
using Astra.UI.Controls;
using Astra.UI.Services;
using HandyControl.Controls;
using System;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using PropertyDescriptor = Astra.UI.Abstractions.Models.PropertyDescriptor;

namespace Astra.UI.PropertyEditors
{
    /// <summary>
    /// CheckComboBox 属性编辑器
    /// </summary>
    public class CheckComboBoxPropertyEditor : PropertyEditorBase
    {
        private static readonly IItemsSourceResolver ItemsSourceResolver = DefaultItemsSourceResolver.Instance;

        public override FrameworkElement CreateElement(PropertyDescriptor propertyDescriptor)
        {
            var comboBox = new CheckComboBox
            {
                MinWidth = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
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

                CheckComboBoxSelectedItemsBehavior.SetPropertyDescriptor(comboBox, propertyDescriptor);
            }
            catch (Exception)
            {
                comboBox.IsEnabled = false;
            }

            return comboBox;
        }

        public override void CreateBinding(PropertyDescriptor propertyDescriptor, DependencyObject element)
        {
        }

        public override DependencyProperty GetDependencyProperty()
        {
            return FrameworkElement.DataContextProperty;
        }

        private void SetItemsSource(CheckComboBox comboBox, PropertyDescriptor propertyDescriptor, ItemsSourceAttribute attribute)
        {
            var targetObject = GetTargetObject(propertyDescriptor);

            if (targetObject != null
                && !string.IsNullOrEmpty(attribute.PropertyName)
                && attribute.StaticType == null
                && attribute.ItemsSourceType == null
                && string.IsNullOrEmpty(attribute.MethodName))
            {
                var prop = targetObject.GetType().GetProperty(
                    attribute.PropertyName,
                    BindingFlags.Public | BindingFlags.Instance);

                if (prop != null)
                {
                    comboBox.ItemsSource = prop.GetValue(targetObject) as IEnumerable;
                }

                if (targetObject is INotifyPropertyChanged inpc)
                {
                    var propName = attribute.PropertyName;
                    PropertyChangedEventHandler handler = null;
                    handler = (s, e) =>
                    {
                        if (e.PropertyName != propName) return;
                        if (!comboBox.IsLoaded && !comboBox.IsVisible)
                        {
                            inpc.PropertyChanged -= handler;
                            return;
                        }
                        comboBox.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (prop != null)
                                comboBox.ItemsSource = prop.GetValue(s) as IEnumerable;
                        }));
                    };
                    inpc.PropertyChanged += handler;

                    comboBox.Unloaded += (_, __) => inpc.PropertyChanged -= handler;
                }
                return;
            }

            if (ItemsSourceResolver.TryResolve(attribute, targetObject, out var itemsSource))
            {
                comboBox.ItemsSource = itemsSource;
            }
        }
    }
}

