using Astra.Core.Devices;
using Astra.Core.Devices.Attributes;
using Astra.Core.Foundation.Common;
using Astra.Plugins.PLC.Configs;
using Astra.Plugins.PLC.ViewModels;
using Astra.Plugins.PLC.Views;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Plugins.PLC.Devices
{
    [DeviceDebugUI(typeof(PlcDeviceDebugView), typeof(PlcDeviceDebugViewModel))]
    public class S7SiemensPlcDevice : PlcDeviceBase
    {
        private readonly S7SiemensPlcDeviceConnection _s7Connection;
        private readonly S7SiemensPlcDeviceConfig _s7Config;

        public override string Protocol => "S7";

        public S7SiemensPlcDevice(S7SiemensPlcDeviceConfig config)
            : base(new S7SiemensPlcDeviceConnection(config), config)
        {
            _s7Config = config ?? throw new ArgumentNullException(nameof(config));
            _s7Connection = (S7SiemensPlcDeviceConnection)_connection;
        }

        [JsonConstructor]
        public S7SiemensPlcDevice(S7SiemensPlcDeviceConfig currentConfig, bool _ = true)
            : this(currentConfig)
        {
        }

        public override OperationResult<T> Read<T>(string address)
        {
            var readResult = _s7Connection.Read(address);
            if (!readResult.Success)
            {
                return OperationResult<T>.Failure(readResult.ErrorMessage ?? "读取失败", readResult.ErrorCode);
            }

            try
            {
                var converted = ConvertValue<T>(readResult.Data);
                return OperationResult<T>.Succeed(converted, $"读取成功: {address}");
            }
            catch (Exception ex)
            {
                return OperationResult<T>.Fail($"读取地址 {address} 的值转换失败: {ex.Message}", ex, ErrorCodes.InvalidData);
            }
        }

        public override Task<OperationResult<T>> ReadAsync<T>(string address, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Read<T>(address), cancellationToken);
        }

        public override OperationResult Write<T>(string address, T value)
        {
            return _s7Connection.Write(address, value!);
        }

        public override Task<OperationResult> WriteAsync<T>(string address, T value, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Write(address, value), cancellationToken);
        }

        public override OperationResult<Dictionary<string, object>> BatchRead(Dictionary<string, string> addressMap)
        {
            if (addressMap == null || addressMap.Count == 0)
            {
                return OperationResult<Dictionary<string, object>>.Failure("批量读取地址映射不能为空", ErrorCodes.InvalidData);
            }

            var result = new Dictionary<string, object>();
            var failed = new List<string>();

            foreach (var pair in addressMap)
            {
                var readResult = _s7Connection.Read(pair.Value);
                if (readResult.Success)
                {
                    result[pair.Key] = readResult.Data!;
                }
                else
                {
                    failed.Add($"{pair.Key}({pair.Value}): {readResult.ErrorMessage}");
                }
            }

            if (failed.Count == 0)
            {
                return OperationResult<Dictionary<string, object>>.Succeed(result, $"批量读取成功，共 {result.Count} 项");
            }

            if (result.Count > 0)
            {
                return OperationResult<Dictionary<string, object>>.PartialSuccess(
                    $"批量读取部分成功：成功 {result.Count} 项，失败 {failed.Count} 项",
                    result,
                    string.Join("; ", failed),
                    ErrorCodes.ReceiveFailed);
            }

            return OperationResult<Dictionary<string, object>>.Failure(
                $"批量读取失败：{string.Join("; ", failed)}",
                ErrorCodes.ReceiveFailed);
        }

        public override Task<OperationResult<Dictionary<string, object>>> BatchReadAsync(
            Dictionary<string, string> addressMap,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() => BatchRead(addressMap), cancellationToken);
        }

        public override OperationResult BatchWrite(Dictionary<string, object> values)
        {
            if (values == null || values.Count == 0)
            {
                return OperationResult.Failure("批量写入数据不能为空", ErrorCodes.InvalidData);
            }

            var failed = new List<string>();

            foreach (var pair in values)
            {
                var writeResult = _s7Connection.Write(pair.Key, pair.Value);
                if (!writeResult.Success)
                {
                    failed.Add($"{pair.Key}: {writeResult.ErrorMessage}");
                }
            }

            if (failed.Count == 0)
            {
                return OperationResult.Succeed($"批量写入成功，共 {values.Count} 项");
            }

            if (failed.Count < values.Count)
            {
                return OperationResult.Failure(
                    $"批量写入部分失败：失败 {failed.Count}/{values.Count} 项。{string.Join("; ", failed)}",
                    ErrorCodes.SendFailed);
            }

            return OperationResult.Failure(
                $"批量写入失败：{string.Join("; ", failed)}",
                ErrorCodes.SendFailed);
        }

        public override Task<OperationResult> BatchWriteAsync(
            Dictionary<string, object> values,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() => BatchWrite(values), cancellationToken);
        }

        protected override void ApplyHotUpdateProperties(PlcDeviceConfig newConfig, List<string> changedProps)
        {
            base.ApplyHotUpdateProperties(newConfig, changedProps);

            if (newConfig is S7SiemensPlcDeviceConfig s7Config)
            {
                _s7Config.Ip = s7Config.Ip;
                _s7Config.Port = s7Config.Port;
                _s7Config.Rack = s7Config.Rack;
                _s7Config.Slot = s7Config.Slot;
                _s7Config.ConnectTimeoutMs = s7Config.ConnectTimeoutMs;
                _s7Config.ReadWriteTimeoutMs = s7Config.ReadWriteTimeoutMs;
                _s7Config.AutoReconnect = s7Config.AutoReconnect;
                _s7Config.ReconnectIntervalMs = s7Config.ReconnectIntervalMs;
            }
        }

        private static T ConvertValue<T>(object value)
        {
            if (value is null)
            {
                throw new InvalidOperationException("PLC 返回值为空");
            }

            if (value is T direct)
            {
                return direct;
            }

            var targetType = typeof(T);
            var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (underlying.IsEnum)
            {
                if (value is string s)
                {
                    return (T)Enum.Parse(underlying, s, true);
                }

                var enumValue = Enum.ToObject(underlying, value);
                return (T)enumValue;
            }

            var converted = Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
            return (T)converted!;
        }
    }
}
