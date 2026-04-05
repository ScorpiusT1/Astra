using System;
using System.Collections.Generic;
using System.Linq;
using Astra.Plugins.DataProcessing.Helpers;
using Astra.Core.Data;
using Astra.Core.Nodes.Models;
using Astra.UI.Abstractions.Nodes;
using NVHDataBridge.Models;
using System.ComponentModel.DataAnnotations;

namespace Astra.Plugins.DataProcessing.Nodes
{
    /// <summary>
    /// NWaves IIR 滤波节点基类：多设备、多通道输入与多路 Raw 发布均在此实现。
    /// 所有派生节点（Butterworth / Bessel / Chebyshev I·II / 椭圆 × 低·高·带通）共用本逻辑，无需各自重复实现。
    /// </summary>
    public abstract class IirFilterNodeBase : DataProcessingNodeBase, IRawDataPipelineNode, IMultiRawDataPipelineNode
    {
        protected IirFilterNodeBase(string nodeTypeKey, string defaultName) : base(nodeTypeKey, defaultName)
        {
        }

        /// <summary>兼容 <see cref="IRawDataPipelineNode"/>：解析时优先匹配首设备，多设备时由 <see cref="IMultiRawDataPipelineNode"/> 兜底。</summary>
        public string DataAcquisitionDeviceDisplayName =>
            TryResolveTargetDeviceForDesignTime(out var d) ? d : string.Empty;

        IEnumerable<string> IMultiRawDataPipelineNode.DataAcquisitionDeviceDisplayNames =>
            EnumerateManagedRawDeviceDisplayNames();

        private IEnumerable<string> EnumerateManagedRawDeviceDisplayNames()
        {
            if (!TryBuildFilterDevicePlans(out var plans, out _) || plans.Count == 0)
                return Array.Empty<string>();
            return plans.Select(p => p.DeviceDisplayName).Distinct(StringComparer.Ordinal);
        }

        [Display(Name = "阶数", GroupName = "滤波", Order = 0)]
        public int FilterOrder { get; set; } = 4;

        /// <summary>对采样数据执行当前节点对应的滤波。</summary>
        protected abstract double[] ApplyFilter(double[] samples, int samplingRate, int order);

        /// <summary>发布到算法结果的图表产物名（须各类节点唯一）。</summary>
        protected abstract string ChartArtifactName { get; }

        /// <summary>结果标签，用于 UI 区分。</summary>
        protected abstract string ResultTag { get; }

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            if (!TryBuildFilterDevicePlans(out var plans, out var planErr))
                return Task.FromResult(ExecutionResult.Failed(planErr ?? "无法解析滤波计划。"));

            var bus = context.GetDataBus();
            if (bus == null)
                return Task.FromResult(ExecutionResult.Failed("无法从数据总线读取 Raw。"));

            var groupName = DataProcessingRawArtifactHelper.NvhSignalGroupName;
            var charts = new List<(string SeriesName, ChartDisplayPayload Chart)>();

