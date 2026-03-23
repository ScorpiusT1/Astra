using Astra.Core.Devices;
using Astra.Core.Devices.Base;
using Astra.Core.Foundation.Common;
using Astra.Plugins.PLC.Configs;
using S7.Net;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Plugins.PLC.Devices
{
    public class S7SiemensPlcDeviceConnection : DeviceConnectionBase
    {
        private readonly object _plcLock = new();
        private readonly S7SiemensPlcDeviceConfig _config;
        private Plc? _plc;

        public S7SiemensPlcDeviceConnection(S7SiemensPlcDeviceConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            AutoReconnectEnabled = _config.AutoReconnect;
            InitialReconnectDelay = Math.Max(500, _config.ReconnectIntervalMs);
        }

        public OperationResult<object> Read(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return OperationResult<object>.Failure("PLC 地址不能为空", ErrorCodes.InvalidData);
            }

            lock (_plcLock)
            {
                if (_plc == null || !_plc.IsConnected)
                {
                    return OperationResult<object>.Failure("PLC 未连接", ErrorCodes.DeviceNotOnline);
                }

                try
                {
                    var value = _plc.Read(address);
                    return value is null
                        ? OperationResult<object>.Failure($"地址 {address} 返回空值", ErrorCodes.ReceiveFailed)
                        : OperationResult<object>.Succeed(value);
                }
                catch (Exception ex)
                {
                    return OperationResult<object>.Fail($"读取地址 {address} 失败: {ex.Message}", ex, ErrorCodes.ReceiveFailed);
                }
            }
        }

        public OperationResult Write(string address, object value)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return OperationResult.Failure("PLC 地址不能为空", ErrorCodes.InvalidData);
            }

            lock (_plcLock)
            {
                if (_plc == null || !_plc.IsConnected)
                {
                    return OperationResult.Failure("PLC 未连接", ErrorCodes.DeviceNotOnline);
                }

                try
                {
                    _plc.Write(address, value);
                    return OperationResult.Succeed();
                }
                catch (Exception ex)
                {
                    return OperationResult.Fail($"写入地址 {address} 失败: {ex.Message}", ex, ErrorCodes.SendFailed);
                }
            }
        }

        protected override OperationResult DoConnect()
        {
            lock (_plcLock)
            {
                try
                {
                    if (_plc != null && _plc.IsConnected)
                    {
                        return OperationResult.Succeed("PLC 已连接");
                    }

                    _plc?.Close();

                    _plc = new Plc(
                        ResolveCpuType(),
                        _config.Ip,
                        (short)_config.Rack,
                        (short)_config.Slot);

                    _plc.Open();
                    return _plc.IsConnected
                        ? OperationResult.Succeed("S7 PLC 连接成功")
                        : OperationResult.Failure("S7 PLC 连接失败", ErrorCodes.ConnectFailed);
                }
                catch (Exception ex)
                {
                    return OperationResult.Fail($"连接 S7 PLC 失败: {ex.Message}", ex, ErrorCodes.ConnectFailed);
                }
            }
        }

        protected override Task<OperationResult> DoConnectAsync(CancellationToken cancellationToken)
        {
            return Task.Run(DoConnect, cancellationToken);
        }

        protected override OperationResult DoDisconnect()
        {
            lock (_plcLock)
            {
                try
                {
                    if (_plc == null)
                    {
                        return OperationResult.Succeed("PLC 已断开");
                    }

                    _plc.Close();
                    _plc = null;
                    return OperationResult.Succeed("S7 PLC 断开成功");
                }
                catch (Exception ex)
                {
                    return OperationResult.Fail($"断开 S7 PLC 失败: {ex.Message}", ex, ErrorCodes.DisconnectFailed);
                }
            }
        }

        protected override Task<OperationResult> DoDisconnectAsync(CancellationToken cancellationToken)
        {
            return Task.Run(DoDisconnect, cancellationToken);
        }

        protected override bool DoCheckDeviceExists()
        {
            using var tcp = new TcpClient();
            var connectTask = tcp.ConnectAsync(_config.Ip, _config.Port);
            var timeoutTask = Task.Delay(Math.Max(500, _config.ConnectTimeoutMs));
            var done = Task.WhenAny(connectTask, timeoutTask).GetAwaiter().GetResult();

            return done == connectTask && tcp.Connected;
        }

        protected override async Task<bool> DoCheckDeviceExistsAsync(CancellationToken cancellationToken)
        {
            using var tcp = new TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(Math.Max(500, _config.ConnectTimeoutMs));

            try
            {
                await tcp.ConnectAsync(_config.Ip, _config.Port, timeoutCts.Token);
                return tcp.Connected;
            }
            catch
            {
                return false;
            }
        }

        protected override bool DoCheckAlive()
        {
            lock (_plcLock)
            {
                return _plc != null && _plc.IsConnected;
            }
        }

        protected override Task<bool> DoCheckAliveAsync(CancellationToken cancellationToken)
        {
            return Task.Run(DoCheckAlive, cancellationToken);
        }

        private CpuType ResolveCpuType()
        {
            var model = (_config.Model ?? string.Empty).ToUpperInvariant();
            if (model.Contains("1500")) return CpuType.S71500;
            if (model.Contains("1200")) return CpuType.S71200;
            if (model.Contains("300")) return CpuType.S7300;
            if (model.Contains("400")) return CpuType.S7400;
            return CpuType.S71200;
        }
    }
}
