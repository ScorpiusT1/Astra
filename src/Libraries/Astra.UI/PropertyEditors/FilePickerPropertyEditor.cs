using Astra.UI.Abstractions.Attributes;
using Astra.UI.Abstractions.Models;
using Astra.UI.Controls;
using Microsoft.Win32;
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Astra.UI.PropertyEditors
{
    /// <summary>
    /// 单文件选择属性编辑器，基于 <see cref="FilePickerBox"/>。
    /// 配合 <see cref="FilePickerAttribute"/> 使用以指定对话框模式和过滤器。
    /// </summary>
    public class FilePickerPropertyEditor : PropertyEditorBase
    {
        public override FrameworkElement CreateElement(PropertyDescriptor propertyDescriptor)
        {
            var attr = propertyDescriptor?.PropertyInfo?.GetCustomAttribute<FilePickerAttribute>();

            var picker = new FilePickerBox();
            picker.SetBinding(FrameworkElement.WidthProperty, new Binding("ActualWidth")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(PropertyEditorHost), 1)
            });
            picker.TextBox.ToolTip = propertyDescriptor?.Description;

            picker.BrowseClick += (_, _) => OnBrowseClick(picker, propertyDescriptor, attr);
            picker.ClearClick += (_, _) =>
            {
                picker.Text = string.Empty;
                SetDescriptorValue(propertyDescriptor, string.Empty);
            };

            return picker;
        }

        public override void CreateBinding(PropertyDescriptor propertyDescriptor, DependencyObject element)
        {
            if (element is FilePickerBox picker)
            {
                var binding = new Binding("Value")
                {
                    Source = propertyDescriptor,
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                };
                picker.TextBox.SetBinding(TextBox.TextProperty, binding);
            }
        }

        public override DependencyProperty GetDependencyProperty()
        {
            return TextBox.TextProperty;
        }

        private static void OnBrowseClick(FilePickerBox picker, PropertyDescriptor descriptor, FilePickerAttribute attr)
        {
            var mode = attr?.Mode ?? FilePickerMode.Open;
            var filter = attr?.Filter ?? "所有文件 (*.*)|*.*";
            var title = attr?.Title;
            var defaultExt = attr?.DefaultExtension;

            var currentValue = picker.Text?.Trim() ?? string.Empty;
            string initialDirectory = null;
            string initialFileName = null;

            if (!string.IsNullOrEmpty(currentValue))
            {
                try
                {
                    var dir = Path.GetDirectoryName(currentValue);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                        initialDirectory = dir;
                    initialFileName = Path.GetFileName(currentValue);
                }
                catch { }
            }

            if (mode == FilePickerMode.Save)
            {
                var dlg = new SaveFileDialog { Filter = filter, OverwritePrompt = true };
                if (!string.IsNullOrEmpty(title)) dlg.Title = title;
                if (!string.IsNullOrEmpty(defaultExt)) dlg.DefaultExt = defaultExt;
                if (initialDirectory != null) dlg.InitialDirectory = initialDirectory;
                if (!string.IsNullOrEmpty(initialFileName)) dlg.FileName = initialFileName;

                if (dlg.ShowDialog() == true)
                {
                    picker.Text = dlg.FileName;
                    SetDescriptorValue(descriptor, dlg.FileName);
                }
            }
            else
            {
                var dlg = new OpenFileDialog { Filter = filter, Multiselect = false };
                if (!string.IsNullOrEmpty(title)) dlg.Title = title;
                if (initialDirectory != null) dlg.InitialDirectory = initialDirectory;
                if (!string.IsNullOrEmpty(initialFileName) && File.Exists(currentValue))
                    dlg.FileName = currentValue;

                if (dlg.ShowDialog() == true)
                {
                    picker.Text = dlg.FileName;
                    SetDescriptorValue(descriptor, dlg.FileName);
                }
            }
        }

        private static void SetDescriptorValue(PropertyDescriptor descriptor, string value)
        {
            if (descriptor == null) return;
            try { descriptor.Value = value; } catch { }
        }
    }
}
