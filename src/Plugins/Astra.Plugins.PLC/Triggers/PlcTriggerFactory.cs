using Astra.Contract.Communication.Abstractions;
using Astra.Plugins.PLC.Providers;

namespace Astra.Plugins.PLC.Triggers
{
    /// <summary>
    /// 根据设备名称与 IO 配置名称创建 <see cref="PlcTrigger"/>。
    /// </summary>
    public static class PlcTriggerFactory
    {
        /// <summary>
        /// 使用已配置的 PLC 设备与 IO 点位（名称需在 IO 配置中存在且已启用）。
        /// </summary>
        /// <param name="plcDeviceName">与 PLC 设备配置中的设备名称一致</param>
        /// <param name="ioPointName">与 IO 配置中的 IO 名称一致</param>
        public static PlcTrigger? TryCreate(string? plcDeviceName, string? ioPointName)
        {
            var plugin = PlcPlugin.Current;
            if (plugin == null)
            {
                return null;
            }

            var plcName = plcDeviceName?.Trim() ?? string.Empty;
            var ioName = ioPointName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(plcName) || string.IsNullOrWhiteSpace(ioName))
            {
                return null;
            }

            var io = PlcIoProvider.FindByName(ioName);
            if (io == null || string.IsNullOrWhiteSpace(io.Address))
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(io.PlcDeviceName) &&
                !string.Equals(io.PlcDeviceName.Trim(), plcName, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var plc = plugin.FindPlcByDeviceName(plcName);
            if (plc == null)
            {
                return null;
            }

            return new PlcTrigger(plc, io.Address.Trim(), io.Name?.Trim());
        }
    }
}
