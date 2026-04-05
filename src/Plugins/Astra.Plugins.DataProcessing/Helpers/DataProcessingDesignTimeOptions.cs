using Astra.Core.Constants;
using Astra.Core.Data;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Plugins.DataProcessing.Helpers
{
    internal static class DataProcessingDesignTimeOptions
    {
        public const string UnselectedLabel = AstraSharedConstants.DesignTimeLabels.Unselected;
        public const string UseFirstChannelInGroupLabel = AstraSharedConstants.DesignTimeLabels.UseFirstChannelInGroup;

        public static IEnumerable<string> GetAcquisitionDeviceNames()
        {
            var list = AcquisitionDeviceCatalog.GetAcquisitionDeviceDisplayNames().ToList();
            list.Insert(0, UnselectedLabel);
            return list;
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
    }
}
