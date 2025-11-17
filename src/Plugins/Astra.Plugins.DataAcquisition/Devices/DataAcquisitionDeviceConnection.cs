using System;
using System.Threading;
using System.Threading.Tasks;
using Astra.Core.Devices;
using Astra.Core.Devices.Base;
using Astra.Core.Devices.Configuration;
using Astra.Core.Foundation.Common;
using Astra.Core.Logs;

namespace Astra.Plugins.DataAcquisition.Devices
{
    public class DataAcquisitionDeviceConnection : DeviceConnectionBase
    {
        private readonly object _syncRoot = new();
        private readonly DataAcquisitionConfig _config;
        private readonly ILogger _logger;
        private bool _connected;
        private bool _deviceExists;

        public DataAcquisitionDeviceConnection(DataAcquisitionConfig config, ILogger logger = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger;
            _deviceExists = true;         
            AutoReconnectEnabled = true;
        }

        protected override bool DoCheckAlive()
        {
            lock (_syncRoot)
            {
                return _connected;
            }
        }

        protected override Task<bool> DoCheckAliveAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(DoCheckAlive());
        }

        protected override bool DoCheckDeviceExists()
        {
            lock (_syncRoot)
            {
                return _deviceExists;
            }
        }

        protected override Task<bool> DoCheckDeviceExistsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(DoCheckDeviceExists());
        }

        protected override OperationResult DoConnect()
        {
            lock (_syncRoot)
            {
                if (!_deviceExists)
                {
                    return OperationResult.Fail($"采集卡 {_config.DeviceName} 不存在或未就绪", ErrorCodes.DeviceNotFound);
                }

                if (_connected)
                {
                    return OperationResult.Succeed("采集卡已处于连接状态");
                }

                try
                {
                    // 模拟设备建立连接时的初始化操作
                    Thread.Sleep(500);
                    _connected = true;
                    _logger?.Info($"[{_config.DeviceName}] 连接成功", LogCategory.Device);
                    return OperationResult.Succeed("采集卡连接成功");
                }
                catch (Exception ex)
                {
                    _logger?.Error($"[{_config.DeviceName}] 连接失败: {ex.Message}", ex, LogCategory.Device);
                    return OperationResult.Fail($"连接采集卡失败: {ex.Message}", ex, ErrorCodes.ConnectFailed);
                }
            }
        }

        protected override Task<OperationResult> DoConnectAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(DoConnect());
        }

        protected override OperationResult DoDisconnect()
        {
            lock (_syncRoot)
            {
                if (!_connected)
                {
                    return OperationResult.Succeed("采集卡已经断开");
                }

                _connected = false;
                _logger?.Info($"[{_config.DeviceName}] 已断开连接", LogCategory.Device);
                return OperationResult.Succeed("采集卡断开成功");
            }
        }

        protected override Task<OperationResult> DoDisconnectAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(DoDisconnect());
        }
    }
}
