using Astra.Core.Data;
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
        /// 从曲线通道配置解析采集卡显示名与 NVH 通道键：<c>设备/通道</c> 用具体通道；仅 <c>设备</c> 表示该卡首通道。
        /// </summary>
        public static bool TryResolveCurveSelection(
            string? configured,
            out string deviceDisplayName,
            out string? nvhChannelKey,
            out string? error)
        {
            deviceDisplayName = string.Empty;
            nvhChannelKey = null;
            error = null;

            if (string.IsNullOrWhiteSpace(configured))
            {
                error = "请选择通道";
                return false;
            }

            var t = configured.Trim();
            if (string.Equals(t, LimitsDesignTimeOptions.UnselectedLabel, StringComparison.Ordinal))
            {
                error = "请选择通道";
                return false;
            }

            if (string.Equals(t, LimitsDesignTimeOptions.UseFirstChannelInGroupLabel, StringComparison.Ordinal))
            {
                error = "请从下拉选择具体设备或「设备/通道」";
                return false;
            }

            if (QualifiedChannelHelper.TrySplit(t, out var dev, out var ch))
            {
                deviceDisplayName = dev;
                nvhChannelKey = string.IsNullOrEmpty(ch) ? null : ch;
                return true;
            }

            deviceDisplayName = t;
            nvhChannelKey = null;
            return true;
        }

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

            if (QualifiedChannelHelper.TrySplit(t, out _, out var ch))
                return string.IsNullOrEmpty(ch) ? null : ch;

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

        /// <summary>
        /// 解析实测标量：优先 <see cref="NodeContext.InputData"/>（含 <c>节点Id:Scalar.xxx</c>），否则 <see cref="NodeContext.GlobalVariables"/>。
        /// </summary>
        public static bool TryResolveMeasuredScalar(NodeContext context, string? key, out object? raw, out string? error)
        {
            raw = null;
            error = null;
            var k = key?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(k))
            {
                error = "请填写实测值变量名或选择上游标量键";
                return false;
            }

            if (context.InputData != null && context.InputData.TryGetValue(k, out raw))
                return true;

            if (context.GlobalVariables != null && context.GlobalVariables.TryGetValue(k, out raw))
                return true;

            error = $"找不到上游输出或全局变量: {k}";
            return false;
        }

        public static ExecutionResult WithOptionalChartDisplay(
            ExecutionResult result,
            NodeContext context,
            string producerNodeId,
            bool associateCurveForDisplay,
            string? chartArtifactKey,
            string? nvhChannelKeyInSignalGroup)
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

            var filter = nvhChannelKeyInSignalGroup ?? string.Empty;
            TryPublishLimitChartPayloadForReport(context, producerNodeId, key, filter);

            return result
                .WithOutput(NodeUiOutputKeys.HasChartData, true)
                .WithOutput(NodeUiOutputKeys.ChartArtifactKey, key)
                .WithOutput(NodeUiOutputKeys.ChartNvhChannelFilter, filter);
        }

        /// <summary>
        /// 曲线卡控 / 值+曲线等节点在已解析 Raw 与通道时，统一写入主页过滤键并发布单通道 <see cref="ChartDisplayPayload"/> 到测试总线（报告与曲线配图使用）。
        /// </summary>
        public static ExecutionResult WithNvhCurveChartOutputs(
            ExecutionResult result,
            NodeContext context,
            string producerNodeId,
            bool showChart,
            string? chartArtifactKey,
            string? nvhChannelKeyInSignalGroup)
        {
            if (!showChart || string.IsNullOrWhiteSpace(chartArtifactKey))
            {
                return result.WithOutput(NodeUiOutputKeys.HasChartData, false);
            }

            var key = chartArtifactKey.Trim();
            if (!context.TryGetArtifact<NvhMemoryFile>(key, out var nvh) || nvh == null)
            {
                return result.WithOutput(NodeUiOutputKeys.HasChartData, false);
            }

            var filter = nvhChannelKeyInSignalGroup ?? string.Empty;
            TryPublishLimitChartPayloadForReport(context, producerNodeId, key, filter);

            return result
                .WithOutput(NodeUiOutputKeys.HasChartData, true)
                .WithOutput(NodeUiOutputKeys.ChartArtifactKey, key)
                .WithOutput(NodeUiOutputKeys.ChartNvhChannelFilter, filter);
        }

        private static void TryPublishLimitChartPayloadForReport(
            NodeContext context,
            string producerNodeId,
            string chartArtifactKey,
            string nvhChannelFilterStored)
        {
            var bus = context.GetDataBus();
            if (bus == null)
            {
                return;
            }

            if (!context.TryGetArtifact<NvhMemoryFile>(chartArtifactKey, out var file) || file == null)
            {
                return;
            }

            var ch = string.IsNullOrWhiteSpace(nvhChannelFilterStored) ? null : nvhChannelFilterStored.Trim();
            if (!NvhCurveSampleUtil.TryExtractAsDoubleArray(
                    file,
                    LimitCurveArtifactResolver.NvhSignalGroupName,
                    ch,
                    out var samples,
                    out var wfInc) ||
                samples.Length == 0)
            {
                return;
            }

            var payload = new ChartDisplayPayload
            {
                Kind = ChartPayloadKind.Signal1D,
                SignalY = samples,
                SamplePeriod = wfInc > 0 ? wfInc : 1.0,
                BottomAxisLabel = "样本",
                LeftAxisLabel = "数值"
            };

            bus.PublishAlgorithmResult(producerNodeId, "LimitCurveChart", payload, tag: "limits");
        }
    }
}
