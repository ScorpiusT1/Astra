using Astra.Core.Configuration;
using Astra.Core.Devices.Abstractions;
using Astra.Core.Devices.Base;
using Astra.Core.Devices.Management;
using Astra.Core.Logs;
using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.Health;
using Astra.Core.Plugins.Messaging;
using Astra.Plugins.DataAcquisition.Abstractions;
using Astra.Plugins.DataAcquisition.Configs;
using Astra.Plugins.DataAcquisition.Devices;
using Astra.Plugins.DataAcquisition.Factories;
using Astra.Plugins.DataAcquisition.Specifications;
using Microsoft.Extensions.DependencyInjection;


namespace Astra.Plugins.DataAcquisition
{
    public class DataAcquisitionPlugin : IPlugin
    {
        private IPluginContext _context;
        private IDeviceManager? _deviceManager;

        private IConfigurationManager? _configuManager;
        private IMessageBus _messageBus;
        private ILogger? _logger;
        private readonly List<IDataAcquisition> _devices = new();
        private readonly List<IDeviceFactory> _factories = new();
        private bool _disposed;

        public string Id => "Astra.Plugins.DataAcquisition";

        public string Name => "数据采集插件";

        public Version Version => new Version(1, 0, 0);

        public async Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            // ⭐ 从 _context.ServiceProvider 获取服务
            // 这样可以确保插件系统和主应用使用同一个服务实例
            try
            {
                _deviceManager = context.ServiceProvider?.GetService<IDeviceManager>();

                _configuManager = context.ServiceProvider?.GetService<IConfigurationManager>();

            }
            catch
            {
                _deviceManager = null;
                _configuManager = null;
            }

            _messageBus = context.MessageBus;

            // 尝试从服务注册表获取 Astra.Core.Logs.ILogger，如果获取不到则使用 null
            try
            {
                _logger = context.Services.Resolve<ILogger>();
            }
            catch
            {
                _logger = null;
            }

            _logger?.Info($"[{Name}] 开始初始化插件", LogCategory.System);

            // 初始化设备规格
            DataAcquisitionSpecificationInitializer.Initialize();

            // 初始化设备工厂
            InitializeFactories();

            // 先加载传感器配置（需要在设备配置加载前完成，以便恢复传感器引用）
            await LoadSensorConfig(cancellationToken).ConfigureAwait(false);
            
            // 从配置文件加载设备配置（配置会先注册到 ConfigurationManager，然后根据配置创建设备）
            // 加载设备配置时会自动恢复传感器引用
            await LoadDeviceConfigurationsAsync(cancellationToken).ConfigureAwait(false);
            
            _logger?.Info($"[{Name}] 插件初始化完成，已加载 {_devices.Count} 个设备", LogCategory.System);
        }

