using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Astra.Core.Devices.Abstractions;
using Astra.Core.Devices.Base;
using Astra.Core.Devices.Configuration;
using Astra.Core.Devices.Interfaces;
using Astra.Core.Foundation.Common;
using Astra.Core.Logs;
using Astra.Core.Logs.Extensions;
using Microsoft.Extensions.Logging;

namespace Astra.Core.Devices.Management
{
    /// <summary>
    /// 通用设备注册表基类，负责加载、注册、卸载设备
    /// </summary>
    public abstract class DeviceRegistryBase<TConfig, TDevice> : IDisposable
        where TConfig : DeviceConfig
        where TDevice : DeviceBase<TConfig>
    {
        private readonly ConcurrentDictionary<string, TDevice> _devices = new();
        private readonly IReadOnlyList<IDeviceFactory> _factories;
        private readonly IServiceProvider _serviceProvider;

        protected DeviceRegistryBase(
            IEnumerable<IDeviceFactory> factories,
            IDeviceManager deviceManager = null,
            Microsoft.Extensions.Logging.ILogger logger = null,
            IServiceProvider serviceProvider = null)
        {
            _factories = factories?.ToList() ?? throw new ArgumentNullException(nameof(factories));
            if (_factories.Count == 0)
            {
                throw new ArgumentException("未提供任何设备工厂实例", nameof(factories));
            }

            DeviceManager = deviceManager;
            Logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected IDeviceManager DeviceManager { get; }
        protected Microsoft.Extensions.Logging.ILogger Logger { get; }

        public event EventHandler<TDevice> DeviceLoaded;
        public event EventHandler<string> DeviceUnregistered;

        public IReadOnlyCollection<TDevice> Devices => _devices.Values.ToList();

        public TDevice? GetDevice(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return null;
            }

            return _devices.TryGetValue(deviceId, out var device) ? device : null;
        }

        public async Task ReloadAsync(IEnumerable<TConfig> configs, CancellationToken cancellationToken = default)
        {
            DisposeAll();
            await LoadDevicesAsync(configs, cancellationToken).ConfigureAwait(false);
        }

        public async Task ReloadAsync(CancellationToken cancellationToken = default)
        {
            var configs = await GetLatestConfigsAsync(cancellationToken).ConfigureAwait(false);
            await ReloadAsync(configs, cancellationToken).ConfigureAwait(false);
        }

        protected virtual Task<IReadOnlyList<TConfig>> GetLatestConfigsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<TConfig>>(Array.Empty<TConfig>());
        }

