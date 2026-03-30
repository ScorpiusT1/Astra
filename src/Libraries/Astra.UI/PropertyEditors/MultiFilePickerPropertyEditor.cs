using Astra.UI.Abstractions.Attributes;
using Astra.UI.Abstractions.Models;
using Astra.UI.Controls;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Astra.UI.PropertyEditors
{
    /// <summary>
    /// 多文件选择属性编辑器。
    /// 使用 <see cref="FilePickerBox"/>，TextBox 以分号分隔显示所有已选文件路径。
    /// 绑定目标属性类型为 <see cref="List{T}"/> of string。
    /// </summary>
    public class MultiFilePickerPropertyEditor : PropertyEditorBase
    {
        private const char Separator = ';';

        public override FrameworkElement CreateElement(PropertyDescriptor propertyDescriptor)
        {
            var attr = propertyDescriptor?.PropertyInfo?.GetCustomAttribute<FilePickerAttribute>();

            var picker = new FilePickerBox();
            picker.SetBinding(FrameworkElement.WidthProperty, new Binding("ActualWidth")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(PropertyEditorHost), 1)
            });
            picker.TextBox.ToolTip = propertyDescriptor?.Description;
            picker.Text = JoinPaths(GetCurrentList(propertyDescriptor));

            picker.TextBox.LostFocus += (_, _) => SyncTextToDescriptor(picker, propertyDescriptor);

            picker.BrowseClick += (_, _) => OnBrowseClick(picker, propertyDescriptor, attr);
            picker.ClearClick += (_, _) =>
            {
                picker.Text = string.Empty;
                SetDescriptorValue(propertyDescriptor, new List<string>());
            };

            return picker;
        }

        public override void CreateBinding(PropertyDescriptor propertyDescriptor, DependencyObject element)
        {
        }

        public override DependencyProperty GetDependencyProperty()
        {
            return TextBox.TextProperty;
        }

        private static void OnBrowseClick(FilePickerBox picker, PropertyDescriptor descriptor, FilePickerAttribute attr)
        {
            var filter = attr?.Filter ?? "所有文件 (*.*)|*.*";
            var title = attr?.Title ?? "选择文件";

            var currentList = ParsePaths(picker.Text);
            string initialDirectory = null;
            if (currentList.Count > 0)
            {
                try
                {
                    var dir = Path.GetDirectoryName(currentList[currentList.Count - 1]);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                        initialDirectory = dir;
                }
                catch { }
            }

            var dlg = new OpenFileDialog
            {
                Filter = filter,
                Multiselect = true,
                Title = title,
            };
            if (initialDirectory != null)
                dlg.InitialDirectory = initialDirectory;

            if (dlg.ShowDialog() == true && dlg.FileNames.Length > 0)
            {
                var existing = new HashSet<string>(currentList, StringComparer.OrdinalIgnoreCase);
                foreach (var f in dlg.FileNames)
                {
                    if (existing.Add(f))
                        currentList.Add(f);
                }

                picker.Text = JoinPaths(currentList);
                SetDescriptorValue(descriptor, currentList);
            }
        }

        private static void SyncTextToDescriptor(FilePickerBox picker, PropertyDescriptor descriptor)
        {
            var list = ParsePaths(picker.Text);
            SetDescriptorValue(descriptor, list);
        }

        private static List<string> ParsePaths(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            return text.Split(Separator)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();
        }

        private static string JoinPaths(List<string> paths)
        {
            return string.Join(Separator + " ", paths);
        }

        private static List<string> GetCurrentList(PropertyDescriptor descriptor)
        {
            if (descriptor?.Value is List<string> list)
                return new List<string>(list);
            if (descriptor?.Value is IEnumerable<string> enumerable)
                return enumerable.ToList();
            return new List<string>();
        }

        private static void SetDescriptorValue(PropertyDescriptor descriptor, List<string> value)
        {
            if (descriptor == null) return;
            try { descriptor.Value = value; } catch { }
        }
    }
}
