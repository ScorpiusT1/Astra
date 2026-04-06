using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Astra.Core.Nodes.Models;
using Astra.Core.Reporting;
using Astra.Plugins.Limits.Enums;
using Astra.Plugins.Limits.Helpers;
using Astra.UI.Abstractions.Attributes;
using Astra.UI.Abstractions.Nodes;
using Astra.UI.PropertyEditors;
using Newtonsoft.Json;

namespace Astra.Plugins.Limits.Nodes
{
    /// <summary>
    /// 从 Raw 或上游算法图表读取曲线，按可选 X 轴区间筛选后对 Y 计算单一统计量（均值、最大/最小、峰值、峰峰值、RMS、峭度等），再与上下限比较；标量结果写入测试报告。
    /// 主页可显示波形但不纳入报告图表、不绘制规格上下限参考线。
    /// </summary>
    public class CurveScalarStatisticLimitCheckNode : Node,
        IHomeTestItemChartEligibleNode,
        IReportWhitelistScalarNode
    {
        [JsonIgnore]
        private readonly List<IDesignTimeDataSourceInfo> _upstreamSources = new();

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
            {
                DesignTimeUpstreamRegistry.CacheChannelOptions(Id, CachedChannelOptions);
            }

            SyncDisplayNameFromCurveChannel();
        }

        private void MigrateLegacyCurveSelection()
        {
            if (string.IsNullOrEmpty(_legacyDataAcquisitionDeviceName))
            {
                return;
            }

            var dev = _legacyDataAcquisitionDeviceName.Trim();
            if (string.IsNullOrEmpty(dev))
            {
                return;
            }

            var ch = _curveChannelName?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(ch) ||
                string.Equals(ch, LimitsDesignTimeOptions.UseFirstChannelInGroupLabel, StringComparison.Ordinal))
            {
                _curveChannelName = dev;
            }
            else if (ch.IndexOf('/') < 0)
            {
                _curveChannelName = $"{dev}/{ch}";
            }

