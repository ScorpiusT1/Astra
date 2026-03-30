using Astra.Core.Nodes.Models;
using Astra.UI.Abstractions.Nodes;
using NVHDataBridge.Models;
using System;
using System.Globalization;

namespace Astra.Plugins.Limits.Helpers
{
    internal static class LimitNodeShared
    {
        /// <summary>
        /// 将节点上配置的通道名转为 NVH 解析用的通道键：空、空白或「默认首通道」文案均视为未指定（组内首通道）。
        /// </summary>
        public static string? NormalizeCurveChannelKey(string? configured)
        {
            if (string.IsNullOrWhiteSpace(configured))
            {
                return null;
            }

            var t = configured.Trim();
            if (string.Equals(t, LimitsDesignTimeOptions.UseFirstChannelInGroupLabel, StringComparison.Ordinal) ||
                string.Equals(t, LimitsDesignTimeOptions.UnselectedLabel, StringComparison.Ordinal))
            {
                return null;
            }

            return t;
        }

        public static bool TryConvertToDouble(object? v, out double d)
        {
            d = default;
            if (v == null)
            {
                return false;
            }

            switch (v)
            {
                case double dv:
                    d = dv;
                    return true;
                case float fv:
                    d = fv;
                    return true;
                case int iv:
                    d = iv;
                    return true;
                case long lv:
                    d = lv;
                    return true;
                case string s when double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var ds):
                    d = ds;
                    return true;
                case string s2 when double.TryParse(s2, NumberStyles.Any, CultureInfo.CurrentCulture, out var ds2):
                    d = ds2;
                    return true;
                case IConvertible conv:
                    try
                    {
                        d = Convert.ToDouble(conv, CultureInfo.InvariantCulture);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                default:
                    return false;
            }
        }

        public static void NormalizeLimits(ref double lower, ref double upper)
        {
            if (lower > upper)
            {
                (lower, upper) = (upper, lower);
            }
        }

        public static ExecutionResult WithOptionalChartDisplay(
            ExecutionResult result,
            NodeContext context,
            bool associateCurveForDisplay,
            string? chartArtifactKey)
        {
            if (!associateCurveForDisplay || string.IsNullOrWhiteSpace(chartArtifactKey))
            {
                return result
                    .WithOutput(NodeUiOutputKeys.HasChartData, false);
            }

            var key = chartArtifactKey.Trim();
            if (!context.TryGetArtifact<NvhMemoryFile>(key, out var _nvh) || _nvh == null)
            {
                return result
                    .WithOutput(NodeUiOutputKeys.HasChartData, false);
            }

            return result
                .WithOutput(NodeUiOutputKeys.HasChartData, true)
                .WithOutput(NodeUiOutputKeys.ChartArtifactKey, key);
        }
    }
}
