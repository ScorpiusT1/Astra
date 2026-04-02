using Astra.Plugins.Limits.Enums;
using Astra.Plugins.Limits.Helpers;
using Astra.Core.Nodes.Models;
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
    /// 同时或分别启用值卡控与曲线卡控；曲线按上游「设备/通道」解析 Raw。
    /// </summary>
    public class ValueAndCurveLimitCheckNode : Node, IHomeTestItemChartEligibleNode
    {
        [JsonIgnore]
        private readonly List<IDesignTimeDataSourceInfo> _upstreamSources = new();

        [JsonIgnore]
        private readonly List<IDesignTimeScalarOutputProvider> _upstreamScalarProviders = new();

        [JsonIgnore]
        private bool _registrySubscribed;

        [JsonProperty("DataAcquisitionDeviceName", NullValueHandling = NullValueHandling.Ignore)]
        private string? _legacyDataAcquisitionDeviceName;

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

        [Display(Name = "卡控方式", GroupName = "工作模式", Order = 1, Description = "选择要执行的检查类型")]
        public LimitCheckMode CheckMode { get; set; } = LimitCheckMode.Both;

        [JsonProperty("EnableValueValidation", DefaultValueHandling = DefaultValueHandling.Ignore)]
        private bool? _legacyEnableValue;

        [JsonProperty("EnableCurveValidation", DefaultValueHandling = DefaultValueHandling.Ignore)]
        private bool? _legacyEnableCurve;

        [OnDeserialized]
        private void OnDeserializedCallback(StreamingContext context)
        {
            if (_legacyEnableValue is not null || _legacyEnableCurve is not null)
            {
                var v = _legacyEnableValue ?? true;
                var c = _legacyEnableCurve ?? true;
                CheckMode = (v, c) switch
                {
                    (true, false) => LimitCheckMode.ValueOnly,
                    (false, true) => LimitCheckMode.CurveOnly,
                    (true, true) => LimitCheckMode.Both,
                    _ => LimitCheckMode.Both,
                };
                _legacyEnableValue = null;
                _legacyEnableCurve = null;
            }

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

        [JsonIgnore]
        public IEnumerable<string> MeasuredValueKeyOptions
        {
            get => LimitScalarKeyUiFormatter.ToDisplayOptions(GetFullMeasuredScalarKeyOptions());
        }

        /// <summary>序列化与运行时使用完整限定键；属性面板仅展示无上游节点 Id 与 Scalar. 前缀的文案。</summary>
        [JsonProperty("GlobalVariableKey")]
        private string _globalVariableKey = string.Empty;

        [Display(Name = "实测值变量名", GroupName = "数值", Order = 1,
            Description = "与「曲线数据」通道一致：选通道后仅显示该通道相关逻辑名（不显示上游节点 Id 与 Scalar. 前缀）；多键时默认第一项。可手动修改或填全局变量名。")]
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

        [Display(Name = "数值合格下限", GroupName = "数值", Order = 2)]
        public double ValueLowerLimit { get; set; }

        [Display(Name = "数值合格上限", GroupName = "数值", Order = 3)]
        public double ValueUpperLimit { get; set; }

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

        [Display(Name = "通道", GroupName = "曲线数据", Order = 1, Description = "直连数据源时列出通道；仅连标量上游时从标量键括号内解析。未连线时保留缓存或当前已选值")]
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

        [Display(Name = "曲线合格下限（逐点）", GroupName = "曲线合格带", Order = 1)]
        public double CurveLowerLimit { get; set; }

        [Display(Name = "曲线合格上限（逐点）", GroupName = "曲线合格带", Order = 2)]
        public double CurveUpperLimit { get; set; }

        [Display(Name = "未判曲线时仍显示曲线", GroupName = "主页曲线", Order = 1, Description = "仅做数值检查时，仍按所选通道显示曲线")]
        public bool ShowChartWithoutCurveValidation { get; set; } = true;

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var log = context.CreateExecutionLogger($"值与曲线卡控:{Name}");
            var enableValue = CheckMode == LimitCheckMode.ValueOnly || CheckMode == LimitCheckMode.Both;
            var enableCurve = CheckMode == LimitCheckMode.CurveOnly || CheckMode == LimitCheckMode.Both;

            if (!enableValue && !enableCurve)
            {
                return Task.FromResult(ExecutionResult.Failed("请选择有效的卡控方式"));
            }

            var valuePass = true;
            double valueActual = 0;
            var vLo = ValueLowerLimit;
            var vHi = ValueUpperLimit;
            LimitNodeShared.NormalizeLimits(ref vLo, ref vHi);

            if (enableValue)
            {
                var gk = _globalVariableKey?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(gk))
                {
                    return Task.FromResult(ExecutionResult.Failed("已选择数值检查，请填写实测值变量名或选择上游标量键"));
                }

                if (!LimitNodeShared.TryResolveMeasuredScalar(context, gk, out var raw, out var resolveErr))
                {
                    return Task.FromResult(ExecutionResult.Failed(resolveErr ?? "无法解析实测值"));
                }

                if (!LimitNodeShared.TryConvertToDouble(raw, out valueActual))
                {
                    return Task.FromResult(ExecutionResult.Failed("实测值无法转换为数值"));
                }

                valuePass = valueActual >= vLo && valueActual <= vHi;
            }

            var curvePass = true;
            var curveFailDetail = string.Empty;
            double curveRepresentative = 0;
            var cLo = CurveLowerLimit;
            var cHi = CurveUpperLimit;
            LimitNodeShared.NormalizeLimits(ref cLo, ref cHi);

            var needCurveArtifact = enableCurve
                || (ShowChartWithoutCurveValidation && CheckMode == LimitCheckMode.ValueOnly);

            string? resolvedArtifact = null;
            string? nvhChForCurve = null;
            if (needCurveArtifact)
            {
                if (!LimitNodeShared.TryResolveCurveSelection(_curveChannelName, out var dev, out nvhChForCurve, out var selErr))
                {
                    if (enableCurve)
                        return Task.FromResult(ExecutionResult.Failed(selErr ?? "请选择曲线通道"));
                }
                else
                {
                    if (!LimitCurveArtifactResolver.TryResolveRawArtifactKey(context, Id, dev, out var art, out var err))
                    {
                        if (enableCurve)
                            return Task.FromResult(ExecutionResult.Failed(err));
                    }
                    else
                    {
                        resolvedArtifact = art;
                    }
                }
            }

            if (enableCurve)
            {
                if (string.IsNullOrEmpty(resolvedArtifact))
                {
                    return Task.FromResult(ExecutionResult.Failed("无法解析曲线 Raw 键"));
                }

                if (!context.TryGetArtifact<NVHDataBridge.Models.NvhMemoryFile>(resolvedArtifact, out var file) || file == null)
                {
                    return Task.FromResult(ExecutionResult.Failed($"无法从数据总线读取曲线数据: {resolvedArtifact}"));
                }

                if (!NvhCurveSampleUtil.TryExtractAsDoubleArray(file, LimitCurveArtifactResolver.NvhSignalGroupName, nvhChForCurve, out var samples) || samples.Length == 0)
                {
                    return Task.FromResult(ExecutionResult.Failed("曲线样本为空或通道类型不支持"));
                }

                var failIndex = -1;
                var maxVal = samples[0];
                var minVal = samples[0];
                for (var i = 0; i < samples.Length; i++)
                {
                    var v = samples[i];
                    if (v < minVal)
                    {
                        minVal = v;
                    }

                    if (v > maxVal)
                    {
                        maxVal = v;
                    }

                    if (v < cLo || v > cHi)
                    {
                        failIndex = i;
                        break;
                    }
                }

                curvePass = failIndex < 0;
                curveRepresentative = curvePass ? maxVal : samples[failIndex];

                if (!curvePass)
                {
                    curveFailDetail = $"index={failIndex}, value={samples[failIndex]:G}";
                }
            }

            var overall = valuePass && curvePass;
            var summary = BuildSummary(enableValue, enableCurve, valuePass, curvePass, valueActual, vLo, vHi);

            ExecutionResult result;
            if (overall)
            {
                result = ExecutionResult.Successful(summary);
            }
            else
            {
                result = ExecutionResult.Failed(summary);
            }

            if (enableValue)
            {
                result = result.WithOutput(NodeUiOutputKeys.ValueCheckPass, valuePass);
            }

            if (enableCurve)
            {
                result = result.WithOutput(NodeUiOutputKeys.CurveCheckPass, curvePass);
            }

            if (enableValue)
            {
                result = result
                    .WithOutput(NodeUiOutputKeys.ActualValue, valueActual)
                    .WithOutput(NodeUiOutputKeys.LowerLimit, vLo)
                    .WithOutput(NodeUiOutputKeys.UpperLimit, vHi);
            }
            else if (enableCurve)
            {
                result = result
                    .WithOutput(NodeUiOutputKeys.ActualValue, curveRepresentative)
                    .WithOutput(NodeUiOutputKeys.LowerLimit, cLo)
                    .WithOutput(NodeUiOutputKeys.UpperLimit, cHi);
            }

            result = result.WithOutput(NodeUiOutputKeys.Summary, summary);

            if (!string.IsNullOrEmpty(curveFailDetail))
            {
                result = result.WithOutput(NodeUiOutputKeys.CurveFailDetail, curveFailDetail);
            }

            var chartArtifact = resolvedArtifact;
            if (!enableCurve && ShowChartWithoutCurveValidation && chartArtifact != null)
            {
                result = LimitNodeShared.WithOptionalChartDisplay(result, context, true, chartArtifact);
            }
            else if (enableCurve && chartArtifact != null)
            {
                result = result
                    .WithOutput(NodeUiOutputKeys.HasChartData, true)
                    .WithOutput(NodeUiOutputKeys.ChartArtifactKey, chartArtifact);
            }
            else
            {
                result = result.WithOutput(NodeUiOutputKeys.HasChartData, false);
            }

            if (overall)
            {
                log.Info(summary);
            }
            else
            {
                log.Warn(summary);
            }

            return Task.FromResult(result);
        }

        private static string BuildSummary(
            bool doValue,
            bool doCurve,
            bool valuePass,
            bool curvePass,
            double valueActual,
            double vLo,
            double vHi)
        {
            if (doValue && doCurve)
            {
                return $"值卡控={(valuePass ? "OK" : "NG")} ({valueActual:G} in [{vLo:G},{vHi:G}]) · 曲线卡控={(curvePass ? "OK" : "NG")}";
            }

            if (doValue)
            {
                return valuePass
                    ? $"值卡控通过 ({valueActual:G} in [{vLo:G},{vHi:G}])"
                    : $"值卡控失败 ({valueActual:G} vs [{vLo:G},{vHi:G}])";
            }

            return curvePass ? "曲线卡控通过" : "曲线卡控失败";
        }
    }
}
