using Astra.Core.Nodes.Models;
using Astra.Core.Nodes.Ui;
using Astra.UI.Abstractions.Attributes;
using Astra.UI.PropertyEditors;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Plugins.Limits.Nodes
{
    /// <summary>
    /// 从工作流全局变量读取标量并与上下限比较；可选按采集卡+通道在主页显示曲线（不参与判定）。
    /// </summary>
    public class ValueLimitCheckNode : Node
    {
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

        [Display(Name = "采集卡", GroupName = "主页曲线", Order = 2, Description = "首项为未选择；选定后须与多采集节点中的采集卡设备名一致")]
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

        public IEnumerable<string> CurveChannelOptions =>
            LimitsDesignTimeOptions.GetChannelNamesForDevice(
                string.IsNullOrEmpty(_dataAcquisitionDeviceName) ? null : _dataAcquisitionDeviceName);

        [Display(Name = "通道", GroupName = "主页曲线", Order = 3, Description = "未选采集卡时仅显示未选择；选定采集卡后首项为组内默认首通道")]
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
