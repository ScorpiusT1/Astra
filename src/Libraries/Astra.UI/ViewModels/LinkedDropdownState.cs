using Astra.Core.Configuration.Abstractions;
using Astra.Core.Configuration.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Astra.UI.ViewModels
{
    /// <summary>
    /// 带配置订阅的下拉状态辅助类。
    /// <para>
    /// 在 <see cref="DropdownState"/> 的 UI 状态管理之上，额外封装了：
    /// <list type="bullet">
    ///   <item><b>订阅生命周期</b>：构造时自动订阅 <typeparamref name="TConfig"/> 的保存通知，<see cref="Dispose"/> 时自动反订阅。</item>
    ///   <item><b>异步拉取</b>：通知到达时后台拉取最新选项，再切回 UI 线程调用 <see cref="DropdownState.Refresh"/>。</item>
    ///   <item><b>防竞态</b>：版本号机制保证多次快速触发只保留最后一次结果。</item>
    /// </list>
    /// </para>
    /// <para>
    /// 典型用法：
    /// <code>
    /// // ViewModel 构造（实现 IDisposable）
    /// _ioLinked = new LinkedDropdownState&lt;IOConfig&gt;(
    ///     configManager:  mgr,
    ///     getCurrentValue: () => Config.IoPointName,
    ///     fetchItemsAsync: async () => await MyProvider.GetNamesAsync(),
    ///     onValueChanged:  v => Config.IoPointName = v ?? string.Empty);
    ///
    /// _ = _ioLinked.TriggerRefreshAsync(); // 首次加载
    ///
    /// // XAML
    /// &lt;ComboBox ItemsSource="{Binding IoLinked.Dropdown.Options}"
    ///           DisplayMemberPath="Label"
    ///           SelectedItem="{Binding IoLinked.Dropdown.SelectedItem}" /&gt;
    ///
    /// // ViewModel.Dispose
    /// _ioLinked.Dispose();
    /// </code>
    /// </para>
    /// </summary>
    /// <typeparam name="TConfig">订阅的配置类型，保存该类型时自动刷新选项列表。</typeparam>
    public sealed class LinkedDropdownState<TConfig> : IDisposable
        where TConfig : class, IConfig
    {
        private readonly DropdownState _dropdown;
        private readonly IConfigurationManager _configManager;
        private readonly Func<string> _getCurrentValue;
        private readonly Func<Task<IEnumerable<string>>> _fetchItemsAsync;
        private readonly Action<TConfig, ConfigChangeType> _configChangedHandler;
        private int _refreshVersion;
        private bool _disposed;

        /// <summary>可直接绑定到 ComboBox 的下拉状态。</summary>
        public DropdownState Dropdown => _dropdown;

        /// <param name="configManager">配置管理器，用于订阅/反订阅变更通知。</param>
        /// <param name="getCurrentValue">
        /// 读取当前配置值的委托（延迟求值，每次刷新时调用，确保取到最新值）。
        /// </param>
        /// <param name="fetchItemsAsync">
        /// 异步拉取最新选项列表的委托，在后台线程执行，结果会自动切回 UI 线程。
        /// </param>
        /// <param name="onValueChanged">
        /// 用户选择变更或刷新导致选中值改变时的回调（通常写入 Config 属性）。
        /// 若原值已被删除则传入 <c>null</c>。
        /// </param>
        public LinkedDropdownState(
            IConfigurationManager configManager,
            Func<string> getCurrentValue,
            Func<Task<IEnumerable<string>>> fetchItemsAsync,
            Action<string?> onValueChanged)
        {
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            _getCurrentValue = getCurrentValue ?? throw new ArgumentNullException(nameof(getCurrentValue));
            _fetchItemsAsync = fetchItemsAsync ?? throw new ArgumentNullException(nameof(fetchItemsAsync));
            _dropdown = new DropdownState(onValueChanged);

            _configChangedHandler = (_, _) => _ = TriggerRefreshAsync();
            _configManager.Subscribe<TConfig>(_configChangedHandler);
        }

        /// <summary>
        /// 手动触发一次异步刷新（首次加载或联动刷新时调用）。
        /// </summary>
        public Task TriggerRefreshAsync() => RefreshCoreAsync();

        private async Task RefreshCoreAsync()
        {
            var version = Interlocked.Increment(ref _refreshVersion);
            IEnumerable<string> items;
            try
            {
                items = await _fetchItemsAsync().ConfigureAwait(false);
            }
            catch
            {
                return;
            }

            var current = _getCurrentValue();

            await InvokeOnUiThreadAsync(() =>
            {
                if (version != _refreshVersion) return;
                _dropdown.Refresh(items, current);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 反订阅配置变更通知，释放订阅资源。
        /// ViewModel 销毁时应调用此方法。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _configManager.Unsubscribe<TConfig>(_configChangedHandler);
        }

        private static Task InvokeOnUiThreadAsync(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) { action(); return Task.CompletedTask; }
            if (dispatcher.CheckAccess()) { action(); return Task.CompletedTask; }
            return dispatcher.InvokeAsync(action, DispatcherPriority.Normal).Task;
        }
    }
}
