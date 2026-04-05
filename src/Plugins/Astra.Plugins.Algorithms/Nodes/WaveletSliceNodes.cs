using Astra.Core.Nodes.Models;
using Astra.Plugins.Algorithms.APIs;
using Astra.Plugins.Algorithms.Helpers;
using Astra.UI.Abstractions.Attributes;
using Astra.UI.Abstractions.Nodes;
using Astra.Workflow.AlgorithmChannel.APIs;
using Astra.Workflow.AlgorithmChannel.Helpers;
using Astra.Workflow.AlgorithmChannel.Nodes;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using EnumsScaleOptions = Astra.Plugins.Algorithms.Enums.ScaleOptions;

namespace Astra.Plugins.Algorithms.Nodes
{
    /// <summary>
    /// Morlet 小波频带切片：在指定频率范围内，选取该带内「沿时间轴峰值幅值」最大的那条频率线，输出时间–幅值曲线。
    /// </summary>
    public sealed class MorletWaveletBandMaxSliceNode : AlgorithmNodeBase
    {
        private Scale _outputScale = Scale.dB;
        private double _referenceValue = AlgorithmScaleReferenceDefaults.ForDb;

        public MorletWaveletBandMaxSliceNode() : base(nameof(MorletWaveletBandMaxSliceNode), "小波频带最大切片")
        {
        }

        [Order(2, 0)]
        [Display(Name = "起始时间 (s)", GroupName = "参数")]
        public double StartTimeSeconds { get; set; }

        [Display(Name = "分析最低频率 (Hz)", GroupName = "参数", Order = 1)]
        public double AnalysisMinFrequency { get; set; } = 10;

        [Display(Name = "分析最高频率 (Hz)", GroupName = "参数", Order = 2)]
        public double AnalysisMaxFrequency { get; set; } = 8000;

        [Display(Name = "频带下限 (Hz)", GroupName = "参数", Order = 3)]
        public double BandMinFrequency { get; set; } = 100;

        [Display(Name = "频带上限 (Hz)", GroupName = "参数", Order = 4)]
        public double BandMaxFrequency { get; set; } = 500;

        [Display(Name = "频率点数", GroupName = "参数", Order = 5)]
        public int FrequencyPointCount { get; set; } = 64;

        [Display(Name = "循环数 nCycles", GroupName = "参数", Order = 6)]
        public double NCycles { get; set; } = 3;

        [Display(Name = "刻度", GroupName = "参数", Order = 7)]
        public Scale OutputScale
        {
            get => _outputScale;
            set => SetScaleWithReferenceSync(ref _outputScale, value, ref _referenceValue, nameof(OutputScale), nameof(ReferenceValue));
        }

        [Display(Name = "参考值", GroupName = "参数", Order = 8)]
        public double ReferenceValue
        {
            get => _referenceValue;
            set
            {
                if (Math.Abs(_referenceValue - value) < double.Epsilon)
                    return;
                _referenceValue = value;
                OnPropertyChanged();
            }
        }

        protected override IEnumerable<string> EnumerateDesignTimeScalarLogicalNames(string channelLabel)
        {
            yield return $"切片频率Hz({channelLabel})";
            yield return $"切片行峰值({channelLabel})";
        }

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var specs = ResolveInputSpecs();
            if (specs.Count == 0)
                return Task.FromResult(ExecutionResult.Failed("请至少选择一个通道，或确保上游存在可用采集卡（未选通道时将使用各卡首通道）。"));

            if (!AlgorithmInputLoader.TryLoadMultipleVibrations(context, Id, specs, out var entries, out var err, AnalysisWindowStartSeconds, AnalysisWindowEndSeconds))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            if (BandMinFrequency >= BandMaxFrequency)
            {
                AlgorithmInputLoader.DisposeAll(entries);
                return Task.FromResult(ExecutionResult.Failed("频带下限须小于频带上限。"));
            }

            if (AnalysisMinFrequency >= AnalysisMaxFrequency)
            {
                AlgorithmInputLoader.DisposeAll(entries);
                return Task.FromResult(ExecutionResult.Failed("分析最低频率须小于分析最高频率。"));
            }

            var fCount = Math.Max(2, FrequencyPointCount);
            var freqAxis = new double[fCount];
            for (var i = 0; i < fCount; i++)
                freqAxis[i] = AnalysisMinFrequency + (AnalysisMaxFrequency - AnalysisMinFrequency) * i / (fCount - 1);

            if (BandMaxFrequency < freqAxis[0] || BandMinFrequency > freqAxis[^1])
            {
                AlgorithmInputLoader.DisposeAll(entries);
                return Task.FromResult(ExecutionResult.Failed("频带与分析频率范围无交集，请调整分析范围或频带。"));
            }

