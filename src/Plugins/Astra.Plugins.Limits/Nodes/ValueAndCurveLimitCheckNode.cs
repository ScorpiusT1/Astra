using Astra.Core.Nodes.Models;
using Astra.UI.Abstractions.Nodes;
using Astra.UI.Abstractions.Attributes;
using Astra.UI.PropertyEditors;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Plugins.Limits.Nodes
{
    /// <summary>
    /// 同时或分别启用值卡控与曲线卡控；曲线数据按采集卡 + 通道解析。
    /// </summary>
    public class ValueAndCurveLimitCheckNode : Node
    {
        [Display(Name = "卡控方式", GroupName = "工作模式", Order = 1, Description = "选择要执行的检查类型")]
        public LimitCheckMode CheckMode { get; set; } = LimitCheckMode.Both;

        [JsonProperty("EnableValueValidation", DefaultValueHandling = DefaultValueHandling.Ignore)]
        private bool? _legacyEnableValue;

        [JsonProperty("EnableCurveValidation", DefaultValueHandling = DefaultValueHandling.Ignore)]
        private bool? _legacyEnableCurve;

        [OnDeserialized]
        private void OnDeserializedMigrateLegacy(StreamingContext context)
        {
            if (_legacyEnableValue is null && _legacyEnableCurve is null)
            {
                return;
            }

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

        [Display(Name = "实测值变量名", GroupName = "数值", Order = 1, Description = "与脚本里写入的全局变量名一致")]
        public string GlobalVariableKey { get; set; } = string.Empty;

        [Display(Name = "数值合格下限", GroupName = "数值", Order = 2)]
        public double ValueLowerLimit { get; set; }

        [Display(Name = "数值合格上限", GroupName = "数值", Order = 3)]
        public double ValueUpperLimit { get; set; }

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

        public IEnumerable<string> CurveChannelOptions =>
            LimitsDesignTimeOptions.GetChannelNamesForDevice(
                string.IsNullOrEmpty(_dataAcquisitionDeviceName) ? null : _dataAcquisitionDeviceName);

        [Display(Name = "通道", GroupName = "曲线数据", Order = 2, Description = "未选采集卡时仅显示未选择；选定采集卡后首项为组内默认首通道")]
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

        [Display(Name = "曲线合格下限（逐点）", GroupName = "曲线合格带", Order = 1)]
        public double CurveLowerLimit { get; set; }

        [Display(Name = "曲线合格上限（逐点）", GroupName = "曲线合格带", Order = 2)]
        public double CurveUpperLimit { get; set; }

        [Display(Name = "未判曲线时仍显示曲线", GroupName = "主页曲线", Order = 1, Description = "仅做数值检查时，仍按采集卡+通道显示曲线")]
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
                var gk = GlobalVariableKey?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(gk))
                {
                    return Task.FromResult(ExecutionResult.Failed("已选择数值检查，请填写实测值变量名"));
                }

                if (!context.GlobalVariables.TryGetValue(gk, out var raw))
                {
                    return Task.FromResult(ExecutionResult.Failed($"找不到全局变量: {gk}"));
                }

                if (!LimitNodeShared.TryConvertToDouble(raw, out valueActual))
                {
                    return Task.FromResult(ExecutionResult.Failed("全局变量无法转换为数值"));
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
            if (needCurveArtifact)
            {
                if (!LimitCurveArtifactResolver.TryResolveRawArtifactKey(context, Id, DataAcquisitionDeviceName, out var art, out var err))
                {
                    if (enableCurve)
                    {
                        return Task.FromResult(ExecutionResult.Failed(err));
                    }
                }
                else
                {
                    resolvedArtifact = art;
                }
            }

            if (enableCurve)
            {
                if (string.IsNullOrEmpty(resolvedArtifact))
                {
                    return Task.FromResult(ExecutionResult.Failed("无法解析曲线 Raw 键"));
                }

                var store = context.GetRawDataStore();
                if (store == null || !store.TryGet(resolvedArtifact, out var obj) || obj is not NVHDataBridge.Models.NvhMemoryFile file)
                {
                    return Task.FromResult(ExecutionResult.Failed($"无法读取 Raw 曲线: {resolvedArtifact}"));
                }

                var ch = LimitNodeShared.NormalizeCurveChannelKey(_curveChannelName);
                if (!NvhCurveSampleUtil.TryExtractAsDoubleArray(file, LimitCurveArtifactResolver.NvhSignalGroupName, ch, out var samples) || samples.Length == 0)
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
