using Astra.Core.Nodes.Models;
using Astra.UI.Abstractions.Nodes;
using Astra.UI.Abstractions.Attributes;
using Astra.UI.PropertyEditors;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Plugins.Limits.Nodes
{
    /// <summary>
    /// 从 Raw 数据存储读取 NVH 曲线（按采集卡 + 通道），逐样本检查是否落在闭区间 [下限, 上限] 内。
    /// </summary>
    public class CurveLimitCheckNode : Node
    {
        private string _dataAcquisitionDeviceName = string.Empty;
        private string _curveChannelName = string.Empty;

        [Display(Name = "采集卡", GroupName = "曲线数据", Order = 1, Description = "首项为未选择；选定后须与多采集节点中的采集卡设备名一致")]
        [Editor(typeof(ComboBoxPropertyEditor))]
        [ItemsSource(typeof(LimitsDesignTimeOptions), nameof(LimitsDesignTimeOptions.GetAcquisitionDeviceNames), DisplayMemberPath = ".")]
        public string DataAcquisitionDeviceName
        {
            get => string.IsNullOrEmpty(_dataAcquisitionDeviceName)
                ? LimitsDesignTimeOptions.UnselectedLabel
                : _dataAcquisitionDeviceName;
            set
            {
                var v = value ?? string.Empty;
                if (string.Equals(v, LimitsDesignTimeOptions.UnselectedLabel, StringComparison.Ordinal))
                {
                    v = string.Empty;
                }

                if (string.Equals(_dataAcquisitionDeviceName, v, StringComparison.Ordinal))
                {
                    return;
                }

                _dataAcquisitionDeviceName = v;
                OnPropertyChanged();
                _curveChannelName = string.Empty;
                OnPropertyChanged(nameof(CurveChannelName));
                OnPropertyChanged(nameof(CurveChannelOptions));
            }
        }

        /// <summary>供通道下拉绑定：随所选采集卡变化。</summary>
        public IEnumerable<string> CurveChannelOptions =>
            LimitsDesignTimeOptions.GetChannelNamesForDevice(
                string.IsNullOrEmpty(_dataAcquisitionDeviceName) ? null : _dataAcquisitionDeviceName);

        [Display(Name = "通道", GroupName = "曲线数据", Order = 2, Description = "未选采集卡时仅显示未选择；选定采集卡后首项为组内默认首通道，其余为配置中的通道名")]
        [Editor(typeof(ComboBoxPropertyEditor))]
        [ItemsSource(nameof(CurveChannelOptions), DisplayMemberPath = ".")]
        public string CurveChannelName
        {
            get
            {
                if (string.IsNullOrEmpty(_dataAcquisitionDeviceName))
                {
                    return LimitsDesignTimeOptions.UnselectedLabel;
                }

                return string.IsNullOrEmpty(_curveChannelName)
                    ? LimitsDesignTimeOptions.UseFirstChannelInGroupLabel
                    : _curveChannelName;
            }
            set
            {
                var v = value ?? string.Empty;
                if (string.Equals(v, LimitsDesignTimeOptions.UnselectedLabel, StringComparison.Ordinal) ||
                    string.Equals(v, LimitsDesignTimeOptions.UseFirstChannelInGroupLabel, StringComparison.Ordinal))
                {
                    v = string.Empty;
                }

                if (string.Equals(_curveChannelName, v, StringComparison.Ordinal))
                {
                    return;
                }

                _curveChannelName = v;
                OnPropertyChanged();
            }
        }

        [Display(Name = "合格下限（逐点）", GroupName = "曲线合格带", Order = 1, Description = "每个采样点不得低于此值")]
        public double LowerLimit { get; set; }

        [Display(Name = "合格上限（逐点）", GroupName = "曲线合格带", Order = 2, Description = "每个采样点不得高于此值")]
        public double UpperLimit { get; set; }

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var log = context.CreateExecutionLogger($"曲线卡控:{Name}");
            if (!LimitCurveArtifactResolver.TryResolveRawArtifactKey(context, Id, DataAcquisitionDeviceName, out var artifact, out var resolveErr))
            {
                return Task.FromResult(ExecutionResult.Failed(resolveErr));
            }

            if (!context.TryGetArtifact<NVHDataBridge.Models.NvhMemoryFile>(artifact, out var file) || file == null)
            {
                return Task.FromResult(ExecutionResult.Failed($"无法从数据总线读取曲线数据: {artifact}"));
            }

            var ch = LimitNodeShared.NormalizeCurveChannelKey(_curveChannelName);
            if (!NvhCurveSampleUtil.TryExtractAsDoubleArray(file, LimitCurveArtifactResolver.NvhSignalGroupName, ch, out var samples) || samples.Length == 0)
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
