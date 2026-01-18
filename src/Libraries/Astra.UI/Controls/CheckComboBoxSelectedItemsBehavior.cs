using Astra.UI.Abstractions.Models;
using HandyControl.Controls;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;

namespace Astra.UI.Controls
{
    /// <summary>
    /// CheckComboBox SelectedItems 同步行为
    /// </summary>
    public static class CheckComboBoxSelectedItemsBehavior
    {
        private static readonly Dictionary<CheckComboBox, PropertyDescriptor> _attachedProperties = new();
        private static readonly Dictionary<CheckComboBox, NotifyCollectionChangedEventHandler> _collectionChangedHandlers = new();

        public static readonly DependencyProperty PropertyDescriptorProperty =
            DependencyProperty.RegisterAttached(
                "PropertyDescriptor",
                typeof(PropertyDescriptor),
                typeof(CheckComboBoxSelectedItemsBehavior),
                new PropertyMetadata(null, OnPropertyDescriptorChanged));

        public static PropertyDescriptor GetPropertyDescriptor(DependencyObject obj)
        {
            return (PropertyDescriptor)obj.GetValue(PropertyDescriptorProperty);
        }

        public static void SetPropertyDescriptor(DependencyObject obj, PropertyDescriptor value)
        {
            obj.SetValue(PropertyDescriptorProperty, value);
        }

        private static void OnPropertyDescriptorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not CheckComboBox checkComboBox)
                return;

            var oldPropertyDescriptor = e.OldValue as PropertyDescriptor;
            var newPropertyDescriptor = e.NewValue as PropertyDescriptor;

            // 清理旧的事件订阅
            if (oldPropertyDescriptor != null)
            {
                Cleanup(checkComboBox);
            }

            if (newPropertyDescriptor == null)
                return;

            // 保存关联
            _attachedProperties[checkComboBox] = newPropertyDescriptor;

            // 初始化：从属性值加载选中项
            LoadSelectedItems(checkComboBox, newPropertyDescriptor);

