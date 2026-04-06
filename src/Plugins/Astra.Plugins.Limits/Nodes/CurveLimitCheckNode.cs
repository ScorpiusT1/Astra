using System;
using Astra.Plugins.Limits.Helpers;
using Astra.Core.Nodes.Models;
using Astra.Core.Reporting;
using Astra.UI.Abstractions.Nodes;
using Astra.UI.Abstractions.Attributes;
using Astra.UI.PropertyEditors;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Plugins.Limits.Nodes
{
    /// <summary>
    /// 逐样本检查曲线是否落在闭区间 [下限, 上限] 内。样本可来自上游算法图表（<see cref="Astra.UI.Abstractions.Nodes.ChartDisplayPayload"/>）
    /// 或 Raw（采集/导入等 <see cref="NVHDataBridge.Models.NvhMemoryFile"/>），由 <see cref="CurveSampleSource"/> 控制。
    /// </summary>
    public class CurveLimitCheckNode : Node, IHomeTestItemChartEligibleNode, IReportWhitelistScalarNode, IReportWhitelistCurveNode, IReportWhitelistChartProducerNode
    {
        [JsonIgnore]
        private readonly List<IDesignTimeDataSourceInfo> _upstreamSources = new();

        /// <summary>设计期：与值卡控一致登记标量上游，便于仅从算法等标量侧连线时解析「通道」下拉。</summary>
        [JsonIgnore]
        private readonly List<IDesignTimeScalarOutputProvider> _upstreamScalarProviders = new();

        [JsonIgnore]
        private bool _registrySubscribed;

        [JsonIgnore]
        private string? _autoCurveChannelNameSuffix;

        [JsonProperty("DataAcquisitionDeviceName", NullValueHandling = NullValueHandling.Ignore)]
        private string? _legacyDataAcquisitionDeviceName;

        [OnDeserialized]
        private void OnDeserializedCallback(StreamingContext ctx)
        {
            MigrateLegacyCurveSelection();
            if (CachedChannelOptions?.Count > 0 && !string.IsNullOrEmpty(Id))
                DesignTimeUpstreamRegistry.CacheChannelOptions(Id, CachedChannelOptions);
            SyncDisplayNameFromCurveChannel();
        }

        private void MigrateLegacyCurveSelection()
        {
            if (string.IsNullOrEmpty(_legacyDataAcquisitionDeviceName))
                return;
            var dev = _legacyDataAcquisitionDeviceName.Trim();
            if (string.IsNullOrEmpty(dev))
                return;

            var ch = _curveChannelName?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(ch) ||
                string.Equals(ch, LimitsDesignTimeOptions.UseFirstChannelInGroupLabel, StringComparison.Ordinal))
                _curveChannelName = dev;
            else if (ch.IndexOf('/') < 0)
                _curveChannelName = $"{dev}/{ch}";

            _legacyDataAcquisitionDeviceName = null;
        }

        private void EnsureRegistrySubscription()
        {
            if (_registrySubscribed || string.IsNullOrEmpty(Id)) return;
            _registrySubscribed = true;
            var wr = new WeakReference<object>(this);
            DesignTimeUpstreamRegistry.RegisterOwnSourcesChanged(Id, this, changedId =>
            {
                if (!wr.TryGetTarget(out var t) || t is not CurveLimitCheckNode self) return;
                if (changedId != self.Id) return;
                self.RefreshAndCacheChannelOptions();
                self.OnPropertyChanged(nameof(CurveChannelOptions));
            });
            DesignTimeUpstreamRegistry.RegisterUpstreamChannelOptionsListener(this, producerId =>
            {
                if (!wr.TryGetTarget(out var t) || t is not CurveLimitCheckNode self) return;
                if (string.IsNullOrEmpty(self.Id) ||
                    !DesignTimeUpstreamRegistry.IsDownstreamAffectedByProducerChain(self.Id, producerId))
                    return;
                self.RefreshAndCacheChannelOptions();
                self.IntersectCurveSelectionWithOptions();
                self.OnPropertyChanged(nameof(CurveChannelOptions));
            });
            DesignTimeUpstreamRegistry.RegisterDeviceChannelOptionsListener(this, deviceName =>
            {
                if (!wr.TryGetTarget(out var t) || t is not CurveLimitCheckNode self) return;
                if (!DesignTimeUpstreamRegistry.RegisteredUpstreamExposesDevice(self.Id, deviceName)) return;
                self.RefreshAndCacheChannelOptions();
                self.IntersectCurveSelectionWithOptions();
                self.OnPropertyChanged(nameof(CurveChannelOptions));
            });
        }

        public override void OnRemovedFromWorkflow()
        {
            if (_registrySubscribed)
            {
                DesignTimeUpstreamRegistry.UnregisterDesignTimeListeners(this);
                _registrySubscribed = false;
            }

            base.OnRemovedFromWorkflow();
        }

        private void RefreshAndCacheChannelOptions()
        {
            LimitCurveChannelOptionsHelper.RefreshCachedChannelOptions(Id, CachedChannelOptions);
        }

        private string _curveChannelName = string.Empty;

        public List<string> CachedChannelOptions { get; set; } = new();

        [JsonIgnore]
        public IEnumerable<string> CurveChannelOptions
        {
            get
            {
                EnsureRegistrySubscription();
                return LimitCurveChannelOptionsHelper.BuildOptions(Id, CachedChannelOptions, _curveChannelName);
            }
        }

        [Display(Name = "曲线数据来源", GroupName = "曲线数据", Order = 0,
            Description = "自动：上游有图表工件则卡算法曲线，否则卡 Raw。仅原始数据/仅上游图表为强制模式。")]
        public CurveLimitSampleSource CurveSampleSource { get; set; } = CurveLimitSampleSource.Auto;

        [Display(Name = "通道", GroupName = "曲线数据", Order = 1,
            Description = "与算法节点一致：连线后由上游驱动通道列表；删除/断开上游后仍保留已选与缓存；上游通道或设备配置变化时再剔除无效项。")]
        [Editor(typeof(ComboBoxPropertyEditor))]
        [ItemsSource(nameof(CurveChannelOptions), DisplayMemberPath = ".")]
        public string CurveChannelName
        {
            get => string.IsNullOrEmpty(_curveChannelName)
                ? LimitsDesignTimeOptions.UnselectedLabel
                : _curveChannelName;
            set
            {
                var v = value ?? string.Empty;
                if (string.Equals(v, LimitsDesignTimeOptions.UnselectedLabel, StringComparison.Ordinal))
                    v = string.Empty;
                if (string.Equals(_curveChannelName, v, StringComparison.Ordinal))
                    return;
                _curveChannelName = v;
                OnPropertyChanged();
                SyncDisplayNameFromCurveChannel();
            }
        }

        private void SyncDisplayNameFromCurveChannel()
        {
            var frag = NodeNameChannelSuffixHelper.BuildSingleSelectionSuffix(_curveChannelName);
            ApplyAutoChannelSuffixToDisplayName(ref _autoCurveChannelNameSuffix, frag);
        }

        public override void OnConnectionAttached(Edge edge, Node? sourceNode, Node? targetNode)
        {
            base.OnConnectionAttached(edge, sourceNode, targetNode);
            if (targetNode?.Id != Id)
                return;

            EnsureRegistrySubscription();

            if (sourceNode is IDesignTimeScalarOutputProvider sp &&
                !_upstreamScalarProviders.Any(p => p.ProviderNodeId == sp.ProviderNodeId))
            {
                _upstreamScalarProviders.Add(sp);
                DesignTimeUpstreamRegistry.SetScalarUpstreamProviders(Id, _upstreamScalarProviders);
            }

            if (sourceNode is IDesignTimeDataSourceInfo src && !_upstreamSources.Contains(src))
            {
                _upstreamSources.Add(src);
                DesignTimeUpstreamRegistry.SetSources(Id, _upstreamSources);
            }
        }

        private void IntersectCurveSelectionWithOptions()
        {
            var available = LimitCurveChannelOptionsHelper.GetLiveQualifiedCurveChannels(Id)
                .ToHashSet(StringComparer.Ordinal);
            if (available.Count == 0 || string.IsNullOrEmpty(_curveChannelName))
                return;
            if (available.Contains(_curveChannelName))
                return;
            _curveChannelName = string.Empty;
            OnPropertyChanged(nameof(CurveChannelName));
            SyncDisplayNameFromCurveChannel();
        }

        public override void OnConnectionDetached(Edge? edge, Node? sourceNode, Node? targetNode)
        {
            base.OnConnectionDetached(edge, sourceNode, targetNode);
            if (edge == null)
            {
                _upstreamSources.Clear();
                _upstreamScalarProviders.Clear();
                DesignTimeUpstreamRegistry.SetSources(Id, _upstreamSources);
                DesignTimeUpstreamRegistry.SetScalarUpstreamProviders(Id, _upstreamScalarProviders);
                RefreshAndCacheChannelOptions();
                IntersectCurveSelectionWithOptions();
                OnPropertyChanged(nameof(CurveChannelOptions));
                return;
            }

            if (targetNode?.Id != Id)
                return;

            LimitUpstreamDetachHelper.RemoveUpstreamForDetachedEdge(edge, _upstreamSources, _upstreamScalarProviders);
            DesignTimeUpstreamRegistry.SetScalarUpstreamProviders(Id, _upstreamScalarProviders);
            DesignTimeUpstreamRegistry.SetSources(Id, _upstreamSources);
            RefreshAndCacheChannelOptions();
            IntersectCurveSelectionWithOptions();
            OnPropertyChanged(nameof(CurveChannelOptions));
        }

        [Display(Name = "合格下限（逐点）", GroupName = "曲线合格带", Order = 1, Description = "每个采样点不得低于此值")]
        public double LowerLimit { get; set; }

        [Display(Name = "合格上限（逐点）", GroupName = "曲线合格带", Order = 2, Description = "每个采样点不得高于此值")]
        public double UpperLimit { get; set; }

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var log = context.CreateExecutionLogger($"曲线卡控:{Name}");
            if (!LimitNodeShared.TryResolveCurveSelection(_curveChannelName, out var deviceForRaw, out var nvhCh, out var selErr))
            {
                return Task.FromResult(ExecutionResult.Failed(selErr ?? "请选择通道"));
            }

            double[] samples;
            var samplesFromUpstreamChart = false;
            string? rawArtifactKeyForChartOutputs;

            switch (CurveSampleSource)
            {
                case CurveLimitSampleSource.ChartOnly:
                    if (!LimitNodeShared.TryGetCurveLimitSamplesFromUpstreamChart(
                            context,
                            nvhCh,
                            out var chartSamples,
                            out var chartErr))
                    {
                        return Task.FromResult(ExecutionResult.Failed(chartErr ?? "无法读取上游图表曲线"));
                    }

                    samples = chartSamples!;
                    samplesFromUpstreamChart = true;
                    rawArtifactKeyForChartOutputs = null;
                    break;

                case CurveLimitSampleSource.RawOnly:
                    if (!LimitCurveArtifactResolver.TryResolveRawArtifactKey(context, Id, deviceForRaw, out var rawKey, out var rawOnlyErr))
                    {
                        return Task.FromResult(ExecutionResult.Failed(rawOnlyErr));
                    }

                    if (!context.TryGetArtifact<NVHDataBridge.Models.NvhMemoryFile>(rawKey, out var rawOnlyFile) || rawOnlyFile == null)
                    {
                        return Task.FromResult(ExecutionResult.Failed($"无法从数据总线读取曲线数据: {rawKey}"));
                    }

                    if (!NvhCurveSampleUtil.TryExtractAsDoubleArray(
                            rawOnlyFile,
                            LimitCurveArtifactResolver.NvhSignalGroupName,
                            nvhCh,
                            out samples) ||
                        samples.Length == 0)
                    {
                        return Task.FromResult(ExecutionResult.Failed("曲线样本为空或通道类型不支持"));
                    }

                    rawArtifactKeyForChartOutputs = rawKey;
                    break;

                default:
                    if (LimitNodeShared.TryResolveUpstreamCurveSamplesForAuto(context, nvhCh, out var autoChart) &&
                        autoChart is { Length: > 0 })
                    {
                        samples = autoChart;
                        samplesFromUpstreamChart = true;
                        rawArtifactKeyForChartOutputs = null;
                    }
                    else
                    {
                        if (!LimitCurveArtifactResolver.TryResolveRawArtifactKey(context, Id, deviceForRaw, out var autoRawKey, out var autoRawErr))
                        {
                            return Task.FromResult(ExecutionResult.Failed(autoRawErr));
                        }

                        if (!context.TryGetArtifact<NVHDataBridge.Models.NvhMemoryFile>(autoRawKey, out var autoRawFile) || autoRawFile == null)
                        {
                            return Task.FromResult(ExecutionResult.Failed($"无法从数据总线读取曲线数据: {autoRawKey}"));
                        }

                        if (!NvhCurveSampleUtil.TryExtractAsDoubleArray(
                                autoRawFile,
                                LimitCurveArtifactResolver.NvhSignalGroupName,
                                nvhCh,
                                out samples) ||
                            samples.Length == 0)
                        {
                            return Task.FromResult(ExecutionResult.Failed("曲线样本为空或通道类型不支持"));
                        }

                        rawArtifactKeyForChartOutputs = autoRawKey;
                    }

                    break;
            }

            var lo = LowerLimit;
            var hi = UpperLimit;
            LimitNodeShared.NormalizeLimits(ref lo, ref hi);

            var failIndex = -1;
            for (var i = 0; i < samples.Length; i++)
            {
                var v = samples[i];
                if (v < lo || v > hi)
                {
                    failIndex = i;
                    break;
                }
            }

            var pass = failIndex < 0;
            var min = samples[0];
            var max = samples[0];
            foreach (var v in samples)
            {
                if (v < min)
                {
                    min = v;
                }

                if (v > max)
                {
                    max = v;
                }
            }

            var sourceHint = samplesFromUpstreamChart ? "，来源=上游图表" : "，来源=Raw";
            var summary = pass
                ? $"曲线卡控通过，样本数={samples.Length}，min={min:F6} max={max:F6}，带 [{lo:F6},{hi:F6}]{sourceHint}"
                : $"曲线卡控失败，索引={failIndex}，值={samples[failIndex]:F6}，带 [{lo:F6},{hi:F6}]{sourceHint}";

            ExecutionResult result;
            if (pass)
            {
                result = ExecutionResult.Successful(summary);
            }
            else
            {
                result = ExecutionResult.Failed(summary);
            }

            result = result
                .WithOutput(NodeUiOutputKeys.ActualValue, pass ? max : samples[failIndex])
                .WithOutput(NodeUiOutputKeys.LowerLimit, lo)
                .WithOutput(NodeUiOutputKeys.UpperLimit, hi)
                .WithOutput(NodeUiOutputKeys.CurveCheckPass, pass)
                .WithOutput(NodeUiOutputKeys.Summary, summary);

            result = LimitNodeShared.WithNvhCurveChartOutputs(result, context, Id, true, rawArtifactKeyForChartOutputs, nvhCh, IncludeInTestReport);

            if (!pass)
            {
                result = result.WithOutput(NodeUiOutputKeys.CurveFailDetail, $"index={failIndex}, value={samples[failIndex]:G}");
            }

            if (pass)
            {
                log.Info(summary);
            }
            else
            {
                log.Warn(summary);
            }

            return Task.FromResult(result);
        }
    }
}
