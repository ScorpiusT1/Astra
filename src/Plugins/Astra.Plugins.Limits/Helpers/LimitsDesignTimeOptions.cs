using Astra.Core.Constants;
using Astra.Core.Data;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Plugins.Limits.Helpers
{
    /// <summary>
    /// 属性编辑器下拉：采集卡为当前插件中全部设备；通道为所选采集卡配置中的通道。
    /// </summary>
    public static class LimitsDesignTimeOptions
    {
        /// <summary>
        /// 采集卡 / 通道未选择时的下拉显示文案（内部存储仍用空字符串表示未选）。
        /// </summary>
        public const string UnselectedLabel = AstraSharedConstants.DesignTimeLabels.Unselected;

        /// <summary>
        /// 下拉首项文案（底层仍对应「未指定通道名 = 组内第一个通道」）。勿与真实通道名重复。
        /// </summary>
        public const string UseFirstChannelInGroupLabel = AstraSharedConstants.DesignTimeLabels.UseFirstChannelInGroup;

        /// <summary>
        /// 当前已注册采集卡；首项为 <see cref="UnselectedLabel"/>。
        /// </summary>
        public static IEnumerable<string> GetAcquisitionDeviceNames()
        {
            var list = AcquisitionDeviceCatalog.GetAcquisitionDeviceDisplayNames().ToList();
            list.Insert(0, UnselectedLabel);
            return list;
        }

        /// <summary>
        /// 未选采集卡时仅显示 <see cref="UnselectedLabel"/>；已选时首项为 <see cref="UseFirstChannelInGroupLabel"/>（空 = 组内首通道）。
        /// </summary>
        public static IEnumerable<string> GetChannelNamesForDevice(string? deviceDisplayName)
        {
            var d = deviceDisplayName?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(d) || string.Equals(d, UnselectedLabel, StringComparison.Ordinal))
            {
                return new[] { UnselectedLabel };
            }

            var list = AcquisitionDeviceCatalog.GetConfiguredChannelNamesForDeviceDisplayName(d).ToList();
            if (list.Count > 0 && list[0] == string.Empty)
            {
                list[0] = UseFirstChannelInGroupLabel;
            }

            return list;
        }
    }
}
