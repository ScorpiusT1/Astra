using Astra.Plugins.Limits.Helpers;
using Astra.Core.Nodes.Models;
using Astra.Core.Reporting;
using Astra.UI.Abstractions.Nodes;
using Astra.UI.Abstractions.Attributes;
using Astra.UI.PropertyEditors;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Plugins.Limits.Nodes
{
    /// <summary>
    /// 从上游节点输出或工作流全局变量读取标量并与上下限比较；可选按通道在主页显示曲线（不参与判定）。
    /// </summary>
    public class ValueLimitCheckNode : Node, IHomeTestItemChartEligibleNode, IReportWhitelistScalarNode, IReportWhitelistChartProducerNode
    {
        [JsonIgnore]
        private readonly List<IDesignTimeDataSourceInfo> _upstreamSources = new();

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
            ApplyDefaultMeasuredValueKeyIfNeeded();
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
                    OnPropertyChanged(nameof(MeasuredValueKeyOptions));
                    ApplyDefaultMeasuredValueKeyIfNeeded();
                }
            };
        }

        private void RefreshAndCacheChannelOptions()
        {
            LimitCurveChannelOptionsHelper.RefreshCachedChannelOptions(Id, CachedChannelOptions);
        }

        private List<string> GetFullMeasuredScalarKeyOptions()
        {
            EnsureRegistrySubscription();
            return LimitScalarKeyFilter.FilterByCurveChannel(
                DesignTimeUpstreamRegistry.GetScalarInputKeyOptions(Id),
                _curveChannelName).ToList();
        }

        [JsonIgnore]
        public IEnumerable<string> MeasuredValueKeyOptions
        {
            get => LimitScalarKeyUiFormatter.ToDisplayOptions(GetFullMeasuredScalarKeyOptions());
        }

        /// <summary>序列化与运行时使用完整限定键；属性面板仅展示无上游节点 Id 前缀的文案。</summary>
        [JsonProperty("GlobalVariableKey")]
        private string _globalVariableKey = string.Empty;

        [Display(Name = "实测值变量名", GroupName = "数值", Order = 1,
            Description = "连线后下拉与「通道」一致：选通道后仅显示该通道相关逻辑名（不显示上游节点 Id 与 Scalar. 前缀）；多键时默认第一项。可手动改键名或填全局变量名。")]
        [Editor(typeof(ComboBoxPropertyEditor))]
        [ItemsSource(nameof(MeasuredValueKeyOptions), DisplayMemberPath = ".", IsEditable = true)]
        [JsonIgnore]
        public string GlobalVariableKey
        {
            get => LimitScalarKeyUiFormatter.ToDisplay(_globalVariableKey);
            set
            {
                var resolved = LimitScalarKeyUiFormatter.ResolveToStoredKey(value ?? string.Empty, GetFullMeasuredScalarKeyOptions());
                if (string.Equals(_globalVariableKey, resolved, StringComparison.Ordinal))
                    return;
                _globalVariableKey = resolved;
                OnPropertyChanged();
            }
        }

        [Display(Name = "合格下限", GroupName = "数值", Order = 2, Description = "实测值不低于此值（含）")]
        public double LowerLimit { get; set; }

        [Display(Name = "合格上限", GroupName = "数值", Order = 3, Description = "实测值不高于此值（含）")]
        public double UpperLimit { get; set; }

        [Display(Name = "在主页同时显示曲线", GroupName = "主页曲线", Order = 1, Description = "开启后需选择「设备/通道」；仅展示，不参与合格判定")]
        public bool AssociateCurveForDisplay { get; set; }

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

        [Display(Name = "通道", GroupName = "主页曲线", Order = 2, Description = "直连采集数据源时列出其通道；仅连算法等标量上游时从标量键括号内标签解析。未连线时保留缓存或当前已选值")]
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
                OnPropertyChanged(nameof(MeasuredValueKeyOptions));
                ApplyDefaultMeasuredValueKeyIfNeeded();
            }
        }

        /// <summary>当前通道下可选标量键变化时：若为空或未在列表中则默认选第一项。</summary>
        private void ApplyDefaultMeasuredValueKeyIfNeeded()
        {
            var fullOpts = GetFullMeasuredScalarKeyOptions();
            if (fullOpts.Count == 0)
                return;
            var cur = _globalVariableKey?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(cur) && fullOpts.Contains(cur, StringComparer.Ordinal))
                return;
            _globalVariableKey = fullOpts[0];
            OnPropertyChanged(nameof(GlobalVariableKey));
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
                OnPropertyChanged(nameof(MeasuredValueKeyOptions));
                ApplyDefaultMeasuredValueKeyIfNeeded();
            }

            if (sourceNode is IDesignTimeDataSourceInfo src && !_upstreamSources.Contains(src))
            {
                _upstreamSources.Add(src);
                DesignTimeUpstreamRegistry.SetSources(Id, _upstreamSources);
                RefreshAndCacheChannelOptions();
                IntersectCurveSelectionWithOptions();
                OnPropertyChanged(nameof(CurveChannelOptions));
                ApplyDefaultMeasuredValueKeyIfNeeded();
            }
        }

        private void IntersectCurveSelectionWithOptions()
        {
            var opts = CurveChannelOptions.ToHashSet(System.StringComparer.Ordinal);
            if (opts.Count == 0 || string.IsNullOrEmpty(_curveChannelName))
                return;
            if (opts.Contains(_curveChannelName))
                return;
            _curveChannelName = string.Empty;
            OnPropertyChanged(nameof(CurveChannelName));
            OnPropertyChanged(nameof(MeasuredValueKeyOptions));
            ApplyDefaultMeasuredValueKeyIfNeeded();
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
                OnPropertyChanged(nameof(MeasuredValueKeyOptions));
                ApplyDefaultMeasuredValueKeyIfNeeded();
            }

            if (targetNode?.Id == Id && sourceNode is IDesignTimeDataSourceInfo src)
                _upstreamSources.Remove(src);

            DesignTimeUpstreamRegistry.SetSources(Id, _upstreamSources);
        }

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var log = context.CreateExecutionLogger($"值卡控:{Name}");
            var key = _globalVariableKey?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(key))
            {
                return Task.FromResult(ExecutionResult.Failed("请填写实测值变量名或选择上游标量键"));
            }

            if (!LimitNodeShared.TryResolveMeasuredScalar(context, key, out var raw, out var resolveErr))
            {
                return Task.FromResult(ExecutionResult.Failed(resolveErr ?? "无法解析实测值"));
            }

            if (!LimitNodeShared.TryConvertToDouble(raw, out var actual))
            {
                return Task.FromResult(ExecutionResult.Failed("实测值无法转换为数值"));
            }

            var lo = LowerLimit;
            var hi = UpperLimit;
            LimitNodeShared.NormalizeLimits(ref lo, ref hi);
            var pass = actual >= lo && actual <= hi;
            var summary = pass
                ? $"值卡控通过，实测={actual:F6} [{lo:F6},{hi:F6}]"
                : $"值卡控失败，实测={actual:F6}，规格 [{lo:F6},{hi:F6}]";

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
                .WithOutput(NodeUiOutputKeys.ActualValue, actual)
                .WithOutput(NodeUiOutputKeys.LowerLimit, lo)
                .WithOutput(NodeUiOutputKeys.UpperLimit, hi)
                .WithOutput(NodeUiOutputKeys.ValueCheckPass, pass)
                .WithOutput(NodeUiOutputKeys.Summary, summary);

            string? chartKey = null;
            string? nvhChForChart = null;
            if (AssociateCurveForDisplay &&
                LimitNodeShared.TryResolveCurveSelection(_curveChannelName, out var dev, out nvhChForChart, out _) &&
                LimitCurveArtifactResolver.TryResolveRawArtifactKey(context, Id, dev, out var art, out _))
            {
                chartKey = art;
            }

            result = LimitNodeShared.WithOptionalChartDisplay(
                result,
                context,
                Id,
                AssociateCurveForDisplay,
                chartKey,
                nvhChForChart);

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
