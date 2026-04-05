using Astra.Core.Nodes.Models;
using Astra.Plugins.DataImport.Nodes;
using Astra.Plugins.DataImport.ViewModels;
using Astra.UI.Abstractions.Nodes;
using Microsoft.Win32;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Astra.Plugins.DataImport.Views
{
    /// <summary>
    /// 文件导入节点自定义属性编辑面板。
    /// 宿主通过 <see cref="NodePropertyEditorAttribute"/> 发现并加载此 UserControl，
    /// 初始化时 DataContext 被设置为可编辑节点副本；Loaded 后替换为本面板专用 ViewModel。
    /// </summary>
    public partial class FileImportNodePropertyView : UserControl, INodePropertyEditor
    {
        private FileImportNodePropertyViewModel? _viewModel;

        public FileImportNodePropertyView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 宿主将可编辑副本设为 DataContext；在此创建 ViewModel 并替换。
            // 将 WpfPlot.Plot 引用和 Refresh 委托传入，ViewModel 直接操作图表对象。
            if (DataContext is FileImportRawNodeBase node)
            {
                _viewModel = new FileImportNodePropertyViewModel(
                    node,
                    WaveformPlot.Plot,
                    () => WaveformPlot.Refresh());
                DataContext = _viewModel;
            }
        }

        // ====== INodePropertyEditor ======

        /// <summary>
        /// 将 ViewModel 中的编辑结果写回目标节点（<c>[Display]</c> 属性已由窗口的通用反射同步处理）。
        /// </summary>
        public void Apply(Node target)
        {
            if (_viewModel != null && target is FileImportRawNodeBase importNode)
                _viewModel.Apply(importNode);
        }

        // ====== 按钮事件处理 ======

        private void OnAddFilesClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择数据文件",
                Filter = "NVH 数据文件 (*.tdms;*.wav)|*.tdms;*.wav" +
                         "|TDMS 文件 (*.tdms)|*.tdms" +
                         "|WAV 文件 (*.wav)|*.wav" +
                         "|所有文件 (*.*)|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
                _viewModel?.AddFiles(dialog.FileNames);
        }

        private void OnRemoveFileClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string path)
                _viewModel?.RemoveFile(path);
        }

        private void OnSelectAllClick(object sender, RoutedEventArgs e)
            => _viewModel?.SelectAllChannels();

        private void OnDeselectAllClick(object sender, RoutedEventArgs e)
            => _viewModel?.DeselectAllChannels();
    }

    // ====== 局部值转换器（View 层专用，避免依赖主程序转换器） ======

    /// <summary>bool → Visibility（true → Visible，false → Collapsed）</summary>
    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public static readonly BoolToVisibilityConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility.Visible;
    }

    /// <summary>bool → Visibility（取反：false → Visible，true → Collapsed）</summary>
    public sealed class BoolToVisibilityInverseConverter : IValueConverter
    {
        public static readonly BoolToVisibilityInverseConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is false ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is not Visibility.Visible;
    }

    /// <summary>集合 Count → Visibility（Count == 0 → Visible，否则 Collapsed；用于空提示行）</summary>
    public sealed class CountToVisibilityConverter : IValueConverter
    {
        public static readonly CountToVisibilityConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is int n && n == 0 ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