        public async Task OnEnableAsync(CancellationToken cancellationToken = default)
        {
            // ⭐ 从 _context.ServiceProvider 获取 IDeviceManager（确保使用主应用的实例）
            if (_deviceManager == null && _context.ServiceProvider != null)
            {
                try
                {
                    _deviceManager = _context.ServiceProvider.GetService<IDeviceManager>();
                }
                catch
                {
                    _deviceManager = null;
                }
            }

            if (_deviceManager == null)
            {
                _logger?.Error($"[{Name}] DeviceManager 为 null，无法注册设备", null, LogCategory.System);
                return;
            }

            _logger?.Info($"[{Name}] 启用插件，共有 {_devices.Count} 个设备需要注册", LogCategory.System);

            // 初始化并注册所有设备
            int successCount = 0;
            int failCount = 0;

            foreach (var device in _devices)
            {
                try
                {
                    // IDataAcquisition 设备都继承自 DeviceBase，因此也是 IDevice
                    var deviceAsIDevice = device as Astra.Core.Devices.Interfaces.IDevice;
                    if (deviceAsIDevice == null)
                    {
                        _logger?.Error($"[{Name}] 设备 {device.DeviceId} 不是有效的 IDevice 类型", null, LogCategory.Device);
                        failCount++;
                        continue;
                    }

                    var initResult = await device.InitializeAsync().ConfigureAwait(false);

                    if (initResult)
                    {
                        var registerResult = _deviceManager.RegisterDevice(deviceAsIDevice);

                        if (registerResult.Success)
                        {
                            successCount++;
                            _logger?.Info($"[{Name}] 设备 {deviceAsIDevice.DeviceName} 注册成功", LogCategory.Device);

                            // 如果配置了自动启动，则开始采集
                            if (device is DeviceBase<DataAcquisitionConfig> deviceBase)
                            {
                                var config = deviceBase.CurrentConfig;
                                if (config != null && config.AutoStart)
                                {
                                    await device.StartAcquisitionAsync(cancellationToken).ConfigureAwait(false);
                                }
                            }
                        }
                        else
                        {
                            failCount++;
                            _logger?.Error($"[{Name}] 设备 {deviceAsIDevice.DeviceName} 注册失败: {registerResult.ErrorMessage}", null, LogCategory.Device);
                        }
                    }
                    else
                    {
                        failCount++;
                        _logger?.Error($"[{Name}] 设备 {deviceAsIDevice.DeviceName} 初始化失败", null, LogCategory.Device);
                    }
                }
                catch (Exception ex)
                {
                    failCount++;
                    _logger?.Error($"[{Name}] 启用设备 {device.DeviceId} 时发生异常: {ex.Message}", ex, LogCategory.Device);
                }
            }

            _logger?.Info($"[{Name}] 插件已启用，成功注册 {successCount} 个设备，失败 {failCount} 个", LogCategory.System);
        }