            // 监听属性值变化
            newPropertyDescriptor.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(PropertyDescriptor.Value))
                {
                    LoadSelectedItems(checkComboBox, newPropertyDescriptor);
                }
            };

            // 监听 CheckComboBox 的 SelectedItems 集合变化
            // 注意：HandyControl 的 CheckComboBox 没有 SelectedItemsChanged 事件
            // 我们需要监听 SelectedItems 集合的 CollectionChanged 事件
            if (checkComboBox.SelectedItems is INotifyCollectionChanged notifyCollection)
            {
                NotifyCollectionChangedEventHandler handler = (sender, args) =>
                {
                    if (args.Action != NotifyCollectionChangedAction.Move) // 忽略移动操作
                    {
                        UpdatePropertyValue(checkComboBox, newPropertyDescriptor);
                    }
                };
                
                notifyCollection.CollectionChanged += handler;
                _collectionChangedHandlers[checkComboBox] = handler;
            }

            // 监听控件卸载事件，清理资源
            checkComboBox.Unloaded += OnCheckComboBoxUnloaded;
        }

        private static void OnCheckComboBoxUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is CheckComboBox checkComboBox)
            {
                Cleanup(checkComboBox);
            }
        }

        private static void Cleanup(CheckComboBox checkComboBox)
        {
            // 移除集合变化监听
            if (_collectionChangedHandlers.TryGetValue(checkComboBox, out var handler))
            {
                if (checkComboBox.SelectedItems is INotifyCollectionChanged notifyCollection)
                {
                    notifyCollection.CollectionChanged -= handler;
                }
                _collectionChangedHandlers.Remove(checkComboBox);
            }

            // 移除属性关联
            _attachedProperties.Remove(checkComboBox);

            // 移除卸载事件
            checkComboBox.Unloaded -= OnCheckComboBoxUnloaded;
        }

        private static bool _isUpdatingSelectedItems = false; // 防止循环更新

        private static void LoadSelectedItems(CheckComboBox checkComboBox, PropertyDescriptor propertyDescriptor)
        {
            // 防止循环更新
            if (_isUpdatingSelectedItems)
                return;

            // 延迟加载，确保控件完全初始化后再设置 SelectedItems
            if (!checkComboBox.IsLoaded)
            {
                checkComboBox.Loaded += (s, e) => LoadSelectedItemsInternal(checkComboBox, propertyDescriptor);
                return;
            }

            // 使用 DispatcherPriority.Background 避免死锁
            checkComboBox.Dispatcher.BeginInvoke(
                new Action(() => LoadSelectedItemsInternal(checkComboBox, propertyDescriptor)),
                System.Windows.Threading.DispatcherPriority.Background);
        }

        private static void LoadSelectedItemsInternal(CheckComboBox checkComboBox, PropertyDescriptor propertyDescriptor)
        {
            if (_isUpdatingSelectedItems)
                return;

            try
            {
                _isUpdatingSelectedItems = true;

                if (propertyDescriptor.Value == null)
                {
                    // 只有在 SelectedItems 不为 null 且集合不为空时才清除
                    if (checkComboBox.SelectedItems != null && checkComboBox.SelectedItems.Count > 0)
                    {
                        try
                        {
                            checkComboBox.SelectedItems.Clear();
                        }
                        catch (InvalidOperationException)
                        {
                            // 如果失败，说明控件还未准备好，忽略错误
                        }
                    }
                    return;
                }

                var currentCollection = propertyDescriptor.Value as IEnumerable;
                if (currentCollection == null)
                    return;

                // 收集要选中的项
                var selectedItems = new List<object>();
                foreach (var item in currentCollection)
                {
                    selectedItems.Add(item);
                }

                // 更新 CheckComboBox 的选中项
                if (checkComboBox.SelectedItems != null)
                {
                    try
                    {
                        // 先清除现有项
                        checkComboBox.SelectedItems.Clear();
                        
                        // 然后添加新项
                        foreach (var item in selectedItems)
                        {
                            checkComboBox.SelectedItems.Add(item);
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"加载 CheckComboBox 选中项失败: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadSelectedItems 出错: {ex.Message}");
            }
            finally
            {
                _isUpdatingSelectedItems = false;
            }
        }

        private static void UpdatePropertyValue(CheckComboBox checkComboBox, PropertyDescriptor propertyDescriptor)
        {
            // 防止循环更新
            if (_isUpdatingSelectedItems || checkComboBox.SelectedItems == null)
                return;

            try
            {
                _isUpdatingSelectedItems = true;

                var itemType = propertyDescriptor.CollectionItemType ?? typeof(object);
                var selectedItemsList = checkComboBox.SelectedItems.Cast<object>().ToList();

                // 创建集合实例
                object collection;
                if (propertyDescriptor.PropertyType.IsArray)
                {
                    var array = Array.CreateInstance(itemType, selectedItemsList.Count);
                    for (int i = 0; i < selectedItemsList.Count; i++)
                    {
                        array.SetValue(selectedItemsList[i], i);
                    }
                    collection = array;
                }
                else if (propertyDescriptor.PropertyType.IsGenericType)
                {
                    var genericType = propertyDescriptor.PropertyType.GetGenericTypeDefinition();
                    if (genericType == typeof(ObservableCollection<>))
                    {
                        var listType = typeof(ObservableCollection<>).MakeGenericType(itemType);
                        collection = Activator.CreateInstance(listType);
                        var addMethod = listType.GetMethod("Add");
                        foreach (var item in selectedItemsList)
                        {
                            addMethod.Invoke(collection, new[] { item });
                        }
                    }
                    else
                    {
                        var listType = typeof(List<>).MakeGenericType(itemType);
                        collection = Activator.CreateInstance(listType);
                        var addMethod = listType.GetMethod("Add");
                        foreach (var item in selectedItemsList)
                        {
                            addMethod.Invoke(collection, new[] { item });
                        }
                    }
                }
                else
                {
                    collection = new System.Collections.ArrayList(selectedItemsList);
                }

                propertyDescriptor.Value = collection;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新集合属性值失败: {ex.Message}");
            }
            finally
            {
                _isUpdatingSelectedItems = false;
            }
        }
    }
}