            try
            {
                var results = new (string Label, ChartDisplayPayload Chart, double SelFreq, double RowPeak)[entries.Count];
                AlgorithmParallel.For(0, entries.Count, cancellationToken, i =>
                {
                    var e = entries[i];
                    var scaleOpt = new EnumsScaleOptions(OutputScale, ReferenceValue);
                    var z = Nvh.MorletWaveletTransform(e.Signal, scaleOpt, StartTimeSeconds, freqAxis, NCycles, out var timeAxis);
                    AlgorithmTimeAxisHelper.ApplyAnalysisOriginInPlace(timeAxis, e.TimeAxisOriginSeconds);
                    var (slice, selFreq, rowPeak) = WaveletSliceHelper.PickBandMaxRow(z, freqAxis, timeAxis.Length, BandMinFrequency, BandMaxFrequency);
                    var chart = ChartDisplayPayloadFactory.XYLine(timeAxis, slice, "时间 (s)", "幅值");
                    results[i] = (e.Label, chart, selFreq, rowPeak);
                });
                var charts = results.Select(r => (r.Label, r.Chart)).ToList();
                var chartResult = PublishMultiChart(context, "MorletBandMaxSlice", charts, tag: "wavelet");
                var scalars = results.SelectMany(r => new (string, double, string)[]
                {
                    ($"切片频率Hz({r.Label})", r.SelFreq, "Hz"),
                    ($"切片行峰值({r.Label})", r.RowPeak, string.Empty),
                }).ToList();
                return Task.FromResult(AppendScalarsToChartResult(context, chartResult, scalars, "wavelet", "MorletBandMaxSlice"));
            }
            catch (AggregateException aex)
            {
                var inner = aex.Flatten().InnerException ?? aex;
                return Task.FromResult(ExecutionResult.Failed(inner.Message, inner));
            }
            catch (Exception ex)
            {
                return Task.FromResult(ExecutionResult.Failed(ex.Message, ex));
            }
            finally
            {
                AlgorithmInputLoader.DisposeAll(entries);
            }
        }
    }

    /// <summary>
    /// Morlet 小波单频切片：在给定分析频率轴上，选取与目标频率最接近的频点，输出该频点的时间–幅值曲线。
    /// </summary>
    public sealed class MorletWaveletNearestFrequencySliceNode : AlgorithmNodeBase
    {
        private Scale _outputScale = Scale.dB;
        private double _referenceValue = AlgorithmScaleReferenceDefaults.ForDb;

        public MorletWaveletNearestFrequencySliceNode() : base(nameof(MorletWaveletNearestFrequencySliceNode), "小波单频切片")
        {
        }

        [Order(2, 0)]
        [Display(Name = "起始时间 (s)", GroupName = "参数")]
        public double StartTimeSeconds { get; set; }

        [Display(Name = "最低频率 (Hz)", GroupName = "参数", Order = 1)]
        public double MinFrequency { get; set; } = 10;

        [Display(Name = "最高频率 (Hz)", GroupName = "参数", Order = 2)]
        public double MaxFrequency { get; set; } = 8000;

        [Display(Name = "目标频率 (Hz)", GroupName = "参数", Order = 3)]
        public double TargetFrequencyHz { get; set; } = 1000;

        [Display(Name = "频率点数", GroupName = "参数", Order = 4)]
        public int FrequencyPointCount { get; set; } = 64;

        [Display(Name = "循环数 nCycles", GroupName = "参数", Order = 5)]
        public double NCycles { get; set; } = 3;

        [Display(Name = "刻度", GroupName = "参数", Order = 6)]
        public Scale OutputScale
        {
            get => _outputScale;
            set => SetScaleWithReferenceSync(ref _outputScale, value, ref _referenceValue, nameof(OutputScale), nameof(ReferenceValue));
        }

        [Display(Name = "参考值", GroupName = "参数", Order = 7)]
        public double ReferenceValue
        {
            get => _referenceValue;
            set
            {
                if (Math.Abs(_referenceValue - value) < double.Epsilon)
                    return;
                _referenceValue = value;
                OnPropertyChanged();
            }
        }

        protected override IEnumerable<string> EnumerateDesignTimeScalarLogicalNames(string channelLabel)
        {
            yield return $"选用频率Hz({channelLabel})";
            yield return $"单频切片峰值({channelLabel})";
        }

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var specs = ResolveInputSpecs();
            if (specs.Count == 0)
                return Task.FromResult(ExecutionResult.Failed("请至少选择一个通道，或确保上游存在可用采集卡（未选通道时将使用各卡首通道）。"));

            if (!AlgorithmInputLoader.TryLoadMultipleVibrations(context, Id, specs, out var entries, out var err, AnalysisWindowStartSeconds, AnalysisWindowEndSeconds))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            if (MinFrequency >= MaxFrequency)
            {
                AlgorithmInputLoader.DisposeAll(entries);
                return Task.FromResult(ExecutionResult.Failed("最低频率须小于最高频率。"));
            }

            var fCount = Math.Max(2, FrequencyPointCount);
            var freqAxis = new double[fCount];
            for (var i = 0; i < fCount; i++)
                freqAxis[i] = MinFrequency + (MaxFrequency - MinFrequency) * i / (fCount - 1);

            if (TargetFrequencyHz < freqAxis[0] || TargetFrequencyHz > freqAxis[^1])
            {
                AlgorithmInputLoader.DisposeAll(entries);
                return Task.FromResult(ExecutionResult.Failed("目标频率须落在分析频率范围内。"));
            }

            try
            {
                var results = new (string Label, ChartDisplayPayload Chart, double UsedFreq, double Peak)[entries.Count];
                AlgorithmParallel.For(0, entries.Count, cancellationToken, i =>
                {
                    var e = entries[i];
                    var scaleOpt = new EnumsScaleOptions(OutputScale, ReferenceValue);
                    var z = Nvh.MorletWaveletTransform(e.Signal, scaleOpt, StartTimeSeconds, freqAxis, NCycles, out var timeAxis);
                    AlgorithmTimeAxisHelper.ApplyAnalysisOriginInPlace(timeAxis, e.TimeAxisOriginSeconds);
                    var (slice, usedFreq) = WaveletSliceHelper.PickNearestFrequencyRow(z, freqAxis, timeAxis, TargetFrequencyHz);
                    var peak = AlgorithmScalarMath.MaxAbs(slice);
                    var chart = ChartDisplayPayloadFactory.XYLine(timeAxis, slice, "时间 (s)", "幅值");
                    results[i] = (e.Label, chart, usedFreq, peak);
                });
                var charts = results.Select(r => (r.Label, r.Chart)).ToList();
                var chartResult = PublishMultiChart(context, "MorletNearestFreqSlice", charts, tag: "wavelet");
                var scalars = results.SelectMany(r => new (string, double, string)[]
                {
                    ($"选用频率Hz({r.Label})", r.UsedFreq, "Hz"),
                    ($"单频切片峰值({r.Label})", r.Peak, string.Empty),
                }).ToList();
                return Task.FromResult(AppendScalarsToChartResult(context, chartResult, scalars, "wavelet", "MorletNearestFreqSlice"));
            }
            catch (AggregateException aex)
            {
                var inner = aex.Flatten().InnerException ?? aex;
                return Task.FromResult(ExecutionResult.Failed(inner.Message, inner));
            }
            catch (Exception ex)
            {
                return Task.FromResult(ExecutionResult.Failed(ex.Message, ex));
            }
            finally
            {
                AlgorithmInputLoader.DisposeAll(entries);
            }
        }
    }

    internal static class WaveletSliceHelper
    {
        /// <summary>
        /// z 为 [freqBins, timeBins]。在频带内选取沿时间峰值幅值最大的频率行。
        /// </summary>
        public static (double[] Slice, double SelectedFrequencyHz, double RowPeak) PickBandMaxRow(
            double[,] z,
            double[] freqAxis,
            int timeBins,
            double bandMinHz,
            double bandMaxHz)
        {
            var nFreq = freqAxis.Length;
            var bestI = -1;
            var bestRowPeak = double.NegativeInfinity;

            for (var i = 0; i < nFreq; i++)
            {
                var f = freqAxis[i];
                if (f < bandMinHz || f > bandMaxHz)
                    continue;

                var rowPeak = 0.0;
                for (var j = 0; j < timeBins; j++)
                {
                    var v = Math.Abs(z[i, j]);
                    if (v > rowPeak)
                        rowPeak = v;
                }

                if (rowPeak > bestRowPeak)
                {
                    bestRowPeak = rowPeak;
                    bestI = i;
                }
            }

            if (bestI < 0)
                throw new InvalidOperationException("频带内没有可用的频率采样点，请增大频率点数或放宽频带。");

            var slice = new double[timeBins];
            for (var j = 0; j < timeBins; j++)
                slice[j] = z[bestI, j];

            return (slice, freqAxis[bestI], bestRowPeak);
        }

        /// <summary>
        /// 选取与 <paramref name="targetHz"/> 最接近的频率行。
        /// </summary>
        public static (double[] Slice, double UsedFrequencyHz) PickNearestFrequencyRow(
            double[,] z,
            double[] freqAxis,
            int timeBins,
            double targetHz)
        {
            var nFreq = freqAxis.Length;
            var bestI = 0;
            var bestD = double.PositiveInfinity;
            for (var i = 0; i < nFreq; i++)
            {
                var d = Math.Abs(freqAxis[i] - targetHz);
                if (d < bestD)
                {
                    bestD = d;
                    bestI = i;
                }
            }

            var slice = new double[timeBins];
            for (var j = 0; j < timeBins; j++)
                slice[j] = z[bestI, j];

            return (slice, freqAxis[bestI]);
        }

        /// <summary>
        /// 选取与 <paramref name="targetHz"/> 最接近的频率行。
        /// <paramref name="timeAxis"/> 与矩阵 z 的时间维列数须一致（用于校验；物理时间与 z 的列一一对应）。
        /// </summary>
        public static (double[] Slice, double UsedFrequencyHz) PickNearestFrequencyRow(
            double[,] z,
            double[] freqAxis,
            double[] timeAxis,
            double targetHz)
        {
            if (timeAxis == null)
                throw new ArgumentNullException(nameof(timeAxis));
            if (z.GetLength(1) != timeAxis.Length)
                throw new ArgumentException("timeAxis 长度须与 z 的时间维（列数）一致。", nameof(timeAxis));

            return PickNearestFrequencyRow(z, freqAxis, timeAxis.Length, targetHz);
        }
    }
}
