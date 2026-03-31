using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace Astra.UI.ViewModels
{
    /// <summary>
    /// 下拉选项条目，携带 Value（写入配置的键）和 Label（界面显示文本）。
    /// </summary>
    public sealed class DropdownOptionItem
    {
        public string Value { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
    }

    /// <summary>
    /// WPF ComboBox 下拉状态辅助类。
    /// <para>
    /// 解决了 WPF <c>SelectedValue + SelectedValuePath</c> 在 <c>ItemsSource</c> 重建后无法自动回显选中值的已知问题：
    /// 通过直接写入 backing field 再触发 <see cref="INotifyPropertyChanged"/>，
    /// 使 WPF 见到引用从 <c>null</c> 变为匹配项而强制重新绑定，替代原来的字符串 SelectedValue 方案。
    /// </para>
    /// <para>
    /// 典型用法：
    /// <code>
    /// // ViewModel 构造
    /// MyDropdown = new DropdownState(v => Config.MyField = v ?? string.Empty);
    /// MyDropdown.Refresh(someItems, Config.MyField);
    ///
    /// // XAML
    /// &lt;ComboBox ItemsSource="{Binding MyDropdown.Options}"
    ///           DisplayMemberPath="Label"
    ///           SelectedItem="{Binding MyDropdown.SelectedItem}" /&gt;
    /// </code>
    /// </para>
    /// </summary>
    public sealed class DropdownState : INotifyPropertyChanged
    {
        private readonly Action<string?> _onValueChanged;
        private bool _isRefreshing;
        private DropdownOptionItem? _selectedItem;

        /// <summary>选项列表，直接绑定到 <c>ComboBox.ItemsSource</c>。</summary>
        public ObservableCollection<DropdownOptionItem> Options { get; } = new();

        /// <summary>当前选中项，直接绑定到 <c>ComboBox.SelectedItem</c>。</summary>
        public DropdownOptionItem? SelectedItem
        {
            get => _selectedItem;
            set
            {
                // ItemsSource.Clear() 触发的 null 写回，在刷新期间忽略
                if (_isRefreshing && value == null) return;
                if (ReferenceEquals(_selectedItem, value)) return;

                _selectedItem = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedItem)));
                _onValueChanged?.Invoke(value?.Value);
            }
        }

        /// <summary>当前选中的字符串值（即 <see cref="DropdownOptionItem.Value"/>），为 null 表示未选中。</summary>
        public string? CurrentValue => _selectedItem?.Value;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <param name="onValueChanged">
        /// 用户选择变更或 <see cref="Refresh"/> 导致值改变时触发的回调（写入 Config 等）。
        /// 参数为新值，若原值已被删除则传入 <c>null</c>。
        /// </param>
        public DropdownState(Action<string?> onValueChanged)
        {
            _onValueChanged = onValueChanged;
        }

        /// <summary>
        /// 重建选项列表并强制回显 <paramref name="currentValue"/>。
        /// <list type="bullet">
        ///   <item>若 <paramref name="currentValue"/> 在新列表中存在，选中该项。</item>
        ///   <item>若不存在，清空选中并以 <c>null</c> 回调（通知宿主清空 Config 字段）。</item>
        /// </list>
        /// <para>此方法必须在 UI 线程调用。</para>
        /// </summary>
        public void Refresh(IEnumerable<string> items, string? currentValue)
        {
            _isRefreshing = true;
            Options.Clear();
            foreach (var n in items)
                Options.Add(new DropdownOptionItem { Value = n, Label = n });
            _isRefreshing = false;

            var matched = Options.FirstOrDefault(x =>
                string.Equals(x.Value, currentValue, StringComparison.OrdinalIgnoreCase));

            // 直接写 backing field 再触发 PropertyChanged：
            // WPF 见到引用从 null 变为非 null（或不同引用），必定重新推送 SelectedItem，从而正确回显
            _selectedItem = matched;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedItem)));

            var finalValue = matched?.Value;
            if (!string.Equals(currentValue?.Trim(), finalValue, StringComparison.Ordinal))
                _onValueChanged?.Invoke(finalValue);
        }

        /// <summary>
        /// 从外部（如 Config.PropertyChanged）同步选中状态，不触发 <c>onValueChanged</c> 回调，避免循环写入。
        /// </summary>
        public void ApplySelection(string? value)
        {
            var normalized = value?.Trim() ?? string.Empty;
            var matched = Options.FirstOrDefault(x =>
                string.Equals(x.Value, normalized, StringComparison.OrdinalIgnoreCase));

            _selectedItem = matched;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedItem)));
        }
    }
}