        public async Task LoadDevicesAsync(IEnumerable<TConfig> configs, CancellationToken cancellationToken = default)
        {
            if (configs == null)
            {
                return;
            }

            foreach (var config in configs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var factory = SelectFactory(config);
                if (factory == null)
                {
                    Logger?.LogWarn($"未找到可用于配置 {config.DeviceName} ({config.DeviceId}) 的设备工厂", LogCategory.Device);
                    continue;
                }

                TDevice device = null;
                try
                {
                    var created = factory.Create(config, _serviceProvider);
                    device = created as TDevice;

                    if (device == null)
                    {
                        Logger?.LogWarn($"工厂 {factory.GetType().Name} 创建的设备类型不匹配，已忽略", LogCategory.Device);
                        created?.Dispose();
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogError($"创建设备失败: {ex.Message}", ex, LogCategory.Device);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(device.DeviceId))
                {
                    device.Dispose();
                    Logger?.LogWarn("设备 DeviceId 为空，已忽略该配置", LogCategory.Device);
                    continue;
                }

                if (!_devices.TryAdd(device.DeviceId, device))
                {
                    device.Dispose();
                    Logger?.LogWarn($"设备 {device.DeviceId} 已存在，忽略重复配置", LogCategory.Device);
                    continue;
                }

                var initialized = false;
                try
                {
                    initialized = await InitializeDeviceAsync(device, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger?.LogError($"设备 {device.DeviceName} 初始化异常: {ex.Message}", ex, LogCategory.Device);
                }

                if (!initialized)
                {
                    _devices.TryRemove(device.DeviceId, out _);
                    device.Dispose();
                    OnDeviceLoadFailed(device, "初始化失败");
                    continue;
                }

                RegisterWithDeviceManager(device);
                OnDeviceLoaded(device);
            }
        }

        public async Task StartAutoDevicesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var device in Devices)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (ShouldAutoStart(device))
                {
                    await StartDeviceAsync(device, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        public async Task StartAllAsync(CancellationToken cancellationToken = default)
        {
            foreach (var device in Devices)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await StartDeviceAsync(device, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task StopAllAsync(CancellationToken cancellationToken = default)
        {
            foreach (var device in Devices)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await StopDeviceAsync(device).ConfigureAwait(false);
            }
        }

        public bool UnregisterDevice(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return false;
            }

            if (_devices.TryRemove(deviceId, out var device))
            {
                try
                {
                    DeviceManager?.UnregisterDevice(deviceId);
                }
                catch (Exception ex)
                {
                    Logger?.LogWarn($"注销设备 {deviceId} 时发生异常: {ex.Message}", LogCategory.Device);
                }

                device.Dispose();
                OnDeviceUnregistered(deviceId);
                return true;
            }

            return false;
        }

        public void DisposeAll()
        {
            foreach (var kvp in _devices.ToList())
            {
                var deviceId = kvp.Key;
                var device = kvp.Value;

                try
                {
                    DeviceManager?.UnregisterDevice(deviceId);
                }
                catch (Exception ex)
                {
                    Logger?.LogWarn($"注销设备 {deviceId} 时发生异常: {ex.Message}", LogCategory.Device);
                }

                device.Dispose();
                OnDeviceUnregistered(deviceId);
            }

            _devices.Clear();
        }

        public void Dispose() => DisposeAll();

        /// <summary>
        /// 选择工厂（增强版：支持厂家和型号匹配）
        /// </summary>
        protected virtual IDeviceFactory SelectFactory(TConfig config)
        {
            // 优先查找精确匹配的工厂（厂家+型号）
            if (config is IDeviceInfo deviceInfo && 
                !string.IsNullOrWhiteSpace(deviceInfo.Manufacturer) && 
                !string.IsNullOrWhiteSpace(deviceInfo.Model))
            {
                var exactMatch = _factories.FirstOrDefault(f => 
                    f.CanCreate(config) && 
                    IsFactoryMatch(f, deviceInfo.Manufacturer, deviceInfo.Model));
                
                if (exactMatch != null)
                {
                    return exactMatch;
                }
            }

            // 回退到类型匹配
            return _factories.FirstOrDefault(f => f.CanCreate(config));
        }

        /// <summary>
        /// 检查工厂是否匹配指定的厂家和型号（子类可重写）
        /// </summary>
        protected virtual bool IsFactoryMatch(
            IDeviceFactory factory, 
            string manufacturer, 
            string model)
        {
            // 默认实现：工厂名称包含厂家和型号信息
            // 子类可以重写此方法以实现更精确的匹配逻辑
            var factoryName = factory.GetType().Name;
            return factoryName.Contains(manufacturer, StringComparison.OrdinalIgnoreCase) &&
                   factoryName.Contains(model, StringComparison.OrdinalIgnoreCase);
        }

        protected virtual void RegisterWithDeviceManager(TDevice device)
        {
            if (DeviceManager == null)
            {
                return;
            }

            var result = DeviceManager.RegisterDevice(device);
            if (!result.Success)
            {
                Logger?.LogWarn($"设备 {device.DeviceName} 注册失败: {result.ErrorMessage}", LogCategory.Device);
            }
        }

        protected virtual void OnDeviceLoaded(TDevice device)
        {
            DeviceLoaded?.Invoke(this, device);
        }

        protected virtual void OnDeviceUnregistered(string deviceId)
        {
            DeviceUnregistered?.Invoke(this, deviceId);
        }

        protected virtual void OnDeviceLoadFailed(TDevice device, string reason)
        {
            Logger?.LogWarn($"设备 {device.DeviceName} 加载失败：{reason}", LogCategory.Device);
        }

        protected virtual Task<bool> InitializeDeviceAsync(TDevice device, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected virtual bool ShouldAutoStart(TDevice device) => false;

        protected virtual Task StartDeviceAsync(TDevice device, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        protected virtual Task StopDeviceAsync(TDevice device)
        {
            return Task.CompletedTask;
        }
    }
}


