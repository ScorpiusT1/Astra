using Astra.Core.Data;
using Astra.Core.Nodes.Models;
using Astra.Core.Reporting;
using Astra.UI.Abstractions.Nodes;
using NVHDataBridge.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Astra.Plugins.Limits.Helpers
{
    internal static class LimitNodeShared
    {
        /// <summary>曲线限值节点写入总线的图表在报告中归入「曲线结果」分层。</summary>
        private static readonly Dictionary<string, object> LimitCurveChartReportPreview = new()
        {
            [ReportArtifactPreviewKeys.ChartReportSourceKind] = nameof(ReportChartSourceKind.CurveResult)
        };

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
            string? nvhChannelKeyInSignalGroup,
            bool includeInTestReport = true)
        {
            if (!associateCurveForDisplay)
            {
                return result
                    .WithOutput(NodeUiOutputKeys.HasChartData, false);
            }

            if (TryResolveUpstreamChartDisplayForLimits(context, out var upKey, out var upFilter))
            {
                var filter = string.IsNullOrEmpty(upFilter)
                    ? (nvhChannelKeyInSignalGroup ?? string.Empty)
                    : upFilter;
                return ApplyChartArtifactToLimitOutputs(
                    result,
                    context,
                    producerNodeId,
                    upKey,
                    filter,
                    includeInTestReport,
                    mergeHorizontalLimitsForChart: true);
            }

            if (string.IsNullOrWhiteSpace(chartArtifactKey))
            {
                return result
                    .WithOutput(NodeUiOutputKeys.HasChartData, false);
            }

            return ApplyChartArtifactToLimitOutputs(
                result,
                context,
                producerNodeId,
                chartArtifactKey.Trim(),
                nvhChannelKeyInSignalGroup ?? string.Empty,
                includeInTestReport,
                mergeHorizontalLimitsForChart: true);
        }

        /// <summary>
        /// 曲线卡控 / 值+曲线等节点在已解析 Raw 与通道时，统一写入主页过滤键并发布单通道 <see cref="ChartDisplayPayload"/> 到测试总线（报告与曲线配图使用）。
        /// </summary>
        /// <param name="mergeHorizontalLimitsForChart">false 时不把节点输出的上下限写入图表水平参考线（统计卡控等仅看波形）。</param>
        public static ExecutionResult WithNvhCurveChartOutputs(
            ExecutionResult result,
            NodeContext context,
            string producerNodeId,
            bool showChart,
            string? chartArtifactKey,
            string? nvhChannelKeyInSignalGroup,
            bool includeInTestReport = true,
            bool mergeHorizontalLimitsForChart = true)
        {
            if (!showChart)
            {
                return result.WithOutput(NodeUiOutputKeys.HasChartData, false);
            }

            if (TryResolveUpstreamChartDisplayForLimits(context, out var upKey, out var upFilter))
            {
                var filter = string.IsNullOrEmpty(upFilter)
                    ? (nvhChannelKeyInSignalGroup ?? string.Empty)
                    : upFilter;
                return ApplyChartArtifactToLimitOutputs(
                    result,
                    context,
                    producerNodeId,
                    upKey,
                    filter,
                    includeInTestReport,
                    mergeHorizontalLimitsForChart);
            }

            if (string.IsNullOrWhiteSpace(chartArtifactKey))
            {
                return result.WithOutput(NodeUiOutputKeys.HasChartData, false);
            }

            return ApplyChartArtifactToLimitOutputs(
                result,
                context,
                producerNodeId,
                chartArtifactKey.Trim(),
                nvhChannelKeyInSignalGroup ?? string.Empty,
                includeInTestReport,
                mergeHorizontalLimitsForChart);
        }

        /// <summary>
        /// 供 <see cref="ChartDisplayPayload.MergeAxisMetadata"/>：为 false 时复制输出并去掉上下限键，避免污染总线图表与主页参考线。
        /// </summary>
        private static Dictionary<string, object>? CopyOutputDataForChartMerge(
            Dictionary<string, object>? outputData,
            bool mergeHorizontalLimitsFromOutputs)
        {
            if (outputData == null || outputData.Count == 0)
            {
                return null;
            }

            if (mergeHorizontalLimitsFromOutputs)
            {
                return outputData;
            }

            var copy = new Dictionary<string, object>(outputData);
            copy.Remove(NodeUiOutputKeys.LowerLimit);
            copy.Remove(NodeUiOutputKeys.UpperLimit);
            return copy;
        }

        /// <summary>
        /// 上游算法/采集等节点经 <see cref="NodeContext.InputData"/> 传入的图表产物键（<see cref="NodeUiOutputKeys.ChartArtifactKey"/>），
        /// 对应总线中 <see cref="ChartDisplayPayload"/> 或 <see cref="NvhMemoryFile"/>；优先于按采集卡解析的 Raw 键用于主页/报告配图。
        /// </summary>
        internal static bool TryResolveUpstreamChartDisplayForLimits(
            NodeContext context,
            out string artifactKey,
            out string nvhChannelFilterFromUpstream)
        {
            artifactKey = string.Empty;
            nvhChannelFilterFromUpstream = string.Empty;

            if (context.InputData == null || context.InputData.Count == 0)
                return false;

            static bool TryReadKeys(
                IReadOnlyDictionary<string, object> input,
                out bool hasChart,
                out string? chartKey,
                out string? nvhFilter)
            {
                hasChart = input.TryGetValue(NodeUiOutputKeys.HasChartData, out var hObj) &&
                           hObj is bool hb &&
                           hb;
                chartKey = null;
                nvhFilter = null;
                if (!hasChart)
                    return false;
                if (!input.TryGetValue(NodeUiOutputKeys.ChartArtifactKey, out var kObj) ||
                    kObj is not string ks ||
                    string.IsNullOrWhiteSpace(ks))
                    return false;
                chartKey = ks.Trim();
                if (input.TryGetValue(NodeUiOutputKeys.ChartNvhChannelFilter, out var fObj) &&
                    fObj is string fs)
                    nvhFilter = fs;
                return true;
            }

            var prefixedChartKeySuffix = ":" + NodeUiOutputKeys.ChartArtifactKey;
            foreach (var kvp in context.InputData.OrderByDescending(x => x.Key.Length))
            {
                if (!kvp.Key.EndsWith(prefixedChartKeySuffix, StringComparison.Ordinal) ||
                    kvp.Key.Length <= prefixedChartKeySuffix.Length)
                    continue;
                if (kvp.Value is not string candidate || string.IsNullOrWhiteSpace(candidate))
                    continue;
                var sourceId = kvp.Key[..^prefixedChartKeySuffix.Length];
                if (string.IsNullOrEmpty(sourceId))
                    continue;
                if (!context.InputData.TryGetValue($"{sourceId}:{NodeUiOutputKeys.HasChartData}", out var hObj) ||
                    hObj is not bool hb ||
                    !hb)
                    continue;
                var ck = candidate.Trim();
                if (!context.TryGetArtifact(ck, out var obj) || obj is not (ChartDisplayPayload or NvhMemoryFile))
                    continue;
                artifactKey = ck;
                if (context.InputData.TryGetValue($"{sourceId}:{NodeUiOutputKeys.ChartNvhChannelFilter}", out var nf) &&
                    nf is string nfs)
                    nvhChannelFilterFromUpstream = nfs ?? string.Empty;
                return true;
            }

            if (!TryReadKeys(context.InputData, out _, out var key, out var filter))
                return false;
            if (string.IsNullOrEmpty(key))
                return false;
            if (!context.TryGetArtifact(key, out var o) || o is not (ChartDisplayPayload or NvhMemoryFile))
                return false;
            artifactKey = key;
            nvhChannelFilterFromUpstream = filter ?? string.Empty;
            return true;
        }

        /// <summary>
        /// 自动模式：已解析到上游图表键且能从总线抽出非空 <c>Y</c> 或 <c>SignalY</c>（或 NVH 文件中的 Signal 通道）时返回 true。
        /// </summary>
        internal static bool TryResolveUpstreamCurveSamplesForAuto(
            NodeContext context,
            string? nvhChannelKeyFromCurveSelection,
            out double[]? samples)
        {
            samples = null;
            if (!TryResolveUpstreamChartDisplayForLimits(context, out var artifactKey, out var upFilter))
            {
                return false;
            }

            return TryExtractUpstreamChartSamplesCore(
                context,
                artifactKey,
                upFilter,
                nvhChannelKeyFromCurveSelection,
                out samples) &&
                   samples is { Length: > 0 };
        }

        /// <summary>
        /// 强制图表模式：解析失败或样本为空时写入 <paramref name="error"/>。
        /// </summary>
        internal static bool TryGetCurveLimitSamplesFromUpstreamChart(
            NodeContext context,
            string? nvhChannelKeyFromCurveSelection,
            out double[]? samples,
            out string? error)
        {
            samples = null;
            error = null;

            if (!TryResolveUpstreamChartDisplayForLimits(context, out var artifactKey, out var upFilter))
            {
                error = "上游未提供有效图表数据（HasChartData / ChartArtifactKey），或总线中无对应工件";
                return false;
            }

            if (!TryExtractUpstreamChartSamplesCore(
                    context,
                    artifactKey,
                    upFilter,
                    nvhChannelKeyFromCurveSelection,
                    out samples) ||
                samples == null ||
                samples.Length == 0)
            {
                error = $"无法从上游图表抽取非空曲线样本（需 Y 或 SignalY，或 NVH Signal 通道）: {artifactKey}";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 与 <see cref="ApplyChartArtifactToLimitOutputs"/> 一致：上游 <c>ChartNvhChannelFilter</c> 非空时优先用于系列/通道匹配。
        /// </summary>
        private static bool TryExtractUpstreamChartSamplesCore(
            NodeContext context,
            string artifactKey,
            string upFilter,
            string? nvhChannelKeyFromCurveSelection,
            out double[]? samples)
        {
            samples = null;
            if (!context.TryGetArtifact(artifactKey.Trim(), out var obj) || obj == null)
            {
                return false;
            }

            var channelForSeries = string.IsNullOrEmpty(upFilter)
                ? (nvhChannelKeyFromCurveSelection ?? string.Empty)
                : upFilter.Trim();

            if (obj is ChartDisplayPayload cp)
            {
                return TryExtractSamplesFromChartDisplayPayload(cp, channelForSeries, out samples) &&
                       samples is { Length: > 0 };
            }

            if (obj is NvhMemoryFile file)
            {
                var ch = string.IsNullOrWhiteSpace(channelForSeries) ? null : channelForSeries;
                if (!NvhCurveSampleUtil.TryExtractAsDoubleArray(
                        file,
                        LimitCurveArtifactResolver.NvhSignalGroupName,
                        ch,
                        out var raw) ||
                    raw.Length == 0)
                {
                    return false;
                }

                samples = raw;
                return true;
            }

            return false;
        }

        /// <summary>多系列时按通道键匹配单条系列后取 <see cref="ChartDisplayPayload.Y"/>（优先，如频谱）或 <see cref="ChartDisplayPayload.SignalY"/>。</summary>
        private static bool TryExtractSamplesFromChartDisplayPayload(
            ChartDisplayPayload payload,
            string? nvhChannelKeyInSignalGroup,
            out double[]? samples)
        {
            samples = null;
            var prepared = CoerceLimitCurveChartToSingleChannel(payload.Clone(), nvhChannelKeyInSignalGroup);
            if (prepared.Y is { Length: > 0 } y)
            {
                samples = y;
                return true;
            }

            if (prepared.SignalY is { Length: > 0 } sy)
            {
                samples = sy;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 自动模式：从上游图表工件解析出与 Y/SignalY 对齐的 X 轴（显式 <see cref="ChartDisplayPayload.X"/> 或按 <see cref="ChartDisplayPayload.SamplePeriod"/> 生成）。
        /// </summary>
        internal static bool TryResolveUpstreamCurveXyForAuto(
            NodeContext context,
            string? nvhChannelKeyFromCurveSelection,
            out double[]? x,
            out double[]? y)
        {
            x = null;
            y = null;
            if (!TryResolveUpstreamChartDisplayForLimits(context, out var artifactKey, out var upFilter))
            {
                return false;
            }

            return TryExtractUpstreamChartXyCore(
                context,
                artifactKey,
                upFilter,
                nvhChannelKeyFromCurveSelection,
                out x,
                out y) &&
                   x != null &&
                   y != null &&
                   x.Length == y.Length &&
                   y.Length > 0;
        }

        /// <summary>强制图表路径：失败时返回可读错误。</summary>
        internal static bool TryGetCurveLimitXyFromUpstreamChart(
            NodeContext context,
            string? nvhChannelKeyFromCurveSelection,
            out double[]? x,
            out double[]? y,
            out string? error)
        {
            x = null;
            y = null;
            error = null;

            if (!TryResolveUpstreamChartDisplayForLimits(context, out var artifactKey, out var upFilter))
            {
                error = "上游未提供有效图表数据（HasChartData / ChartArtifactKey），或总线中无对应工件";
                return false;
            }

            if (!TryExtractUpstreamChartXyCore(
                    context,
                    artifactKey,
                    upFilter,
                    nvhChannelKeyFromCurveSelection,
                    out x,
                    out y) ||
                x == null ||
                y == null ||
                x.Length != y.Length ||
                y.Length == 0)
            {
                error = $"无法从上游图表解析 X/Y 曲线序列: {artifactKey}";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 与曲线范围卡控节点的「曲线数据来源」逻辑一致：返回用于统计的 X、Y 数组（等长）及图表输出的 Raw 回退键。
        /// </summary>
        internal static bool TryGetCurveXySeries(
            NodeContext context,
            string limitsNodeId,
            CurveLimitSampleSource source,
            string deviceDisplayName,
            string? nvhChannelKey,
            out double[] x,
            out double[] y,
            out string? rawArtifactKeyForChartOutputs,
            out bool samplesFromUpstreamChart,
            out string? error)
        {
            x = Array.Empty<double>();
            y = Array.Empty<double>();
            rawArtifactKeyForChartOutputs = null;
            samplesFromUpstreamChart = false;
            error = null;

            switch (source)
            {
                case CurveLimitSampleSource.ChartOnly:
                    if (!TryGetCurveLimitXyFromUpstreamChart(context, nvhChannelKey, out var cx, out var cy, out error))
                    {
                        return false;
                    }

                    x = cx!;
                    y = cy!;
                    samplesFromUpstreamChart = true;
                    return true;

                case CurveLimitSampleSource.RawOnly:
                    if (!LimitCurveArtifactResolver.TryResolveRawArtifactKey(
                            context,
                            limitsNodeId,
                            deviceDisplayName,
                            out var rawKey,
                            out var rawErr))
                    {
                        error = rawErr;
                        return false;
                    }

                    if (!context.TryGetArtifact<NvhMemoryFile>(rawKey, out var rawFile) || rawFile == null)
                    {
                        error = $"无法从数据总线读取曲线数据: {rawKey}";
                        return false;
                    }

                    if (!NvhCurveSampleUtil.TryExtractAsDoubleArray(
                            rawFile,
                            LimitCurveArtifactResolver.NvhSignalGroupName,
                            nvhChannelKey,
                            out var rawY,
                            out var wfInc) ||
                        rawY.Length == 0)
                    {
                        error = "曲线样本为空或通道类型不支持";
                        return false;
                    }

                    var dt = wfInc > 0 ? wfInc : 1.0;
                    x = new double[rawY.Length];
                    for (var i = 0; i < rawY.Length; i++)
                    {
                        x[i] = i * dt;
                    }

                    y = rawY;
                    rawArtifactKeyForChartOutputs = rawKey;
                    return true;

                default:
                    if (TryResolveUpstreamCurveXyForAuto(context, nvhChannelKey, out var ax, out var ay) &&
                        ax != null &&
                        ay != null &&
                        ax.Length == ay.Length &&
                        ay.Length > 0)
                    {
                        x = ax;
                        y = ay;
                        samplesFromUpstreamChart = true;
                        rawArtifactKeyForChartOutputs = null;
                        return true;
                    }

                    if (!LimitCurveArtifactResolver.TryResolveRawArtifactKey(
                            context,
                            limitsNodeId,
                            deviceDisplayName,
                            out var autoRawKey,
                            out var autoRawErr))
                    {
                        error = autoRawErr;
                        return false;
                    }

                    if (!context.TryGetArtifact<NvhMemoryFile>(autoRawKey, out var autoRawFile) || autoRawFile == null)
                    {
                        error = $"无法从数据总线读取曲线数据: {autoRawKey}";
                        return false;
                    }

                    if (!NvhCurveSampleUtil.TryExtractAsDoubleArray(
                            autoRawFile,
                            LimitCurveArtifactResolver.NvhSignalGroupName,
                            nvhChannelKey,
                            out var autoY,
                            out var autoWfInc) ||
                        autoY.Length == 0)
                    {
                        error = "曲线样本为空或通道类型不支持";
                        return false;
                    }

                    var autoDt = autoWfInc > 0 ? autoWfInc : 1.0;
                    x = new double[autoY.Length];
                    for (var i = 0; i < autoY.Length; i++)
                    {
                        x[i] = i * autoDt;
                    }

                    y = autoY;
                    rawArtifactKeyForChartOutputs = autoRawKey;
                    return true;
            }
        }

        /// <summary>与 <see cref="TryExtractUpstreamChartSamplesCore"/> 对应，额外构造与 Y 等长的 X。</summary>
        private static bool TryExtractUpstreamChartXyCore(
            NodeContext context,
            string artifactKey,
            string upFilter,
            string? nvhChannelKeyFromCurveSelection,
            out double[]? x,
            out double[]? y)
        {
            x = null;
            y = null;
            if (!context.TryGetArtifact(artifactKey.Trim(), out var obj) || obj == null)
            {
                return false;
            }

            var channelForSeries = string.IsNullOrEmpty(upFilter)
                ? (nvhChannelKeyFromCurveSelection ?? string.Empty)
                : upFilter.Trim();

            if (obj is ChartDisplayPayload cp)
            {
                return TryExtractXyFromChartDisplayPayload(cp, channelForSeries, out x, out y);
            }

            if (obj is NvhMemoryFile file)
            {
                var ch = string.IsNullOrWhiteSpace(channelForSeries) ? null : channelForSeries;
                if (!NvhCurveSampleUtil.TryExtractAsDoubleArray(
                        file,
                        LimitCurveArtifactResolver.NvhSignalGroupName,
                        ch,
                        out var rawY,
                        out var wfInc) ||
                    rawY.Length == 0)
                {
                    return false;
                }

                y = rawY;
                var dt = wfInc > 0 ? wfInc : 1.0;
                x = new double[y.Length];
                for (var i = 0; i < y.Length; i++)
                {
                    x[i] = i * dt;
                }

                return true;
            }

            return false;
        }

        /// <summary>多系列收敛后：优先用载荷的 X；否则用序号 × <see cref="ChartDisplayPayload.SamplePeriod"/>。</summary>
        private static bool TryExtractXyFromChartDisplayPayload(
            ChartDisplayPayload payload,
            string? nvhChannelKeyInSignalGroup,
            out double[]? x,
            out double[]? y)
        {
            x = null;
            y = null;
            var prepared = CoerceLimitCurveChartToSingleChannel(payload.Clone(), nvhChannelKeyInSignalGroup);
            var dt = prepared.SamplePeriod > 0 ? prepared.SamplePeriod : 1.0;

            if (prepared.Y is { Length: > 0 } yArr)
            {
                y = yArr;
                if (prepared.X != null && prepared.X.Length == yArr.Length)
                {
                    x = prepared.X;
                }
                else
                {
                    x = new double[yArr.Length];
                    for (var i = 0; i < yArr.Length; i++)
                    {
                        x[i] = i * dt;
                    }
                }

                return true;
            }

            if (prepared.SignalY is { Length: > 0 } sy)
            {
                y = sy;
                x = new double[sy.Length];
                for (var i = 0; i < sy.Length; i++)
                {
                    x[i] = i * dt;
                }

                return true;
            }

            return false;
        }

        private static ExecutionResult ApplyChartArtifactToLimitOutputs(
            ExecutionResult result,
            NodeContext context,
            string producerNodeId,
            string artifactKey,
            string nvhChannelKeyInSignalGroup,
            bool includeInTestReport,
            bool mergeHorizontalLimitsForChart)
        {
            if (string.IsNullOrWhiteSpace(artifactKey) ||
                !context.TryGetArtifact(artifactKey.Trim(), out var obj) ||
                obj == null)
            {
                return result.WithOutput(NodeUiOutputKeys.HasChartData, false);
            }

            var key = artifactKey.Trim();
            var mergeSource = CopyOutputDataForChartMerge(result.OutputData, mergeHorizontalLimitsForChart);

            if (obj is ChartDisplayPayload chartPayload)
            {
                // 以曲线/限值节点身份重新发布到总线，使 __ProducerNodeId 与节点 Id 一致；否则报告仅按生产者匹配时会落到
                // 其它「纳入报告」的算法图（如导入预览），且上游算法节点常关闭纳入报告导致无法命中。
                var bus = context.GetDataBus();
                var chartKeyForOutputs = key;
                if (bus != null)
                {
                    var merged = ChartDisplayPayload.MergeAxisMetadata(chartPayload.Clone(), mergeSource);
                    // 上游多为多 Series（如导入预览），必须按曲线节点通道筛选为单路，否则报告会按系列拆成多张「曲线数据」图。
                    var prepared = CoerceLimitCurveChartToSingleChannel(merged, nvhChannelKeyInSignalGroup);
                    var toPublish = mergeHorizontalLimitsForChart
                        ? prepared
                        : prepared.WithoutHorizontalLimitLines();
                    var published = bus.PublishAlgorithmResult(
                        producerNodeId,
                        "LimitCurveChart",
                        toPublish,
                        tag: "limits",
                        parameters: LimitCurveChartReportPreview,
                        includeInTestReport: includeInTestReport);
                    chartKeyForOutputs = published.Key;
                }

                var filterOut = string.IsNullOrWhiteSpace(nvhChannelKeyInSignalGroup)
                    ? string.Empty
                    : nvhChannelKeyInSignalGroup.Trim();
                return result
                    .WithOutput(NodeUiOutputKeys.HasChartData, true)
                    .WithOutput(NodeUiOutputKeys.ChartArtifactKey, chartKeyForOutputs)
                    .WithOutput(NodeUiOutputKeys.ChartNvhChannelFilter, filterOut);
            }

            if (obj is NvhMemoryFile)
            {
                var filter = nvhChannelKeyInSignalGroup ?? string.Empty;
                TryPublishLimitChartPayloadForReport(
                    context,
                    producerNodeId,
                    key,
                    filter,
                    includeInTestReport,
                    mergeSource,
                    mergeHorizontalLimitsForChart,
                    out var publishedAlgoKey);
                var chartKeyForOutputs = !string.IsNullOrEmpty(publishedAlgoKey) ? publishedAlgoKey : key;
                return result
                    .WithOutput(NodeUiOutputKeys.HasChartData, true)
                    .WithOutput(NodeUiOutputKeys.ChartArtifactKey, chartKeyForOutputs)
                    .WithOutput(NodeUiOutputKeys.ChartNvhChannelFilter, filter);
            }

            return result.WithOutput(NodeUiOutputKeys.HasChartData, false);
        }

        /// <summary>
        /// 将含 <see cref="ChartDisplayPayload.Series"/> 的载荷收敛为根级单图：与 NVH Raw 路径一致，有通道键则匹配系列名，否则取首条系列。
        /// </summary>
        private static ChartDisplayPayload CoerceLimitCurveChartToSingleChannel(
            ChartDisplayPayload merged,
            string? nvhChannelKeyInSignalGroup)
        {
            if (merged.Series is not { Count: > 0 } series)
            {
                return merged;
            }

            var ch = string.IsNullOrWhiteSpace(nvhChannelKeyInSignalGroup) ? null : nvhChannelKeyInSignalGroup.Trim();
            var index = FindBestMatchingSeriesIndex(series, ch);
            var inner = series[index].Data.Clone();

            static string PickAxis(string innerVal, string parentVal) =>
                string.IsNullOrWhiteSpace(innerVal) ? parentVal : innerVal;

            return new ChartDisplayPayload
            {
                Kind = inner.Kind,
                BottomAxisLabel = PickAxis(inner.BottomAxisLabel, merged.BottomAxisLabel),
                BottomAxisUnit = PickAxis(inner.BottomAxisUnit, merged.BottomAxisUnit),
                LeftAxisLabel = PickAxis(inner.LeftAxisLabel, merged.LeftAxisLabel),
                LeftAxisUnit = PickAxis(inner.LeftAxisUnit, merged.LeftAxisUnit),
                SignalY = inner.SignalY,
                SamplePeriod = inner.SamplePeriod,
                X = inner.X,
                Y = inner.Y,
                HeatmapZ = inner.HeatmapZ,
                HeatmapXCoordinates = inner.HeatmapXCoordinates,
                HeatmapYCoordinates = inner.HeatmapYCoordinates,
                HeatmapYAxisIsLog10OfQuantity = inner.HeatmapYAxisIsLog10OfQuantity,
                SegmentLines = inner.SegmentLines,
                HorizontalLimitLower = inner.HorizontalLimitLower ?? merged.HorizontalLimitLower,
                HorizontalLimitUpper = inner.HorizontalLimitUpper ?? merged.HorizontalLimitUpper,
                Categories = inner.Categories,
                BarGroups = inner.BarGroups,
                DonutFraction = inner.DonutFraction,
                ExplodeFraction = inner.ExplodeFraction,
                RadarAxisMaxValues = inner.RadarAxisMaxValues,
                Series = null,
                LayoutMode = ChartLayoutMode.SinglePlot,
                ScalarAnnotations = inner.ScalarAnnotations ?? merged.ScalarAnnotations
            };
        }

        /// <summary>
        /// 按 NVH 通道键匹配系列显示名（全名、<c>stem/键</c> 后缀、包含键）；匹配不到时退回索引 0（与组内首通道语义一致）。
        /// </summary>
        private static int FindBestMatchingSeriesIndex(IReadOnlyList<ChartSeriesEntry> series, string? channelKey)
        {
            if (string.IsNullOrEmpty(channelKey))
            {
                return 0;
            }

            for (var i = 0; i < series.Count; i++)
            {
                var name = series[i].Name?.Trim() ?? string.Empty;
                if (name.Length > 0 && string.Equals(name, channelKey, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            var suffix = "/" + channelKey;
            for (var i = 0; i < series.Count; i++)
            {
                var name = series[i].Name?.Trim() ?? string.Empty;
                if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            for (var i = 0; i < series.Count; i++)
            {
                var name = series[i].Name?.Trim() ?? string.Empty;
                if (name.Length > 0 &&
                    name.IndexOf(channelKey, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return i;
                }
            }

            return 0;
        }

        /// <summary>
        /// 将 Raw 中通道抽成 <see cref="ChartDisplayPayload"/> 并发布为算法产物；成功时 <paramref name="publishedAlgorithmKey"/> 为总线键。
        /// </summary>
        private static void TryPublishLimitChartPayloadForReport(
            NodeContext context,
            string producerNodeId,
            string chartArtifactKey,
            string nvhChannelFilterStored,
            bool includeInTestReport,
            Dictionary<string, object>? outputDataForMerge,
            bool mergeHorizontalLimitsForChart,
            out string? publishedAlgorithmKey)
        {
            publishedAlgorithmKey = null;
            var bus = context.GetDataBus();
            if (bus == null)
            {
                return;
            }

            if (!context.TryGetArtifact<NvhMemoryFile>(chartArtifactKey, out var file) || file == null)
            {
                return;
            }

            // 与曲线逐点判定一致：指定 NVH 通道键则只取该通道；未指定则取组内首路可用通道（不展开全组）。
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

            var merged = ChartDisplayPayload.MergeAxisMetadata(payload, outputDataForMerge);
            if (!mergeHorizontalLimitsForChart)
            {
                merged = merged.WithoutHorizontalLimitLines();
            }

            var published = bus.PublishAlgorithmResult(
                producerNodeId,
                "LimitCurveChart",
                merged,
                tag: "limits",
                parameters: LimitCurveChartReportPreview,
                includeInTestReport: includeInTestReport);
            publishedAlgorithmKey = published.Key;
        }
    }
}
