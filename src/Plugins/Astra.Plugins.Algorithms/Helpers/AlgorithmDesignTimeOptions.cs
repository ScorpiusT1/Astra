using Astra.Core.Constants;
using Astra.Plugins.DataAcquisition.Providers;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Plugins.Algorithms.Helpers
{
    /// <summary>属性面板下拉：采集卡与通道（与 Limits / Audio 插件一致）。</summary>
    public static class AlgorithmDesignTimeOptions
    {
        public const string UnselectedLabel = AstraSharedConstants.DesignTimeLabels.Unselected;
        public const string UseFirstChannelInGroupLabel = AstraSharedConstants.DesignTimeLabels.UseFirstChannelInGroup;

        public static IEnumerable<string> GetAcquisitionDeviceNames()
        {
            var list = DataAcquisitionCardProvider.GetDataAcquisitionNames().ToList();
            list.Insert(0, UnselectedLabel);
            return list;
        }

        public static IEnumerable<string> GetChannelNamesForDevice(string? deviceDisplayName)
        {
            var d = deviceDisplayName?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(d) || string.Equals(d, UnselectedLabel, StringComparison.Ordinal))
                return new[] { UnselectedLabel };

            var list = DataAcquisitionCardProvider.GetConfiguredChannelNamesForDeviceDisplayName(d).ToList();
            if (list.Count > 0 && list[0] == string.Empty)
                list[0] = UseFirstChannelInGroupLabel;
            return list;
        }
    }
}
