using Astra.Core.Configuration;
using Astra.Core.Devices.Management;
using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.Health;
using Astra.Core.Plugins.Messaging;
using Astra.Core.Logs;
using Astra.Plugins.DataAcquisition.Devices;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Astra.Plugins.DataAcquisition.Abstractions;

namespace Astra.Plugins.DataAcquisition
{
    public class DataAcquisitionPlugin : IPlugin
    {
        private IPluginContext _context;
        private IDeviceManager? _deviceManager;
        private ConfigurationManager? _configurationManager;
        private IMessageBus _messageBus;
        private ILogger? _logger;
        private readonly List<DataAcquisitionDevice> _devices = new();
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
                _configurationManager = context.ServiceProvider?.GetService<ConfigurationManager>();
            }
            catch
            {
                _deviceManager = null;
                _configurationManager = null;
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

            // 从配置文件加载设备配置（配置会先注册到 ConfigurationManager，然后根据配置创建设备）
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
                    var initResult = await device.InitializeAsync().ConfigureAwait(false);

                    if (initResult)
                    {
                        var registerResult = _deviceManager.RegisterDevice(device);

                        if (registerResult.Success)
                        {
                            successCount++;
                            _logger?.Info($"[{Name}] 设备 {device.DeviceName} 注册成功", LogCategory.Device);
                            
                            // 如果配置了自动启动，则开始采集
                            if (device.CurrentConfig is DataAcquisitionConfig config && config.AutoStart)
                            {
                                await device.StartAcquisitionAsync(cancellationToken).ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            failCount++;
                            _logger?.Error($"[{Name}] 设备 {device.DeviceName} 注册失败: {registerResult.ErrorMessage}", null, LogCategory.Device);
                        }
                    }
                    else
                    {
                        failCount++;
                        _logger?.Error($"[{Name}] 设备 {device.DeviceName} 初始化失败", null, LogCategory.Device);
                    }
                }
                catch (Exception ex)
                {
                    failCount++;
                    _logger?.Error($"[{Name}] 启用设备 {device.DeviceName} 时发生异常: {ex.Message}", ex, LogCategory.Device);
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
                    _logger?.Error($"[{Name}] 停止设备 {device.DeviceName} 时发生异常: {ex.Message}", ex, LogCategory.Device);
                }
            }));

            await Task.WhenAll(stopTasks).ConfigureAwait(false);

            // 注销所有设备
            foreach (var device in _devices)
            {
                try
                {
                    var unregisterResult = _deviceManager.UnregisterDevice(device.DeviceId);
                    if (unregisterResult.Success)
                    {
                        _logger?.Info($"[{Name}] 设备 {device.DeviceName} 已注销", LogCategory.Device);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Error($"[{Name}] 注销设备 {device.DeviceName} 时发生异常: {ex.Message}", ex, LogCategory.Device);
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
                    _logger?.Error($"[{Name}] 释放设备 {device.DeviceName} 时发生异常: {ex.Message}", ex, LogCategory.Device);
                }
            }

            _devices.Clear();
            _disposed = true;
        }

        private async Task LoadDeviceConfigurationsAsync(CancellationToken cancellationToken)
        {
            try
            {
                _devices?.Clear();   
                // 查找配置文件
                var configFileName = "Astra.Plugins.DataAcquisition.config.json";
                
                // 标准配置文件路径：Configs/Devices/{插件名}.config.json
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var configsDevicesDir = Path.Combine(baseDir, "Configs", "Devices");
                var standardConfigPath = Path.Combine(configsDevicesDir, configFileName);
                
                // 实际加载的配置文件路径（可能从旧位置加载，但注册时使用标准路径）
                var actualConfigPath = standardConfigPath;

                // 优先从标准路径加载
                if (!File.Exists(actualConfigPath))
                {
                    // 如果标准路径不存在，尝试从插件目录加载（向后兼容）
                    var pluginConfigPath = Path.Combine(_context.PluginDirectory, configFileName);
                    if (File.Exists(pluginConfigPath))
                    {
                        actualConfigPath = pluginConfigPath;
                    }
                    else
                    {
                        // 如果插件目录中也没有，尝试从 Configs/DeviceConfigs 目录加载（向后兼容）
                        var binPath = Path.GetDirectoryName(_context.PluginDirectory);
                        if (binPath != null)
                        {
                            var alternativePath = Path.Combine(binPath, "Configs", "DeviceConfigs", configFileName);
                            if (File.Exists(alternativePath))
                            {
                                actualConfigPath = alternativePath;
                            }
                        }
                    }
                }

                if (!File.Exists(actualConfigPath))
                {
                    _logger?.Warn($"[{Name}] 未找到配置文件 {configFileName}，将使用默认配置", LogCategory.System);
                    // 文件不存在时，直接返回（没有配置需要加载和注册）
                    return;
                }

                var json = await File.ReadAllTextAsync(actualConfigPath, cancellationToken).ConfigureAwait(false);
                
                var configData = JsonSerializer.Deserialize<DeviceConfigData>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (configData?.Configs == null || configData.Configs.Count == 0)
                {
                    _logger?.Warn($"[{Name}] 配置文件中没有设备配置", LogCategory.System);
                    return;
                }

                // 先注册配置到 ConfigurationManager，然后根据配置创建设备实例
                // 注意：配置独立于设备，先有配置才能创建设备
                foreach (var config in configData.Configs)
                {
                    try
                    {
                        // 1. 先将配置注册到 ConfigurationManager（配置独立于设备）
                        if (_configurationManager != null)
                        {
                            // 传递标准配置文件路径（无论从哪个位置加载，都使用标准路径注册）
                            // 这样确保同一类型配置使用相同的路径，后续保存时统一保存到标准位置
                            var registerResult = _configurationManager.RegisterConfig(config, standardConfigPath);
                            if (registerResult.Success)
                            {
                                _logger?.Info($"[{Name}] 配置已注册到 ConfigurationManager: {config.DeviceName} (ID: {config.ConfigId}), 路径: {standardConfigPath}", LogCategory.System);
                            }
                            else
                            {
                                _logger?.Warn($"[{Name}] 配置注册到 ConfigurationManager 失败: {config.DeviceName} - {registerResult.ErrorMessage}", LogCategory.System);
                                // 即使注册失败，仍然尝试创建设备（向后兼容）
                            }
                        }

                        // 2. 根据配置创建设备实例
                        var device = new DataAcquisitionDevice(config, _messageBus, _logger);
                        _devices?.Add(device);

                        _logger?.Info($"[{Name}] 已加载设备配置: {config.DeviceName} (ID: {config.DeviceId})", LogCategory.Device);
                    }
                    catch (Exception ex)
                    {
                        _logger?.Error($"[{Name}] 加载设备配置失败: {ex.Message}", ex, LogCategory.Device);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"[{Name}] 加载设备配置时发生异常: {ex.Message}", ex, LogCategory.System);
            }
        }

        private class DeviceConfigData
        {
            public List<DataAcquisitionConfig> Configs { get; set; } = new();
        }
    }
}
