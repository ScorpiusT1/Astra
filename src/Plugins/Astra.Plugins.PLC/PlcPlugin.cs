using Astra.Core.Configuration;
using Astra.Core.Configuration.Abstractions;
using Astra.Core.Configuration.Enums;
using Astra.Core.Configuration.Helpers;
using Astra.Core.Configuration.Providers;
using Astra.Core.Logs;
using Astra.Core.Logs.Extensions;
using Astra.Core.Triggers;
using Astra.Core.Triggers.Interlock;
using Astra.Core.Triggers.Manager;
using Astra.Contract.Communication.Abstractions;
using Astra.Core.Devices.Abstractions;
using Astra.Core.Devices.Interfaces;
using Astra.Core.Devices.Management;
using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.Health;
using Astra.UI.Abstractions.Home;
using Astra.UI.Helpers;
using Astra.Plugins.PLC.Configs;
using Astra.Plugins.PLC.Devices;
using Astra.Plugins.PLC.Interlock;
using Astra.Plugins.PLC.Services;
using Astra.Plugins.PLC.Triggers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Astra.Plugins.PLC
{
    public class PlcPlugin : IPlugin
    {
        internal static PlcPlugin? Current { get; private set; }

        private IPluginContext? _context;
        private IDeviceManager? _deviceManager;
        private IConfigurationManager? _configurationManager;
        private readonly List<IDevice> _devices = new();
        private readonly List<IOConfig> _ioConfigs = new();
        private readonly HashSet<string> _registeredPlcTriggerIds = new(StringComparer.Ordinal);
        private Action<PlcTriggerConfig, ConfigChangeType>? _plcTriggerChangedHandler;
        private Action<IOConfig, ConfigChangeType>? _ioConfigChangedHandler;
        private Action<PlcDeviceConfig, ConfigChangeType>? _plcDeviceConfigChangedHandler;
        private bool _triggerObserverRegistered;
        private bool _disposed;
        private PlcHomeIoMonitorRuntime? _homeIoMonitor;

        // 用于序列化并发的设备同步任务，防止多次保存触发多个并发 Sync 导致 _devices 竞争损坏
        private readonly System.Threading.SemaphoreSlim _syncLock = new(1, 1);

        private Microsoft.Extensions.Logging.ILogger? _logger;

        public string Id => "Astra.Plugins.PLC";

        public string Name => "PLC 设备插件";

        public Version Version => new(1, 0, 0);

        public async Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            Current = this;
            _deviceManager = context.ServiceProvider.GetService<IDeviceManager>();
            _configurationManager = context.ServiceProvider.GetService<IConfigurationManager>();

            // 初始化日志器
            try
            {
                var loggerFactory = context.ServiceProvider?.GetService<ILoggerFactory>();
                _logger = loggerFactory != null ? loggerFactory.CreateLogger(Id) : NullLogger.Instance;
            }
            catch
            {
                _logger = NullLogger.Instance;
            }

            _logger?.LogInfo($"[{Name}] 开始初始化插件", LogCategory.System);

            // ──────────────────────────────────────────────────────────────
            // 设备工厂注册：新增品牌时在此追加一行 Register 调用即可，其余代码无需修改。
            // 例：PlcDeviceFactory.Register<OmronPlcDeviceConfig>(cfg => new OmronPlcDevice(cfg));
            // ──────────────────────────────────────────────────────────────
            PlcDeviceFactory.Register<S7SiemensPlcDeviceConfig>(cfg => new S7SiemensPlcDevice(cfg));

            if (_configurationManager != null)
            {
                var generalOptions = new ConfigProviderOptions<PlcDeviceConfig>
                {
                    DefaultCollectionFileName = ConfigFileNameHelper.GetDefaultCollectionFileName(typeof(PlcDeviceConfig))
                };
                _configurationManager.RegisterProvider<PlcDeviceConfig>(options: generalOptions);

                var s7Options = new ConfigProviderOptions<S7SiemensPlcDeviceConfig>
                {
                    DefaultCollectionFileName = ConfigFileNameHelper.GetDefaultCollectionFileName(typeof(S7SiemensPlcDeviceConfig))
                };
                _configurationManager.RegisterProvider<S7SiemensPlcDeviceConfig>(options: s7Options);

                var ioOptions = new ConfigProviderOptions<IOConfig>
                {
                    DefaultCollectionFileName = ConfigFileNameHelper.GetDefaultCollectionFileName(typeof(IOConfig))
                };
                _configurationManager.RegisterProvider<IOConfig>(options: ioOptions);

                var plcTriggerOptions = new ConfigProviderOptions<PlcTriggerConfig>
                {
                    DefaultCollectionFileName = ConfigFileNameHelper.GetDefaultCollectionFileName(typeof(PlcTriggerConfig))
                };
                _configurationManager.RegisterProvider<PlcTriggerConfig>(options: plcTriggerOptions);

                // 通用加载：通过工厂创建设备，无需关心具体品牌类型
                var allConfigs = await GetAllPlcDeviceConfigsFromStorageAsync().ConfigureAwait(false);
                _devices.Clear();
                foreach (var cfg in allConfigs)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var device = PlcDeviceFactory.Create(cfg);
                    if (device != null)
                        _devices.Add(device);
                    else
                        _logger?.LogWarn($"[{Name}] 无法为配置 {cfg.ConfigName ?? cfg.DeviceId} 创建设备，可能未注册对应工厂", LogCategory.Device);
                }

                var ioLoadResult = await _configurationManager.GetAllAsync<IOConfig>();
                if (ioLoadResult.Success && ioLoadResult.Data != null)
                {
                    _ioConfigs.Clear();
                    _ioConfigs.AddRange(ioLoadResult.Data);
                }

                _homeIoMonitor = new PlcHomeIoMonitorRuntime(this);
                IoMonitorRuntimeRegistry.Register(_homeIoMonitor);

                _plcTriggerChangedHandler = OnPlcTriggerConfigChanged;
                _configurationManager.Subscribe<PlcTriggerConfig>(_plcTriggerChangedHandler);

                _ioConfigChangedHandler = OnIoConfigChanged;
                _configurationManager.Subscribe<IOConfig>(_ioConfigChangedHandler);

                _plcDeviceConfigChangedHandler = OnPlcDeviceConfigChanged;
                _configurationManager.Subscribe<PlcDeviceConfig>(_plcDeviceConfigChangedHandler);

                _logger?.LogInfo($"[{Name}] 插件初始化完成，已加载 {_devices.Count} 个 PLC 设备", LogCategory.System);
            }

            if (_context.ServiceProvider.GetService(typeof(SafetyInterlockIoReaderBridge)) is SafetyInterlockIoReaderBridge bridge)
            {
                bridge.SetImplementation(new PlcSafetyInterlockIoReader());
            }

            if (_context.ServiceProvider.GetService(typeof(SafetyInterlockRulesProviderBridge)) is SafetyInterlockRulesProviderBridge rulesBridge)
            {
                rulesBridge.SetImplementation(new PlcSafetyInterlockRulesProvider());
            }
        }

        public async Task OnEnableAsync(CancellationToken cancellationToken = default)
        {
            if (_deviceManager == null)
            {
                _logger?.LogError($"[{Name}] DeviceManager 为 null，无法注册设备", null, LogCategory.System);
                return;
            }

            _logger?.LogInfo($"[{Name}] 启用插件，共有 {_devices.Count} 个 PLC 设备需要注册", LogCategory.System);

            int successCount = 0, failCount = 0;
            foreach (var device in _devices)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = _deviceManager.RegisterDevice(device);
                if (result.Success)
                {
                    successCount++;
                    _logger?.LogInfo($"[{Name}] 设备 {device.DeviceName} 注册成功", LogCategory.Device);
                }
                else
                {
                    failCount++;
                    _logger?.LogError($"[{Name}] 设备 {device.DeviceName} 注册失败: {result.Message}", null, LogCategory.Device);
                }
            }

            _logger?.LogInfo($"[{Name}] 插件已启用，成功注册 {successCount} 个设备，失败 {failCount} 个", LogCategory.System);

            var tm = _context?.ServiceProvider?.GetService<TriggerManager>();
            TryRegisterTriggerObserver(tm);
            await SyncPlcTriggersToManagerAsync(tm).ConfigureAwait(false);

            var interlockMonitor = _context?.ServiceProvider?.GetService(typeof(ISafetyInterlockMonitor)) as ISafetyInterlockMonitor;
            if (interlockMonitor != null)
            {
                await interlockMonitor.StartAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task OnDisableAsync(CancellationToken cancellationToken = default)
        {
            var interlockMonitor = _context?.ServiceProvider?.GetService(typeof(ISafetyInterlockMonitor)) as ISafetyInterlockMonitor;
            if (interlockMonitor != null)
            {
                await interlockMonitor.StopAsync(cancellationToken).ConfigureAwait(false);
            }

            if (_deviceManager == null)
            {
                return;
            }

            foreach (var device in _devices)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _deviceManager.UnregisterDevice(device.DeviceId);
            }
        }

        public Task<HealthCheckResult> CheckHealthAsync()
        {
            var sw = Stopwatch.StartNew();
            var total = _devices.Count;
            var online = _devices.Count(d => d.IsOnline);
            sw.Stop();

            if (total == 0)
            {
                return Task.FromResult(HealthCheckResult.Degraded(Name, "未加载到 PLC 设备配置", sw.Elapsed));
            }

            if (online == total)
            {
                return Task.FromResult(HealthCheckResult.Healthy(Name, $"PLC 设备全部在线 ({online}/{total})", sw.Elapsed));
            }

            return Task.FromResult(HealthCheckResult.Degraded(Name, $"PLC 设备部分在线 ({online}/{total})", sw.Elapsed));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        public ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }

            try
            {
                OnDisableAsync().GetAwaiter().GetResult();
            }
            catch
            {
                // 避免释放流程因注销失败中断
            }

            if (_context?.ServiceProvider?.GetService(typeof(SafetyInterlockIoReaderBridge)) is SafetyInterlockIoReaderBridge interlockBridge)
            {
                interlockBridge.SetImplementation(null);
            }

            if (_context?.ServiceProvider?.GetService(typeof(SafetyInterlockRulesProviderBridge)) is SafetyInterlockRulesProviderBridge rulesBridge)
            {
                rulesBridge.SetImplementation(null);
            }

            if (_configurationManager != null)
            {
                if (_plcTriggerChangedHandler != null)
                {
                    _configurationManager.Unsubscribe<PlcTriggerConfig>(_plcTriggerChangedHandler);
                    _plcTriggerChangedHandler = null;
                }

                if (_ioConfigChangedHandler != null)
                {
                    _configurationManager.Unsubscribe<IOConfig>(_ioConfigChangedHandler);
                    _ioConfigChangedHandler = null;
                }

                if (_plcDeviceConfigChangedHandler != null)
                {
                    _configurationManager.Unsubscribe<PlcDeviceConfig>(_plcDeviceConfigChangedHandler);
                    _plcDeviceConfigChangedHandler = null;
                }
            }

            _homeIoMonitor?.Detach();
            IoMonitorRuntimeRegistry.Register(null);
            _homeIoMonitor = null;

            foreach (var device in _devices.OfType<IDisposable>())
            {
                device.Dispose();
            }

            _devices.Clear();
            Current = null;
            _disposed = true;
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// 供插件内 UI 获取与宿主共享的 <see cref="IConfigurationManager"/>，用于订阅 PLC 设备配置变更等。
        /// </summary>
        public static IConfigurationManager? GetConfigurationManager() => Current?._configurationManager;

        /// <summary>
        /// 从持久化存储读取所有 PLC 设备配置（含各品牌子类）。
        /// 由 <see cref="PlcPlugin"/> 统一维护需要读取的类型列表，外部代码无需关心具体子类型。
        /// 新增 PLC 品牌时，在此方法内追加对应的 <c>GetAllAsync&lt;T&gt;</c> 调用即可。
        /// </summary>
        internal async Task<List<PlcDeviceConfig>> GetAllPlcDeviceConfigsFromStorageAsync()
        {
            var result = new List<PlcDeviceConfig>();
            if (_configurationManager == null) return result;

            // S7 西门子设备（目前唯一实现的品牌；新增品牌时在此追加）
            try
            {
                var s7 = await _configurationManager.GetAllAsync<S7SiemensPlcDeviceConfig>().ConfigureAwait(false);
                if (s7.Success && s7.Data != null)
                    result.AddRange(s7.Data);
            }
            catch { }

            return result;
        }

        internal IReadOnlyList<IPLC> GetAllPlcs()
        {
            return _devices.OfType<IPLC>().ToList().AsReadOnly();
        }

        internal IReadOnlyList<IOConfig> GetAllIoConfigs()
        {
            return _ioConfigs.AsReadOnly();
        }

        /// <summary>
        /// 与配置管理器或首页监控运行时拉取的列表对齐，避免 FindIoByName 等仍读旧缓存。
        /// </summary>
        internal void SyncIoConfigsCache(System.Collections.Generic.IEnumerable<IOConfig> configs)
        {
            _ioConfigs.Clear();
            _ioConfigs.AddRange(configs);
        }

        internal IoPointModel? FindIoByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var n = name.Trim();
            foreach (var cfg in _ioConfigs)
            {
                if (cfg?.IOs == null || cfg.IOs.Count == 0)
                {
                    continue;
                }

                var io = cfg.IOs.FirstOrDefault(i =>
                    i != null &&
                    i.IsEnabled &&
                    string.Equals(i.Name?.Trim(), n, StringComparison.OrdinalIgnoreCase));
                if (io != null)
                {
                    return io;
                }
            }

            return null;
        }

        /// <summary>
        /// 按设备配置中的显示名称查找已注册的 PLC。
        /// </summary>
        public IPLC? FindPlcByDeviceName(string? deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName))
            {
                return null;
            }

            var n = deviceName.Trim();
            return _devices.OfType<IPLC>().FirstOrDefault(p =>
                p is IDevice d && string.Equals(d.DeviceName, n, StringComparison.OrdinalIgnoreCase));
        }

        private void TryRegisterTriggerObserver(TriggerManager? tm)
        {
            if (tm == null || _context?.ServiceProvider == null || _triggerObserverRegistered)
            {
                return;
            }

            var observer = _context.ServiceProvider.GetService<ITriggerObserver>();
            if (observer == null)
            {
                return;
            }

            tm.RegisterObserver(observer);
            _triggerObserverRegistered = true;
        }

        private async Task SyncPlcTriggersToManagerAsync(TriggerManager? tm)
        {
            if (tm == null || _configurationManager == null)
            {
                return;
            }

            var result = await _configurationManager.GetAllAsync<PlcTriggerConfig>().ConfigureAwait(false);
            if (!result.Success || result.Data == null)
            {
                return;
            }

            var enabled = result.Data.Where(c => c != null && c.IsEnabled).ToList();
            var desiredIds = new HashSet<string>(
                enabled.Where(c => !string.IsNullOrWhiteSpace(c.ConfigId)).Select(c => c.ConfigId!),
                StringComparer.Ordinal);

            foreach (var id in _registeredPlcTriggerIds.ToArray())
            {
                if (!desiredIds.Contains(id))
                {
                    await tm.UnregisterTriggerAsync(id).ConfigureAwait(false);
                    _registeredPlcTriggerIds.Remove(id);
                }
            }

            foreach (var cfg in enabled)
            {
                if (string.IsNullOrWhiteSpace(cfg.ConfigId))
                {
                    continue;
                }

                var trigger = PlcTriggerFactory.TryCreate(cfg.PlcDeviceName, cfg.IoPointName);
                if (trigger == null)
                {
                    continue;
                }

                tm.RegisterTrigger(cfg.ConfigId, trigger);
                tm.ConfigureAntiRepeat(cfg.ConfigId, cfg.AntiRepeat);
                _registeredPlcTriggerIds.Add(cfg.ConfigId);
            }
        }

        private void OnPlcDeviceConfigChanged(PlcDeviceConfig cfg, ConfigChangeType changeType)
        {
            _ = SyncAfterPlcDeviceConfigChangedAsync();
        }

        private async Task SyncAfterPlcDeviceConfigChangedAsync()
        {
            // 序列化并发同步：当保存多个兄弟 PLC 节点时会触发多个此方法，
            // 通过信号量确保一次只运行一个，防止 _devices 列表并发损坏和 DeviceManager 重复注册冲突。
            await _syncLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _logger?.LogInfo($"[{Name}] 开始同步 PLC 设备配置", LogCategory.Device);

                // ① 先注销所有当前 PLC 设备（触发 DeviceUnregistered 事件，通知调试树等订阅者）
                if (_deviceManager != null)
                {
                    var oldIds = _devices.Select(d => d.DeviceId).ToList();
                    foreach (var id in oldIds)
                        _deviceManager.UnregisterDevice(id);
                    _logger?.LogInfo($"[{Name}] 已注销 {oldIds.Count} 个旧 PLC 设备", LogCategory.Device);
                }

                // ② 从持久化存储重新加载配置，通过工厂创建设备（支持所有已注册品牌）
                // 此时所有触发本轮同步的 SaveAsync 已完成写盘，读到的是最新状态
                var configs = await GetAllPlcDeviceConfigsFromStorageAsync().ConfigureAwait(false);

                // 保留 (配置, 设备) 对，方便后续在失败时给出带名称的提示
                var devicePairs = new List<(PlcDeviceConfig Config, IDevice Device)>();
                foreach (var cfg in configs)
                {
                    var device = PlcDeviceFactory.Create(cfg);
                    if (device != null)
                        devicePairs.Add((cfg, device));
                    else
                        _logger?.LogWarn($"[{Name}] 无法为配置 {cfg.ConfigName ?? cfg.DeviceId} 创建设备，可能未注册对应工厂", LogCategory.Device);
                }

                _devices.Clear();
                _devices.AddRange(devicePairs.Select(p => p.Device));
                _logger?.LogInfo($"[{Name}] 已从配置中加载 {devicePairs.Count} 个 PLC 设备", LogCategory.Device);

                // ③ 重新注册所有新设备，收集失败信息（触发 DeviceRegistered 事件，通知调试树等订阅者）
                if (_deviceManager != null)
                {
                    var registrationErrors = new List<string>();
                    foreach (var (cfg, device) in devicePairs)
                    {
                        var result = _deviceManager.RegisterDevice(device);
                        if (result.Success)
                        {
                            _logger?.LogInfo($"[{Name}] 设备 {device.DeviceName} 注册成功", LogCategory.Device);
                        }
                        else
                        {
                            _logger?.LogError($"[{Name}] 设备 {device.DeviceName} 注册失败: {result.Message}", null, LogCategory.Device);
                            registrationErrors.Add($"• {cfg.ConfigName ?? device.DeviceId}: {result.Message}");
                        }
                    }

                    if (registrationErrors.Count > 0)
                    {
                        var message = "部分 PLC 设备注册失败，请检查以下配置：\n"
                            + string.Join("\n", registrationErrors);
                        Application.Current?.Dispatcher.InvokeAsync(
                            () => ToastHelper.ShowError(message, "PLC 设备注册错误", 8),
                            DispatcherPriority.Normal);
                    }
                }

                _logger?.LogInfo($"[{Name}] PLC 设备配置同步完成", LogCategory.Device);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"[{Name}] PLC 设备配置同步失败: {ex.Message}", ex, LogCategory.Device);
                // 将意外异常也通知到界面，避免静默失败
                Application.Current?.Dispatcher.InvokeAsync(
                    () => ToastHelper.ShowError($"PLC 设备同步失败: {ex.Message}", "PLC 同步错误", 6),
                    DispatcherPriority.Normal);
            }
            finally
            {
                _syncLock.Release();
            }
        }

        private void OnIoConfigChanged(IOConfig cfg, ConfigChangeType changeType)
        {
            _ = SyncAfterIoConfigChangedAsync();
        }

        private async Task SyncAfterIoConfigChangedAsync()
        {
            try
            {
                if (_configurationManager == null)
                {
                    return;
                }

                var ioLoadResult = await _configurationManager.GetAllAsync<IOConfig>().ConfigureAwait(false);
                if (ioLoadResult.Success && ioLoadResult.Data != null)
                {
                    _ioConfigs.Clear();
                    _ioConfigs.AddRange(ioLoadResult.Data);
                }

                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null)
                {
                    return;
                }

                await dispatcher.InvokeAsync(
                    () => _homeIoMonitor?.ReloadFromConfiguration(),
                    DispatcherPriority.Normal);
            }
            catch
            {
                // 配置中间状态或 UI 未就绪时忽略，下次保存会再次同步。
            }
        }

        private void OnPlcTriggerConfigChanged(PlcTriggerConfig? cfg, ConfigChangeType changeType)
        {
            _ = SyncAfterPlcTriggerConfigChangedAsync();
        }

        private async Task SyncAfterPlcTriggerConfigChangedAsync()
        {
            try
            {
                var tm = _context?.ServiceProvider?.GetService<TriggerManager>();
                TryRegisterTriggerObserver(tm);
                await SyncPlcTriggersToManagerAsync(tm).ConfigureAwait(false);
                var lifecycle = _context?.ServiceProvider?.GetService<IAutoTriggerLifecycle>();
                if (lifecycle != null)
                {
                    await lifecycle.NotifyTriggersRegisteredAsync().ConfigureAwait(false);
                }
            }
            catch
            {
                // 配置中间状态或设备未就绪时忽略，下次保存会再次同步。
            }
        }
    }
}