        public async Task OnDisableAsync(CancellationToken cancellationToken = default)
        {
            _logger?.Info($"[{Name}] 禁用插件", LogCategory.System);

            // 停止所有设备的采集
            var stopTasks = _devices.Select(device => Task.Run(async () =>
            {
                try
                {
                    await device.StopAcquisitionAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    var deviceAsIDevice = device as Astra.Core.Devices.Interfaces.IDevice;
                    var deviceName = deviceAsIDevice?.DeviceName ?? device.DeviceId;
                    _logger?.Error($"[{Name}] 停止设备 {deviceName} 时发生异常: {ex.Message}", ex, LogCategory.Device);
                }
            }));

            await Task.WhenAll(stopTasks).ConfigureAwait(false);

            // 注销所有设备
            foreach (var device in _devices)
            {
                try
                {
                    var deviceAsIDevice = device as Astra.Core.Devices.Interfaces.IDevice;
                    if (deviceAsIDevice != null)
                    {
                        var unregisterResult = _deviceManager.UnregisterDevice(device.DeviceId);
                        if (unregisterResult.Success)
                        {
                            _logger?.Info($"[{Name}] 设备 {deviceAsIDevice.DeviceName} 已注销", LogCategory.Device);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Error($"[{Name}] 注销设备 {device.DeviceId} 时发生异常: {ex.Message}", ex, LogCategory.Device);
                }
            }

            _logger?.Info($"[{Name}] 插件已禁用", LogCategory.System);
        }

        public async Task<HealthCheckResult> CheckHealthAsync()
        {
            var startTime = DateTime.UtcNow;
            var healthyCount = 0;
            var totalCount = _devices.Count;

            try
            {
                foreach (var device in _devices)
                {
                    var state = device.GetState();
                    if (state == AcquisitionState.Running || state == AcquisitionState.Idle)
                    {
                        healthyCount++;
                    }
                }

                var duration = DateTime.UtcNow - startTime;
                var healthStatus = totalCount == 0
                    ? HealthStatus.Degraded
                    : (healthyCount == totalCount ? HealthStatus.Healthy : HealthStatus.Degraded);

                return new HealthCheckResult
                {
                    Name = Name,
                    Status = healthStatus,
                    Message = $"设备健康状态: {healthyCount}/{totalCount} 正常",
                    Duration = duration,
                    Data = new Dictionary<string, object>
                    {
                        ["TotalDevices"] = totalCount,
                        ["HealthyDevices"] = healthyCount,
                        ["UnhealthyDevices"] = totalCount - healthyCount
                    }
                };
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                return HealthCheckResult.Unhealthy(Name, $"健康检查失败: {ex.Message}", ex, duration);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            DisposeAsync().AsTask().GetAwaiter().GetResult();
            _disposed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            await OnDisableAsync().ConfigureAwait(false);

            // 释放所有设备
            foreach (var device in _devices)
            {
                try
                {
                    await device.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    var deviceAsIDevice = device as Astra.Core.Devices.Interfaces.IDevice;
                    var deviceName = deviceAsIDevice?.DeviceName ?? device.DeviceId;
                    _logger?.Error($"[{Name}] 释放设备 {deviceName} 时发生异常: {ex.Message}", ex, LogCategory.Device);
                }
            }

            _devices.Clear();
            _factories.Clear();
            _disposed = true;
        }

        /// <summary>
        /// 初始化设备工厂
        /// </summary>
        private void InitializeFactories()
        {
            _factories.Add(new BRCDataAcquisitionFactory());
            _factories.Add(new MGSDataAcquisitionFactory());
            
            _logger?.Info($"[{Name}] 已注册 {_factories.Count} 个设备工厂", LogCategory.System);
        }

        /// <summary>
        /// 选择工厂（根据配置的厂家选择对应的工厂）
        /// </summary>
        private IDeviceFactory? SelectFactory(DataAcquisitionConfig config)
        {
            // 优先查找精确匹配的工厂（厂家匹配）
            if (!string.IsNullOrWhiteSpace(config.Manufacturer))
            {
                var exactMatch = _factories.FirstOrDefault(f => f.CanCreate(config));
                if (exactMatch != null)
                {
                    return exactMatch;
                }
            }

            // 如果没有匹配的工厂，返回 null（让调用者处理）
            return null;
        }

        private async Task LoadSensorConfig(CancellationToken cancellationToken)
        {
            try
            {
                // ✅ Provider 已通过 ConfigProviderDiscovery 自动发现并注册
                // 无需手动注册，直接获取配置即可
                
                if (_configuManager == null)
                {
                    _logger?.Warn($"[{Name}] ConfigurationManager 未初始化，跳过传感器配置加载", LogCategory.System);
                    return;
                }

                // 直接获取所有传感器配置（Provider 已在启动时自动注册）
                var result = await _configuManager.GetAllConfigsAsync<SensorConfig>();
                
                if (result?.Success == true && result.Data != null)
                {
                    _logger?.Info($"[{Name}] 已加载 {result.Data.Count()} 个传感器配置", LogCategory.System);
                }
                else
                {
                    _logger?.Warn($"[{Name}] 未找到传感器配置或加载失败", LogCategory.System);
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"[{Name}] 加载传感器配置时发生异常: {ex.Message}", ex, LogCategory.System);
            }
        }

        private async Task LoadDeviceConfigurationsAsync(CancellationToken cancellationToken)
        {
            try
            {
                _devices?.Clear();

                // ✅ Provider 已通过 ConfigProviderDiscovery 自动发现并注册
                // 无需手动注册，直接获取配置即可

                if (_configuManager == null)
                {
                    _logger?.Error($"[{Name}] ConfigurationManager 未初始化，无法加载设备配置", null, LogCategory.System);
                    return;
                }

                // 直接获取所有设备配置（Provider 已在启动时自动注册）
                var result = await _configuManager.GetAllConfigsAsync<DataAcquisitionConfig>();

                if (result == null || result.Data == null || result.Data.Count() == 0)
                {
                    _logger?.Warn($"[{Name}] 配置文件中没有设备配置", LogCategory.System);
                    return;
                }

                // 获取所有传感器配置，用于恢复传感器引用
                var sensorResult = await _configuManager.GetAllConfigsAsync<SensorConfig>();
                var availableSensors = sensorResult?.Data?.ToList() ?? new List<SensorConfig>();

                foreach (var config in result.Data)
                {
                    try
                    {
                        // 恢复配置中所有通道的传感器引用
                        config.RestoreSensorReferences(availableSensors);

                        // 使用工厂模式创建设备（根据厂家选择对应的工厂）
                        var factory = SelectFactory(config);
                        if (factory == null)
                        {
                            _logger?.Warn($"[{Name}] 未找到可用于配置 {config.DeviceName} ({config.DeviceId}) 的设备工厂，厂家: {config.Manufacturer}", LogCategory.Device);
                            continue;
                        }

                        var device = factory.Create(config, _context?.ServiceProvider) as IDataAcquisition;
                        if (device == null)
                        {
                            _logger?.Warn($"[{Name}] 工厂 {factory.GetType().Name} 创建的设备类型不匹配", LogCategory.Device);
                            continue;
                        }

                        _devices?.Add(device);
                        _logger?.Info($"[{Name}] 已加载设备配置: {config.DeviceName} (ID: {config.DeviceId}), 厂家: {config.Manufacturer}, 型号: {config.Model}", LogCategory.Device);
                    }
                    catch (Exception ex)
                    {
                        _logger?.Error($"[{Name}] 加载设备配置失败: {ex.Message}", ex, LogCategory.Device);
                    }
                }

                //// 查找配置文件
                //var configFileName = "Astra.Plugins.DataAcquisition.config.json";

                //string fileName = Path.Combine(Core.Configuration.ConfigPathString.DeviceConfigDirectory, configFileName);

                //var configData = await Core.Configuration.ConfigPathString.LoadAsync<ConfigCollection<DataAcquisitionConfig>>(fileName, cancellationToken).ConfigureAwait(false);

                //if (configData?.Configs == null || configData.Configs.Count == 0)
                //{
                //    _logger?.Warn($"[{ConfigName}] 配置文件中没有设备配置", LogCategory.System);
                //    return;
                //}

                //// 先注册配置到 ConfigurationManager，然后根据配置创建设备实例
                //// 注意：配置独立于设备，先有配置才能创建设备
                //foreach (var config in configData.Configs)
                //{
                //    try
                //    {
                //        // 1. 先将配置注册到 ConfigurationManager（配置独立于设备）
                //        if (_configurationManager != null)
                //        {
                //            // 传递标准配置文件路径（无论从哪个位置加载，都使用标准路径注册）
                //            // 这样确保同一类型配置使用相同的路径，后续保存时统一保存到标准位置
                //            var registerResult = _configurationManager.RegisterConfig(config, fileName);


                //            if (registerResult.Success)
                //            {
                //                _logger?.Info($"[{ConfigName}] 配置已注册到 ConfigurationManager: {config.DeviceName} (ID: {config.ConfigId}), 路径: {fileName}", LogCategory.System);
                //            }
                //            else
                //            {
                //                _logger?.Warn($"[{ConfigName}] 配置注册到 ConfigurationManager 失败: {config.DeviceName} - {registerResult.ErrorMessage}", LogCategory.System);
                //                // 即使注册失败，仍然尝试创建设备（向后兼容）
                //            }
                //        }

                //        // 2. 根据配置创建设备实例
                //        var device = new DataAcquisitionDevice(config, _messageBus, _logger);
                //        _devices?.Add(device);

                //        _logger?.Info($"[{ConfigName}] 已加载设备配置: {config.DeviceName} (ID: {config.DeviceId})", LogCategory.Device);
                //    }
                //    catch (Exception ex)
                //    {
                //        _logger?.Error($"[{ConfigName}] 加载设备配置失败: {ex.Message}", ex, LogCategory.Device);
                //    }
                //}
            }
            catch (Exception ex)
            {
                _logger?.Error($"[{Name}] 加载设备配置时发生异常: {ex.Message}", ex, LogCategory.System);
            }
        }
    }
}
