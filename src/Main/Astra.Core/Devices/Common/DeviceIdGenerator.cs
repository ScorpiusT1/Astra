using System;

namespace Astra.Core.Devices.Common
{
    /// <summary>
    /// 提供统一的设备 ID 生成工具
    /// </summary>
    public static class DeviceIdGenerator
    {
        /// <summary>
        /// 根据分组、槽位、序列号等信息生成标准化设备 ID
        /// </summary>
        public static string Generate(
            string prefix,
            string groupId,
            string slotId,
            string serialNumber,
            string deviceName)
        {
            var groupPart = string.IsNullOrWhiteSpace(groupId) ? "G0" : groupId.Trim();
            var slotPart = string.IsNullOrWhiteSpace(slotId) ? "S0" : slotId.Trim();

            string baseId;
            if (!string.IsNullOrWhiteSpace(serialNumber))
            {
                baseId = serialNumber.Trim();
            }
            else if (!string.IsNullOrWhiteSpace(deviceName))
            {
                baseId = deviceName.Replace(" ", string.Empty);
            }
            else
            {
                baseId = Guid.NewGuid().ToString("N");
            }

            if (!string.IsNullOrWhiteSpace(prefix))
            {
                baseId = $"{prefix}-{baseId}";
            }

            return $"{groupPart}:{slotPart}:{baseId}".ToUpperInvariant();
        }

        /// <summary>
        /// 根据序列号生成标准化设备 ID（默认分组/槽位）
        /// </summary>
        public static string FromSerial(string serialNumber, string prefix = null)
        {
            return Generate(prefix, null, null, serialNumber, null);
        }

        /// <summary>
        /// 根据分组和槽位生成标准化设备 ID（自动分配随机后缀）
        /// </summary>
        public static string FromGroupSlot(string groupId, string slotId, string prefix = null)
        {
            return Generate(prefix, groupId, slotId, null, Guid.NewGuid().ToString("N"));
        }

        /// <summary>
        /// 根据分组、槽位与序列号生成标准化设备 ID
        /// </summary>
        public static string FromGroupSlotSerial(string groupId, string slotId, string serialNumber, string prefix = null)
        {
            return Generate(prefix, groupId, slotId, serialNumber, null);
        }
    }
}