            _legacyDataAcquisitionDeviceName = null;
        }

        private void EnsureRegistrySubscription()
        {
            if (_registrySubscribed || string.IsNullOrEmpty(Id))
            {
                return;
            }

            _registrySubscribed = true;
            var wr = new WeakReference<object>(this);
            DesignTimeUpstreamRegistry.RegisterOwnSourcesChanged(Id, this, changedId =>
            {
                if (!wr.TryGetTarget(out var t) || t is not CurveScalarStatisticLimitCheckNode self)
                {
                    return;
                }

                if (changedId != self.Id)
                {
                    return;
                }

                self.RefreshAndCacheChannelOptions();
                self.OnPropertyChanged(nameof(CurveChannelOptions));
            });
            DesignTimeUpstreamRegistry.RegisterUpstreamChannelOptionsListener(this, producerId =>
            {
                if (!wr.TryGetTarget(out var t) || t is not CurveScalarStatisticLimitCheckNode self)
                {
                    return;
                }

                if (string.IsNullOrEmpty(self.Id) ||
                    !DesignTimeUpstreamRegistry.IsDownstreamAffectedByProducerChain(self.Id, producerId))
                {
                    return;
                }

                self.RefreshAndCacheChannelOptions();
                self.IntersectCurveSelectionWithOptions();
                self.OnPropertyChanged(nameof(CurveChannelOptions));
            });
            DesignTimeUpstreamRegistry.RegisterDeviceChannelOptionsListener(this, deviceName =>
            {
                if (!wr.TryGetTarget(out var t) || t is not CurveScalarStatisticLimitCheckNode self)
                {
                    return;
                }

                if (!DesignTimeUpstreamRegistry.RegisteredUpstreamExposesDevice(self.Id, deviceName))
                {
                    return;
                }

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
            Description = "自动：上游有图表工件则用算法曲线，否则用 Raw。仅原始数据/仅上游图表为强制模式。")]
        public CurveLimitSampleSource CurveSampleSource { get; set; } = CurveLimitSampleSource.Auto;

        [Display(Name = "通道", GroupName = "曲线数据", Order = 1,
            Description = "与算法节点一致：连线后由上游驱动通道列表。")]
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
                {
                    v = string.Empty;
                }

                if (string.Equals(_curveChannelName, v, StringComparison.Ordinal))
                {
                    return;
                }

                _curveChannelName = v;
                OnPropertyChanged();
                SyncDisplayNameFromCurveChannel();
            }
        }

        [Display(Name = "统计指标", GroupName = "统计与筛选", Order = 0, Description = "仅计算并判定一种指标")]
        public CurveScalarMetricKind StatisticMetric { get; set; } = CurveScalarMetricKind.Mean;

        [Display(Name = "启用 X 轴区间筛选", GroupName = "统计与筛选", Order = 1,
            Description = "开启后仅保留 X 落在 [X 下限, X 上限]（含）内的点再计算统计量；时域无显式 X 时按采样间隔生成横轴。")]
        public bool EnableXAxisRangeFilter { get; set; }

        [Display(Name = "X 轴下限", GroupName = "统计与筛选", Order = 2, Description = "与横轴物理量同单位（如 s、Hz）")]
        public double XAxisRangeMin { get; set; }

        [Display(Name = "X 轴上限", GroupName = "统计与筛选", Order = 3)]
        public double XAxisRangeMax { get; set; }

        private void SyncDisplayNameFromCurveChannel()
        {
            var frag = NodeNameChannelSuffixHelper.BuildSingleSelectionSuffix(_curveChannelName);
            ApplyAutoChannelSuffixToDisplayName(ref _autoCurveChannelNameSuffix, frag);
        }

        public override void OnConnectionAttached(Edge edge, Node? sourceNode, Node? targetNode)
        {
            base.OnConnectionAttached(edge, sourceNode, targetNode);
            if (targetNode?.Id != Id)
            {
                return;
            }

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
            {
                return;
            }

            if (available.Contains(_curveChannelName))
            {
                return;
            }

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
            {
                return;
            }

            LimitUpstreamDetachHelper.RemoveUpstreamForDetachedEdge(edge, _upstreamSources, _upstreamScalarProviders);
            DesignTimeUpstreamRegistry.SetScalarUpstreamProviders(Id, _upstreamScalarProviders);
            DesignTimeUpstreamRegistry.SetSources(Id, _upstreamSources);
            RefreshAndCacheChannelOptions();
            IntersectCurveSelectionWithOptions();
            OnPropertyChanged(nameof(CurveChannelOptions));
        }

        [Display(Name = "合格下限", GroupName = "合格范围", Order = 1, Description = "统计量不低于此值（含）")]
        public double LowerLimit { get; set; }

        [Display(Name = "合格上限", GroupName = "合格范围", Order = 2, Description = "统计量不高于此值（含）")]
        public double UpperLimit { get; set; }

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var log = context.CreateExecutionLogger($"曲线统计卡控:{Name}");
            if (!LimitNodeShared.TryResolveCurveSelection(_curveChannelName, out var deviceForRaw, out var nvhCh, out var selErr))
            {
                return Task.FromResult(ExecutionResult.Failed(selErr ?? "请选择通道"));
            }

            if (!LimitNodeShared.TryGetCurveXySeries(
                    context,
                    Id,
                    CurveSampleSource,
                    deviceForRaw,
                    nvhCh,
                    out var xSeries,
                    out var ySeries,
                    out var rawArtifactKeyForChartOutputs,
                    out var fromChart,
                    out var seriesErr))
            {
                return Task.FromResult(ExecutionResult.Failed(seriesErr ?? "无法读取曲线数据"));
            }

            if (!CurveScalarStatistics.TryFilterByXInclusive(
                    xSeries,
                    ySeries,
                    EnableXAxisRangeFilter,
                    XAxisRangeMin,
                    XAxisRangeMax,
                    out var yWork,
                    out var filterErr))
            {
                return Task.FromResult(ExecutionResult.Failed(filterErr ?? "X 轴筛选失败"));
            }

            if (!CurveScalarStatistics.TryCompute(StatisticMetric, yWork, out var statValue, out var computeErr))
            {
                return Task.FromResult(ExecutionResult.Failed(computeErr ?? "统计计算失败"));
            }

            var lo = LowerLimit;
            var hi = UpperLimit;
            LimitNodeShared.NormalizeLimits(ref lo, ref hi);
            var pass = statValue >= lo && statValue <= hi;

            var metricLabel = CurveScalarStatistics.GetMetricDisplayName(StatisticMetric);
            var sourceHint = fromChart ? "来源=上游图表" : "来源=Raw";
            var filterHint = EnableXAxisRangeFilter
                ? $"，X∈[{Math.Min(XAxisRangeMin, XAxisRangeMax):G},{Math.Max(XAxisRangeMin, XAxisRangeMax):G}]，筛后点数={yWork.Length}/{ySeries.Length}"
                : $"，点数={ySeries.Length}";

            var summary = pass
                ? $"{metricLabel} 卡控通过，统计值={statValue:F6}，规格 [{lo:F6},{hi:F6}]，{sourceHint}{filterHint}"
                : $"{metricLabel} 卡控失败，统计值={statValue:F6}，规格 [{lo:F6},{hi:F6}]，{sourceHint}{filterHint}";

            ExecutionResult result = pass
                ? ExecutionResult.Successful(summary)
                : ExecutionResult.Failed(summary);

            result = result
                .WithOutput(NodeUiOutputKeys.ActualValue, statValue)
                .WithOutput(NodeUiOutputKeys.LowerLimit, lo)
                .WithOutput(NodeUiOutputKeys.UpperLimit, hi)
                .WithOutput(NodeUiOutputKeys.ValueCheckPass, pass)
                .WithOutput(NodeUiOutputKeys.Summary, summary)
                .WithOutput(NodeUiOutputKeys.ChartSuppressHorizontalLimits, true);

            result = LimitNodeShared.WithNvhCurveChartOutputs(
                result,
                context,
                Id,
                true,
                rawArtifactKeyForChartOutputs,
                nvhCh,
                includeInTestReport: false,
                mergeHorizontalLimitsForChart: false);

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