            foreach (var plan in plans)
            {
                if (!DataProcessingRawArtifactHelper.TryResolveRawArtifactKey(
                        context, Id, plan.DeviceDisplayName, out var key, out var err))
                    return Task.FromResult(ExecutionResult.Failed(err ?? "无法解析 Raw"));

                if (!bus.TryGet<NvhMemoryFile>(key, out var file) || file == null)
                    return Task.FromResult(ExecutionResult.Failed("无法从数据总线读取 Raw。"));

                if (!AcquisitionDeviceCatalog.TryGetDeviceIdByDisplayName(plan.DeviceDisplayName, out var deviceId))
                    return Task.FromResult(ExecutionResult.Failed($"采集卡无效: {plan.DeviceDisplayName}"));

                List<string?> channelKeys;
                if (plan.FilterAllChannelsInRaw)
                {
                    var names = DataProcessingNvhSampleUtil.ListFilterableChannelNames(file, groupName);
                    if (names.Count == 0)
                        return Task.FromResult(ExecutionResult.Failed($"设备「{plan.DeviceDisplayName}」Raw 中无可滤波的 float/double 通道。"));
                    channelKeys = names.Select<string, string?>(n => n).ToList();
                }
                else
                    channelKeys = plan.ExplicitChannelNames.Select<string, string?>(n => n).ToList();

                NvhMemoryFile? outFile = null;
                foreach (var chKey in channelKeys)
                {
                    if (!DataProcessingNvhSampleUtil.TryExtractAsDoubleArray(file, groupName, chKey, out var samples) ||
                        samples.Length == 0)
                        return Task.FromResult(ExecutionResult.Failed(
                            $"无法读取通道样本（{plan.DeviceDisplayName}/{chKey ?? "首通道"}）。"));

                    if (!DataProcessingNvhSampleUtil.TryGetWaveformIncrement(file, groupName, chKey, out var dt) || dt <= 0)
                        dt = 1.0 / 25600.0;

                    var fs = (int)Math.Clamp(Math.Round(1.0 / dt), 1, int.MaxValue);
                    var order = Math.Clamp(FilterOrder, 1, 16);

                    double[] filtered;
                    try
                    {
                        filtered = ApplyFilter(samples, fs, order);
                    }
                    catch (Exception ex)
                    {
                        return Task.FromResult(ExecutionResult.Failed(ex.Message, ex));
                    }

                    var resolvedChannelName = ResolveChannelNameForClone(file, chKey);
                    if (string.IsNullOrEmpty(resolvedChannelName))
                        return Task.FromResult(ExecutionResult.Failed("无法确定通道名。"));

                    try
                    {
                        var basis = outFile ?? file;
                        outFile = NvhMemoryFileCloneHelper.CloneReplacingChannelSamples(
                            basis,
                            groupName,
                            resolvedChannelName,
                            filtered);
                    }
                    catch (Exception ex)
                    {
                        return Task.FromResult(ExecutionResult.Failed("构建滤波后 NVH 数据失败: " + ex.Message, ex));
                    }

                    var period = dt > 0 ? dt : 1.0 / 25600.0;
                    var seriesLabel = string.IsNullOrEmpty(resolvedChannelName)
                        ? $"{plan.DeviceDisplayName}/首通道"
                        : $"{plan.DeviceDisplayName}/{resolvedChannelName}";
                    charts.Add((seriesLabel, ChartDisplayPayloadFactory.Signal1D(
                        filtered,
                        samplePeriod: period,
                        bottomAxisLabel: "时间 (s)",
                        leftAxisLabel: "幅值")));
                }

                if (outFile == null || channelKeys.Count == 0)
                    return Task.FromResult(ExecutionResult.Failed($"设备「{plan.DeviceDisplayName}」未处理任何通道。"));

                bus.PublishRawData(
                    producerNodeId: Id,
                    artifactName: $"{deviceId}:raw",
                    rawData: outFile,
                    displayName: $"{plan.DeviceDisplayName}-Filtered",
                    deviceId: deviceId,
                    includeInTestReport: IncludeInTestReport);
            }

            if (charts.Count == 0)
                return Task.FromResult(ExecutionResult.Failed("未生成任何滤波结果。"));

            return Task.FromResult(DataProcessingResultPublisher.SuccessWithMultiChart(
                context, Id, ChartArtifactName, charts, tag: ResultTag, includeInTestReport: IncludeInTestReport));
        }

        protected static string ResolveChannelNameForClone(NvhMemoryFile file, string? configuredChannelKey)
        {
            if (!string.IsNullOrWhiteSpace(configuredChannelKey) &&
                !string.Equals(configuredChannelKey.Trim(), DataProcessingDesignTimeOptions.UseFirstChannelInGroupLabel, StringComparison.Ordinal))
                return configuredChannelKey.Trim();

            if (!file.TryGetGroup(DataProcessingRawArtifactHelper.NvhSignalGroupName, out var g) || g == null)
                g = file.Groups.Values.FirstOrDefault();
            return g?.Channels.Keys.FirstOrDefault() ?? string.Empty;
        }
    }
}
