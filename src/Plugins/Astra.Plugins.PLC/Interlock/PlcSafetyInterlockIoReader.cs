using Astra.Contract.Communication.Abstractions;
using Astra.Core.Foundation.Common;
using Astra.Core.Triggers.Interlock;

namespace Astra.Plugins.PLC.Interlock
{
    /// <summary>
    /// 基于当前 <see cref="PlcPlugin"/> 实例解析 IO 并读取 BOOL（无反射）。
    /// </summary>
    public sealed class PlcSafetyInterlockIoReader : ISafetyInterlockIoReader
    {
        public async Task<OperationResult<bool>> ReadBoolAsync(
            string plcDeviceName,
            string ioPointName,
            CancellationToken cancellationToken)
        {
            var plugin = PlcPlugin.Current;
            if (plugin == null)
            {
                return OperationResult<bool>.Failure("PLC 插件未加载或未就绪");
            }

            var plcName = plcDeviceName?.Trim() ?? string.Empty;
            var ioName = ioPointName?.Trim() ?? string.Empty;
            if (plcName.Length == 0 || ioName.Length == 0)
            {
                return OperationResult<bool>.Failure("PLC 设备名称或 IO 名称不能为空");
            }

            var io = plugin.FindIoByName(ioName);
            if (io == null)
            {
                return OperationResult<bool>.Failure($"未找到 IO 点位「{ioName}」");
            }

            if (!string.IsNullOrWhiteSpace(io.PlcDeviceName) &&
                !string.Equals(io.PlcDeviceName.Trim(), plcName, StringComparison.OrdinalIgnoreCase))
            {
                return OperationResult<bool>.Failure(
                    $"IO「{ioName}」绑定设备为「{io.PlcDeviceName.Trim()}」，与规则中的「{plcName}」不一致");
            }

            if (string.IsNullOrWhiteSpace(io.Address))
            {
                return OperationResult<bool>.Failure($"IO「{ioName}」未配置地址");
            }

            var plc = plugin.FindPlcByDeviceName(plcName);
            if (plc == null)
            {
                return OperationResult<bool>.Failure($"未找到 PLC 设备「{plcName}」");
            }

            var read = await plc.ReadAsync<bool>(io.Address.Trim(), cancellationToken).ConfigureAwait(false);
            if (!read.Success)
            {
                var msg = string.IsNullOrWhiteSpace(read.ErrorMessage) ? read.Message : read.ErrorMessage;
                return OperationResult<bool>.Failure(string.IsNullOrWhiteSpace(msg) ? "PLC 读取失败" : msg);
            }

            return OperationResult<bool>.Succeed(read.Data);
        }
    }
}
