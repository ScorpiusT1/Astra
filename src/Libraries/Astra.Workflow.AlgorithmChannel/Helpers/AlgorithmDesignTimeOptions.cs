using Astra.Core.Constants;
using Astra.Core.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Workflow.AlgorithmChannel.Helpers
{
    /// <summary>属性面板下拉：采集卡与通道（与 Limits / Audio 插件一致）。</summary>
    public static class AlgorithmDesignTimeOptions
    {
        public const string UnselectedLabel = AstraSharedConstants.DesignTimeLabels.Unselected;
        public const string UseFirstChannelInGroupLabel = AstraSharedConstants.DesignTimeLabels.UseFirstChannelInGroup;

        public static IEnumerable<string> GetAcquisitionDeviceNames()
        {
            var list = AcquisitionDeviceCatalog.GetAcquisitionDeviceDisplayNames().ToList();
            list.Insert(0, UnselectedLabel);
            return list;
        }

        /// <summary>多选 CheckComboBox 使用：不含「未选择」占位。</summary>
        public static IEnumerable<string> GetAcquisitionDeviceNamesForMultiSelect()
        {
            return AcquisitionDeviceCatalog.GetAcquisitionDeviceDisplayNames();
        }

        public static IEnumerable<string> GetChannelNamesForDevice(string? deviceDisplayName)
        {
            var d = deviceDisplayName?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(d) || string.Equals(d, UnselectedLabel, StringComparison.Ordinal))
                return new[] { UnselectedLabel };

            var list = AcquisitionDeviceCatalog.GetConfiguredChannelNamesForDeviceDisplayName(d).ToList();
            if (list.Count > 0 && list[0] == string.Empty)
                list[0] = UseFirstChannelInGroupLabel;
            return list;
        }

        /// <summary>
        /// 聚合多个设备的通道列表，格式为「设备名/通道名」。
        /// 用于多选设备后的通道 CheckComboBox。
        /// </summary>
        public static IEnumerable<string> GetChannelNamesForDevices(IEnumerable<string>? deviceNames)
        {
            if (deviceNames == null)
                yield break;

            foreach (var d in deviceNames)
            {
                if (string.IsNullOrWhiteSpace(d))
                    continue;

                var channels = AcquisitionDeviceCatalog.GetConfiguredChannelNamesForDeviceDisplayName(d);
                foreach (var ch in channels)
                {
                    if (string.IsNullOrEmpty(ch) ||
                        string.Equals(ch, UseFirstChannelInGroupLabel, StringComparison.Ordinal))
                        continue;

                    yield return $"{d}/{ch}";
                }
            }
        }
    }
}
