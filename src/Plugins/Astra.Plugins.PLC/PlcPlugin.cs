using Astra.Core.Configuration.Abstractions;
using Astra.Core.Configuration.Providers;
using Astra.Core.Devices.Abstractions;
using Astra.Core.Devices.Interfaces;
using Astra.Core.Devices.Management;
using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.Health;
using Astra.Plugins.PLC.Configs;
using Astra.Plugins.PLC.Devices;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Plugins.PLC
{
    public class PlcPlugin : IPlugin
    {
        private IPluginContext? _context;
        private IDeviceManager? _deviceManager;
        private IConfigurationManager? _configurationManager;
        private readonly List<IDevice> _devices = new();
        private bool _disposed;

        public string Id => "Astra.Plugins.PLC";

        public string Name => "PLC 设备插件";

        public Version Version => new(1, 0, 0);

        public async Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
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
            }
        }

        public Task OnEnableAsync(CancellationToken cancellationToken = default)
        {
            if (_deviceManager == null)
            {
                return Task.CompletedTask;
            }

            foreach (var device in _devices)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _deviceManager.RegisterDevice(device);
            }

            return Task.CompletedTask;
        }

        public Task OnDisableAsync(CancellationToken cancellationToken = default)
        {
            if (_deviceManager == null)
            {
                return Task.CompletedTask;
            }

            foreach (var device in _devices)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _deviceManager.UnregisterDevice(device.DeviceId);
            }

            return Task.CompletedTask;
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

            foreach (var device in _devices.OfType<IDisposable>())
            {
                device.Dispose();
            }

            _devices.Clear();
            _disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
