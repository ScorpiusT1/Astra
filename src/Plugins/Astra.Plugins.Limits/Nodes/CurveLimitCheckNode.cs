using System;
using Astra.Plugins.Limits.Helpers;
using Astra.Core.Nodes.Models;
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
    /// 从 Raw 数据存储读取 NVH 曲线，逐样本检查是否落在闭区间 [下限, 上限] 内。通道为上游「设备/通道」。
    /// </summary>
    public class CurveLimitCheckNode : Node, IHomeTestItemChartEligibleNode
    {
        [JsonIgnore]
        private readonly List<IDesignTimeDataSourceInfo> _upstreamSources = new();

        /// <summary>设计期：与值卡控一致登记标量上游，便于仅从算法等标量侧连线时解析「通道」下拉（运行时仍按 Raw 解析曲线）。</summary>
        [JsonIgnore]
        private readonly List<IDesignTimeScalarOutputProvider> _upstreamScalarProviders = new();

        [JsonIgnore]
        private bool _registrySubscribed;

        [JsonProperty("DataAcquisitionDeviceName", NullValueHandling = NullValueHandling.Ignore)]
        private string? _legacyDataAcquisitionDeviceName;

        [OnDeserialized]
        private void OnDeserializedCallback(StreamingContext ctx)
        {
            MigrateLegacyCurveSelection();
            if (CachedChannelOptions?.Count > 0 && !string.IsNullOrEmpty(Id))
                DesignTimeUpstreamRegistry.CacheChannelOptions(Id, CachedChannelOptions);
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
            if (_registrySubscribed) return;
            _registrySubscribed = true;
            DesignTimeUpstreamRegistry.SourcesChanged += id =>
            {
                if (id == Id)
                {
                    RefreshAndCacheChannelOptions();
                    IntersectCurveSelectionWithOptions();
                    OnPropertyChanged(nameof(CurveChannelOptions));
                }
            };
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

        [Display(Name = "通道", GroupName = "曲线数据", Order = 1,
            Description = "直连数据源时列出通道；仅连算法等标量上游时从标量键括号内解析。未连线时保留缓存或当前已选值。")]
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
            }
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
                RefreshAndCacheChannelOptions();
                IntersectCurveSelectionWithOptions();
                OnPropertyChanged(nameof(CurveChannelOptions));
            }

            if (sourceNode is IDesignTimeDataSourceInfo src && !_upstreamSources.Contains(src))
            {
                _upstreamSources.Add(src);
                DesignTimeUpstreamRegistry.SetSources(Id, _upstreamSources);
                RefreshAndCacheChannelOptions();
                IntersectCurveSelectionWithOptions();
                OnPropertyChanged(nameof(CurveChannelOptions));
            }
        }

        private void IntersectCurveSelectionWithOptions()
        {
            var opts = CurveChannelOptions.ToHashSet(StringComparer.Ordinal);
            if (opts.Count == 0 || string.IsNullOrEmpty(_curveChannelName))
                return;
            if (opts.Contains(_curveChannelName))
                return;
            _curveChannelName = string.Empty;
            OnPropertyChanged(nameof(CurveChannelName));
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
                return;
            }

            if (targetNode?.Id == Id && sourceNode is IDesignTimeScalarOutputProvider sp)
            {
                _upstreamScalarProviders.RemoveAll(p => p.ProviderNodeId == sp.ProviderNodeId);
                DesignTimeUpstreamRegistry.SetScalarUpstreamProviders(Id, _upstreamScalarProviders);
                RefreshAndCacheChannelOptions();
                IntersectCurveSelectionWithOptions();
                OnPropertyChanged(nameof(CurveChannelOptions));
            }

            if (targetNode?.Id == Id && sourceNode is IDesignTimeDataSourceInfo src)
                _upstreamSources.Remove(src);

            DesignTimeUpstreamRegistry.SetSources(Id, _upstreamSources);
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

            if (!LimitCurveArtifactResolver.TryResolveRawArtifactKey(context, Id, deviceForRaw, out var artifact, out var resolveErr))
            {
                return Task.FromResult(ExecutionResult.Failed(resolveErr));
            }

            if (!context.TryGetArtifact<NVHDataBridge.Models.NvhMemoryFile>(artifact, out var file) || file == null)
            {
                return Task.FromResult(ExecutionResult.Failed($"无法从数据总线读取曲线数据: {artifact}"));
            }

            if (!NvhCurveSampleUtil.TryExtractAsDoubleArray(file, LimitCurveArtifactResolver.NvhSignalGroupName, nvhCh, out var samples) || samples.Length == 0)
            {
                return Task.FromResult(ExecutionResult.Failed("曲线样本为空或通道类型不支持"));
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

            var summary = pass
                ? $"曲线卡控通过，样本数={samples.Length}，min={min:F6} max={max:F6}，带 [{lo:F6},{hi:F6}]"
                : $"曲线卡控失败，索引={failIndex}，值={samples[failIndex]:F6}，带 [{lo:F6},{hi:F6}]";

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
                .WithOutput(NodeUiOutputKeys.Summary, summary)
                .WithOutput(NodeUiOutputKeys.HasChartData, true)
                .WithOutput(NodeUiOutputKeys.ChartArtifactKey, artifact);

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
