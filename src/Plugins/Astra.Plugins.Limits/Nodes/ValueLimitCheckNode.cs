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
    /// 从工作流全局变量读取标量并与上下限比较；可选按采集卡+通道在主页显示曲线（不参与判定）。
    /// </summary>
    public class ValueLimitCheckNode : Node
    {
        [JsonIgnore]
        private readonly List<IDesignTimeDataSourceInfo> _upstreamSources = new();

        [JsonIgnore]
        private bool _registrySubscribed;

        [OnDeserialized]
        private void OnDeserializedCallback(StreamingContext ctx)
        {
            if (CachedChannelOptions?.Count > 0 && !string.IsNullOrEmpty(Id))
                DesignTimeUpstreamRegistry.CacheChannelOptions(Id, CachedChannelOptions);
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
                    OnPropertyChanged(nameof(DeviceNameOptions));
                    OnPropertyChanged(nameof(CurveChannelOptions));
                }
            };
        }

        private void RefreshAndCacheChannelOptions()
        {
            if (string.IsNullOrEmpty(_dataAcquisitionDeviceName)) return;
            var channels = DesignTimeUpstreamRegistry.GetChannelNamesForDevice(Id, _dataAcquisitionDeviceName).ToList();
            if (channels.Count > 0)
            {
                CachedChannelOptions = channels;
                DesignTimeUpstreamRegistry.CacheChannelOptions(Id, channels);
            }
        }

        [Display(Name = "实测值变量名", GroupName = "数值", Order = 1, Description = "与脚本里写入的全局变量名一致")]
        public string GlobalVariableKey { get; set; } = string.Empty;

        [Display(Name = "合格下限", GroupName = "数值", Order = 2, Description = "实测值不低于此值（含）")]
        public double LowerLimit { get; set; }

        [Display(Name = "合格上限", GroupName = "数值", Order = 3, Description = "实测值不高于此值（含）")]
        public double UpperLimit { get; set; }

        [Display(Name = "在主页同时显示曲线", GroupName = "主页曲线", Order = 1, Description = "开启后需选择采集卡与通道；仅展示，不参与合格判定")]
        public bool AssociateCurveForDisplay { get; set; }

        private string _dataAcquisitionDeviceName = string.Empty;
        private string _curveChannelName = string.Empty;

        [JsonIgnore]
        public IEnumerable<string> DeviceNameOptions
        {
            get
            {
                EnsureRegistrySubscription();
                var list = new List<string> { LimitsDesignTimeOptions.UnselectedLabel };
                var fromRegistry = DesignTimeUpstreamRegistry.GetDeviceNames(Id).ToList();
                if (fromRegistry.Count > 0)
                    list.AddRange(fromRegistry);
                else if (!string.IsNullOrEmpty(_dataAcquisitionDeviceName))
                    list.Add(_dataAcquisitionDeviceName);
                return list;
            }
        }

        [Display(Name = "采集卡", GroupName = "主页曲线", Order = 2, Description = "连线后自动显示上游设备")]
        [Editor(typeof(ComboBoxPropertyEditor))]
        [ItemsSource(nameof(DeviceNameOptions), DisplayMemberPath = ".")]
        public string DataAcquisitionDeviceName
        {
            get => string.IsNullOrEmpty(_dataAcquisitionDeviceName)
                ? LimitsDesignTimeOptions.UnselectedLabel
                : _dataAcquisitionDeviceName;
            set
            {
                var v = value ?? string.Empty;
                if (string.Equals(v, LimitsDesignTimeOptions.UnselectedLabel, StringComparison.Ordinal))
                    v = string.Empty;
                if (string.Equals(_dataAcquisitionDeviceName, v, StringComparison.Ordinal))
                    return;
                _dataAcquisitionDeviceName = v;
                OnPropertyChanged();
                _curveChannelName = string.Empty;
                OnPropertyChanged(nameof(CurveChannelName));
                OnPropertyChanged(nameof(CurveChannelOptions));
            }
        }

        public List<string> CachedChannelOptions { get; set; } = new();

        [JsonIgnore]
        public IEnumerable<string> CurveChannelOptions
        {
            get
            {
                EnsureRegistrySubscription();
                if (string.IsNullOrEmpty(_dataAcquisitionDeviceName))
                    return new[] { LimitsDesignTimeOptions.UnselectedLabel };

                var channels = DesignTimeUpstreamRegistry.GetChannelNamesForDevice(Id, _dataAcquisitionDeviceName).ToList();
                if (channels.Count > 0)
                {
                    CachedChannelOptions = channels.ToList();
                    DesignTimeUpstreamRegistry.CacheChannelOptions(Id, CachedChannelOptions);
                    channels.Insert(0, LimitsDesignTimeOptions.UseFirstChannelInGroupLabel);
                    return channels;
                }

                var cached = DesignTimeUpstreamRegistry.GetCachedChannelOptions(Id);
                if (cached.Count > 0)
                {
                    var opts = new List<string> { LimitsDesignTimeOptions.UseFirstChannelInGroupLabel };
                    opts.AddRange(cached);
                    return opts;
                }

                var fallback = LimitsDesignTimeOptions.GetChannelNamesForDevice(_dataAcquisitionDeviceName).ToList();
                if (fallback.Count > 0) return fallback;
                if (!string.IsNullOrEmpty(_curveChannelName))
                    return new[] { LimitsDesignTimeOptions.UseFirstChannelInGroupLabel, _curveChannelName };
                return new[] { LimitsDesignTimeOptions.UnselectedLabel };
            }
        }

        [Display(Name = "通道", GroupName = "主页曲线", Order = 3, Description = "连线后自动显示上游通道")]
        [Editor(typeof(ComboBoxPropertyEditor))]
        [ItemsSource(nameof(CurveChannelOptions), DisplayMemberPath = ".")]
        public string CurveChannelName
        {
            get
            {
                if (string.IsNullOrEmpty(_dataAcquisitionDeviceName))
                    return LimitsDesignTimeOptions.UnselectedLabel;
                return string.IsNullOrEmpty(_curveChannelName)
                    ? LimitsDesignTimeOptions.UseFirstChannelInGroupLabel
                    : _curveChannelName;
            }
            set
            {
                var v = value ?? string.Empty;
                if (string.Equals(v, LimitsDesignTimeOptions.UnselectedLabel, StringComparison.Ordinal) ||
                    string.Equals(v, LimitsDesignTimeOptions.UseFirstChannelInGroupLabel, StringComparison.Ordinal))
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
            if (targetNode?.Id == Id && sourceNode is IDesignTimeDataSourceInfo src && !_upstreamSources.Contains(src))
            {
                _upstreamSources.Add(src);
                DesignTimeUpstreamRegistry.SetSources(Id, _upstreamSources);
                RefreshAndCacheChannelOptions();
            }
        }

        public override void OnConnectionDetached(Edge? edge, Node? sourceNode, Node? targetNode)
        {
            base.OnConnectionDetached(edge, sourceNode, targetNode);
            if (edge == null)
            {
                _upstreamSources.Clear();
                DesignTimeUpstreamRegistry.SetSources(Id, _upstreamSources);
                return;
            }

            if (targetNode?.Id == Id && sourceNode is IDesignTimeDataSourceInfo src)
                _upstreamSources.Remove(src);

            if (_upstreamSources.Count == 0)
            {
                _dataAcquisitionDeviceName = string.Empty;
                _curveChannelName = string.Empty;
                CachedChannelOptions?.Clear();
                DesignTimeUpstreamRegistry.ClearChannelOptionsCache(Id);
                OnPropertyChanged(nameof(DataAcquisitionDeviceName));
                OnPropertyChanged(nameof(CurveChannelName));
            }
            DesignTimeUpstreamRegistry.SetSources(Id, _upstreamSources);
        }

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var log = context.CreateExecutionLogger($"值卡控:{Name}");
            var key = GlobalVariableKey?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(key))
            {
                return Task.FromResult(ExecutionResult.Failed("请填写实测值变量名"));
            }

            if (!context.GlobalVariables.TryGetValue(key, out var raw))
            {
                return Task.FromResult(ExecutionResult.Failed($"找不到全局变量: {key}"));
            }

            if (!LimitNodeShared.TryConvertToDouble(raw, out var actual))
            {
                return Task.FromResult(ExecutionResult.Failed("全局变量无法转换为数值"));
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
            if (AssociateCurveForDisplay)
            {
                if (LimitCurveArtifactResolver.TryResolveRawArtifactKey(context, Id, DataAcquisitionDeviceName, out var art, out _))
                {
                    chartKey = art;
                }
            }

            result = LimitNodeShared.WithOptionalChartDisplay(
                result,
                context,
                AssociateCurveForDisplay,
                chartKey);

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
