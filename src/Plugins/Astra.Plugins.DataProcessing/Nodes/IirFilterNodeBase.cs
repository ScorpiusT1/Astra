using System.Linq;
using Astra.Plugins.DataProcessing.Helpers;
using Astra.Core.Data;
using Astra.Core.Nodes.Models;
using Astra.Plugins.DataAcquisition.Providers;
using Astra.UI.Abstractions.Nodes;
using NVHDataBridge.Models;
using System.ComponentModel.DataAnnotations;

namespace Astra.Plugins.DataProcessing.Nodes
{
    /// <summary>NWaves IIR 滤波节点基类；输出与多采集相同命名的 Raw。</summary>
    public abstract class IirFilterNodeBase : DataProcessingNodeBase, IRawDataPipelineNode
    {
        protected IirFilterNodeBase(string nodeTypeKey, string defaultName) : base(nodeTypeKey, defaultName)
        {
        }

        public string DataAcquisitionDeviceDisplayName
        {
            get
            {
                var d = DataAcquisitionDeviceName?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(d) || string.Equals(d, DataProcessingDesignTimeOptions.UnselectedLabel, StringComparison.Ordinal))
                    return string.Empty;
                return d;
            }
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
            if (!DataProcessingRawArtifactHelper.TryResolveRawArtifactKey(context, Id, DataAcquisitionDeviceName, out var key, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "无法解析 Raw"));

            var bus = context.GetDataBus();
            if (bus == null || !bus.TryGet<NvhMemoryFile>(key, out var file) || file == null)
                return Task.FromResult(ExecutionResult.Failed("无法从数据总线读取 Raw。"));

            if (!DataAcquisitionCardProvider.TryGetDeviceIdByDisplayName(DataAcquisitionDeviceName, out var deviceId))
                return Task.FromResult(ExecutionResult.Failed("采集卡无效。"));

            var chKey = ResolveChannelKey();
            if (!DataProcessingNvhSampleUtil.TryExtractAsDoubleArray(file, DataProcessingRawArtifactHelper.NvhSignalGroupName, chKey, out var samples) ||
                samples.Length == 0)
                return Task.FromResult(ExecutionResult.Failed("无法读取通道样本。"));

            if (!DataProcessingNvhSampleUtil.TryGetWaveformIncrement(file, DataProcessingRawArtifactHelper.NvhSignalGroupName, chKey, out var dt) || dt <= 0)
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

            NvhMemoryFile outFile;
            try
            {
                outFile = NvhMemoryFileCloneHelper.CloneReplacingChannelSamples(
                    file,
                    DataProcessingRawArtifactHelper.NvhSignalGroupName,
                    resolvedChannelName,
                    filtered);
            }
            catch (Exception ex)
            {
                return Task.FromResult(ExecutionResult.Failed("构建滤波后 NVH 数据失败: " + ex.Message, ex));
            }

            bus.PublishRawData(
                producerNodeId: Id,
                artifactName: $"{deviceId}:raw",
                rawData: outFile,
                displayName: $"{DataAcquisitionDeviceName}-Filtered",
                deviceId: deviceId);

            var n = filtered.Length;
            var t = new double[n];
            for (var i = 0; i < n; i++)
                t[i] = i * dt;
            var chart = ChartDisplayPayloadFactory.XYLine(t, filtered, "时间 (s)", "幅值");

            return Task.FromResult(DataProcessingResultPublisher.SuccessWithChart(context, Id, ChartArtifactName, chart, tag: ResultTag));
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
