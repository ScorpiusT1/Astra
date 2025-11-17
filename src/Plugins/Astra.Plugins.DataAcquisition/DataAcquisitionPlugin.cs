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
        private IDeviceManager _deviceManager;
        private IMessageBus _messageBus;
        private ILogger _logger;
        private readonly List<DataAcquisitionDevice> _devices = new();
        private bool _disposed;

        public string Id => "Astra.Plugins.DataAcquisition";

        public string Name => "数据采集插件";

        public Version Version => new Version(1, 0, 0);

        public async Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        {
            System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] ========== InitializeAsync 开始 ==========");
            _context = context ?? throw new ArgumentNullException(nameof(context));
            
            // ⭐ 优先从主应用的 ServiceProvider 获取 IDeviceManager
            // 这样可以确保插件系统和主应用使用同一个 IDeviceManager 实例
            IDeviceManager pluginContextDeviceManager = null;
            try
            {
                pluginContextDeviceManager = context.Services?.Resolve<IDeviceManager>();
                if (pluginContextDeviceManager != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 从 PluginContext 获取到 IDeviceManager，实例哈希码: {pluginContextDeviceManager.GetHashCode()}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 从 PluginContext 获取 IDeviceManager 异常: {ex.Message}");
            }
            
            // ⭐ 尝试从主应用的 ServiceProvider 获取（优先使用主应用的实例）
            try
            {
                var appServiceProvider = GetMainApplicationServiceProvider();
                
                if (appServiceProvider != null)
                {
                    var mainAppDeviceManager = appServiceProvider.GetService<IDeviceManager>();
                    if (mainAppDeviceManager != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 从主应用 ServiceProvider 获取到 IDeviceManager，实例哈希码: {mainAppDeviceManager.GetHashCode()}");
                        
                        // 比较两个实例是否是同一个
                        if (pluginContextDeviceManager != null && pluginContextDeviceManager.GetHashCode() != mainAppDeviceManager.GetHashCode())
                        {
                            System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] ⚠️ 警告：PluginContext 和主应用的 DeviceManager 不是同一个实例！");
                            System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin]   使用主应用的 DeviceManager 实例（确保设备注册到正确的位置）");
                        }
                        
                        // 优先使用主应用的 DeviceManager 实例
                        _deviceManager = mainAppDeviceManager;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 主应用 ServiceProvider 中没有 IDeviceManager");
                        // 如果主应用没有，则使用 PluginContext 的（如果有）
                        _deviceManager = pluginContextDeviceManager;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 无法获取主应用 ServiceProvider，使用 PluginContext 的 DeviceManager");
                    // 如果无法获取主应用的 ServiceProvider，则使用 PluginContext 的（如果有）
                    _deviceManager = pluginContextDeviceManager;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 从主应用 ServiceProvider 获取 IDeviceManager 失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"  堆栈: {ex.StackTrace}");
                // 如果获取失败，则使用 PluginContext 的（如果有）
                _deviceManager = pluginContextDeviceManager;
            }
            
            if (_deviceManager == null)
            {
                System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] ⚠️ 警告：无法获取 IDeviceManager，设备注册功能将不可用");
            }
            
            _messageBus = context.MessageBus;
            
            // 尝试从服务注册表获取 Astra.Core.Logs.ILogger，如果获取不到则使用 null
            try
            {
                _logger = context.Services?.Resolve<ILogger>();
            }
            catch
            {
                _logger = null;
            }

            _logger?.Info($"[{Name}] 开始初始化插件", LogCategory.System);
            System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 开始初始化插件: {Name}");

            // 从配置文件加载设备配置
            System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 开始加载设备配置...");
            await LoadDeviceConfigurationsAsync(cancellationToken).ConfigureAwait(false);
            System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 设备配置加载完成，共创建 {_devices.Count} 个设备实例");

            _logger?.Info($"[{Name}] 插件初始化完成，已加载 {_devices.Count} 个设备", LogCategory.System);
            System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] ========== InitializeAsync 完成，设备数量: {_devices.Count} ==========");
        }

        public async Task OnEnableAsync(CancellationToken cancellationToken = default)
        {
            System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] ========== OnEnableAsync 开始 ==========");
            System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] DeviceManager 状态: {_deviceManager != null}, 设备数量: {_devices.Count}");
            
            // ⭐ 在 OnEnableAsync 时再次尝试获取主应用的 DeviceManager（此时 App.ServiceProvider 可能已经设置）
            // 这样可以确保使用主应用的 DeviceManager 实例
            try
            {
                var appServiceProvider = GetMainApplicationServiceProvider();
                if (appServiceProvider != null)
                {
                    var mainAppDeviceManager = appServiceProvider.GetService<IDeviceManager>();
                    if (mainAppDeviceManager != null)
                    {
                        var mainAppHashCode = mainAppDeviceManager.GetHashCode();
                        var currentHashCode = _deviceManager?.GetHashCode() ?? 0;
                        
                        System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 主应用 DeviceManager 实例哈希码: {mainAppHashCode}");
                        System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 当前 DeviceManager 实例哈希码: {currentHashCode}");
                        
                        // 如果不是同一个实例，切换到主应用的实例
                        if (_deviceManager == null || mainAppHashCode != currentHashCode)
                        {
                            System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] ⚠️ DeviceManager 实例不一致，切换到主应用的实例");
                            _deviceManager = mainAppDeviceManager;
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] ✅ DeviceManager 实例一致，使用当前实例");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] OnEnableAsync 中获取主应用 DeviceManager 失败: {ex.Message}");
            }
            
            if (_deviceManager == null)
            {
                System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] ⚠️ 错误：DeviceManager 为 null，无法注册设备！");
                return;
            }
            
            _logger?.Info($"[{Name}] 启用插件，共有 {_devices.Count} 个设备需要注册", LogCategory.System);
            System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 启用插件，共有 {_devices.Count} 个设备需要注册");
            System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 最终使用的 DeviceManager 实例哈希码: {_deviceManager.GetHashCode()}");

            // 初始化并注册所有设备
            int successCount = 0;
            int failCount = 0;
            
            foreach (var device in _devices)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 正在初始化设备: {device.DeviceName}");
                    var initResult = await device.InitializeAsync().ConfigureAwait(false);
                    if (initResult)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 设备 {device.DeviceName} 初始化成功，开始注册...");
                        System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] DeviceManager 实例哈希码: {_deviceManager?.GetHashCode()}");
                        var registerResult = _deviceManager.RegisterDevice(device);
                        if (registerResult.Success)
                        {
                            successCount++;
                            _logger?.Info($"[{Name}] 设备 {device.DeviceName} 注册成功", LogCategory.Device);
                            System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 设备 {device.DeviceName} (ID: {device.DeviceId}) 注册成功");
                            
                            // 验证设备是否真的注册成功
                            var deviceCount = _deviceManager.GetDeviceCount();
                            System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 注册后设备总数: {deviceCount}");
                            
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
                            System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 设备 {device.DeviceName} 注册失败: {registerResult.ErrorMessage}");
                        }
                    }
                    else
                    {
                        failCount++;
                        _logger?.Error($"[{Name}] 设备 {device.DeviceName} 初始化失败", null, LogCategory.Device);
                        System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 设备 {device.DeviceName} 初始化失败");
                    }
                }
                catch (Exception ex)
                {
                    failCount++;
                    _logger?.Error($"[{Name}] 启用设备 {device.DeviceName} 时发生异常: {ex.Message}", ex, LogCategory.Device);
                    System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 启用设备 {device.DeviceName} 时发生异常: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"  堆栈: {ex.StackTrace}");
                }
            }

            _logger?.Info($"[{Name}] 插件已启用，成功注册 {successCount} 个设备，失败 {failCount} 个", LogCategory.System);
            System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 插件已启用，成功注册 {successCount} 个设备，失败 {failCount} 个");
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
            System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] ========== LoadDeviceConfigurationsAsync 开始 ==========");
            System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] PluginDirectory: {_context?.PluginDirectory}");
            
            try
            {
                // 查找配置文件
                var configFileName = "Astra.Plugins.DataAcquisition.config.json";
                var configPath = Path.Combine(_context.PluginDirectory, configFileName);
                System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 尝试从插件目录加载配置: {configPath}");

                // 如果插件目录中没有，尝试从 Configs/Devices 目录加载
                if (!File.Exists(configPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 插件目录中未找到配置文件，尝试从 Configs/Devices 目录加载");
                    var binPath = Path.GetDirectoryName(_context.PluginDirectory);
                    if (binPath != null)
                    {
                        var alternativePath = Path.Combine(binPath, "Configs", "Devices", configFileName);
                        System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 尝试备用路径: {alternativePath}");
                        if (File.Exists(alternativePath))
                        {
                            configPath = alternativePath;
                            System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 在备用路径找到配置文件");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 备用路径也不存在");
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 在插件目录找到配置文件");
                }

                if (!File.Exists(configPath))
                {
                    _logger?.Warn($"[{Name}] 未找到配置文件 {configFileName}，将使用默认配置", LogCategory.System);
                    System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] ⚠️ 未找到配置文件: {configFileName}");
                    System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 插件目录: {_context.PluginDirectory}");
                    System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] ========== LoadDeviceConfigurationsAsync 结束（无配置文件） ==========");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 加载配置文件: {configPath}");
                var json = await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);
                System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 配置文件内容长度: {json?.Length ?? 0} 字符");
                
                var configData = JsonSerializer.Deserialize<DeviceConfigData>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (configData?.Devices == null || configData.Devices.Count == 0)
                {
                    _logger?.Warn($"[{Name}] 配置文件中没有设备配置", LogCategory.System);
                    System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] ⚠️ 配置文件中没有设备配置");
                    System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] configData 是否为 null: {configData == null}");
                    if (configData != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] Devices 是否为 null: {configData.Devices == null}");
                        if (configData.Devices != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] Devices 数量: {configData.Devices.Count}");
                        }
                    }
                    System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] ========== LoadDeviceConfigurationsAsync 结束（无设备配置） ==========");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 配置文件中找到 {configData.Devices.Count} 个设备配置");

                // 创建设备实例
                foreach (var deviceConfig in configData.Devices)
                {
                    try
                    {
                        var config = new DataAcquisitionConfig
                        {
                            DeviceName = deviceConfig.DeviceName ?? $"采集卡设备_{_devices.Count + 1}",
                            SerialNumber = deviceConfig.SN ?? string.Empty,
                            SampleRate = deviceConfig.SampleRate > 0 ? deviceConfig.SampleRate : 51200,
                            ChannelCount = deviceConfig.ChannelCount > 0 ? deviceConfig.ChannelCount : 8,
                            BufferSize = deviceConfig.BufferSize > 0 ? deviceConfig.BufferSize : 8192,
                            AutoStart = deviceConfig.AutoStart,
                            IsEnabled = true
                        };

                        var device = new DataAcquisitionDevice(config, _messageBus, _logger);
                        _devices.Add(device);

                        _logger?.Info($"[{Name}] 已加载设备配置: {config.DeviceName}", LogCategory.Device);
                        System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 已加载设备配置: {config.DeviceName} (SN: {config.SerialNumber})");
                    }
                    catch (Exception ex)
                    {
                        _logger?.Error($"[{Name}] 加载设备配置失败: {ex.Message}", ex, LogCategory.Device);
                        System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 加载设备配置失败: {ex.Message}");
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 设备配置加载完成，共创建 {_devices.Count} 个设备实例");
                System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] ========== LoadDeviceConfigurationsAsync 完成 ==========");
            }
            catch (Exception ex)
            {
                _logger?.Error($"[{Name}] 加载设备配置时发生异常: {ex.Message}", ex, LogCategory.System);
                System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] ❌ 加载设备配置时发生异常: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"  异常类型: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"  堆栈: {ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] ========== LoadDeviceConfigurationsAsync 异常结束 ==========");
            }
        }

        private class DeviceConfigData
        {
            public List<DeviceConfigItem> Devices { get; set; } = new();
        }

        private class DeviceConfigItem
        {
            public string DeviceName { get; set; }
            public string SN { get; set; }
            public int SampleRate { get; set; }
            public int ChannelCount { get; set; }
            public int BufferSize { get; set; }
            public bool AutoStart { get; set; } = true;
        }

        /// <summary>
        /// 获取主应用的 ServiceProvider（通过反射访问 App.ServiceProvider 静态属性）
        /// </summary>
        private System.IServiceProvider GetMainApplicationServiceProvider()
        {
            try
            {
                // 方法1：尝试从当前应用程序域中查找 Astra.App 类型
                System.Type appType = null;
                
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        if (assembly.GetName().Name == "Astra")
                        {
                            appType = assembly.GetType("Astra.App");
                            if (appType != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 找到 Astra.App 类型: {appType.FullName}");
                                break;
                            }
                        }
                    }
                    catch
                    {
                        // 忽略无法加载的程序集
                    }
                }

                if (appType == null)
                {
                    // 方法2：尝试通过 Type.GetType 查找
                    appType = System.Type.GetType("Astra.App, Astra");
                }

                if (appType != null)
                {
                    var serviceProviderProperty = appType.GetProperty("ServiceProvider",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    
                    if (serviceProviderProperty != null)
                    {
                        var serviceProvider = serviceProviderProperty.GetValue(null) as System.IServiceProvider;
                        if (serviceProvider != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 成功获取主应用 ServiceProvider");
                            return serviceProvider;
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] App.ServiceProvider 为 null（可能还在初始化中）");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 未找到 ServiceProvider 属性");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 未找到 Astra.App 类型");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DataAcquisitionPlugin] 获取主应用 ServiceProvider 时出错: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"  异常类型: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"  堆栈: {ex.StackTrace}");
            }

            return null;
        }
    }
}
