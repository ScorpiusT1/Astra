using Astra.Core.Configuration;
using Astra.Core.Configuration.Abstractions;
using Astra.Core.Configuration.Enums;
using Astra.Core.Configuration.Providers;
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
using Astra.Plugins.PLC.Configs;
using Astra.Plugins.PLC.Devices;
using Astra.Plugins.PLC.Interlock;
using Astra.Plugins.PLC.Services;
using Astra.Plugins.PLC.Triggers;
using Microsoft.Extensions.DependencyInjection;
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
        private bool _triggerObserverRegistered;
        private bool _disposed;
        private PlcHomeIoMonitorRuntime? _homeIoMonitor;

        public string Id => "Astra.Plugins.PLC";

        public string Name => "PLC 设备插件";

        public Version Version => new(1, 0, 0);

        public async Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            Current = this;
            _deviceManager = context.ServiceProvider.GetService<IDeviceManager>();
            _configurationManager = context.ServiceProvider.GetService<IConfigurationManager>();

            if (_configurationManager != null)
            {
                var generalOptions = new ConfigProviderOptions<PlcDeviceConfig>
                {
                    DefaultCollectionFileName = $"{Id}.General.config"
                };
                _configurationManager.RegisterProvider<PlcDeviceConfig>(options: generalOptions);

                var s7Options = new ConfigProviderOptions<S7SiemensPlcDeviceConfig>
                {
                    DefaultCollectionFileName = $"{Id}.S7.config"
                };
                _configurationManager.RegisterProvider<S7SiemensPlcDeviceConfig>(options: s7Options);

                var ioOptions = new ConfigProviderOptions<IOConfig>
                {
                    DefaultCollectionFileName = $"{Id}.IO.config"
                };
                _configurationManager.RegisterProvider<IOConfig>(options: ioOptions);

                var plcTriggerOptions = new ConfigProviderOptions<PlcTriggerConfig>
                {
                    DefaultCollectionFileName = $"{Id}.PlcTrigger.config"
                };
                _configurationManager.RegisterProvider<PlcTriggerConfig>(options: plcTriggerOptions);

                var loadResult = await _configurationManager.GetAllAsync<S7SiemensPlcDeviceConfig>();
                if (loadResult.Success && loadResult.Data != null)
                {
                    _devices.Clear();
                    foreach (var cfg in loadResult.Data)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        _devices.Add(new S7SiemensPlcDevice(cfg));
                    }
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
                return;
            }

            foreach (var device in _devices)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _deviceManager.RegisterDevice(device);
            }

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
