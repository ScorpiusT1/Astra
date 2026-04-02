using Astra.Core.Nodes.Models;
using Astra.Plugins.Algorithms.Helpers;
using Astra.Plugins.Algorithms.APIs;
using Astra.UI.Abstractions.Nodes;
using Astra.UI.Abstractions.Attributes;
using Astra.UI.PropertyEditors;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using EnumsScaleOptions = Astra.Plugins.Algorithms.Enums.ScaleOptions;

namespace Astra.Plugins.Algorithms.Nodes
{
    /// <summary>整体声级谱（OverallLevelSpectral）。</summary>
    public sealed class OverallLevelSpectralNode : AlgorithmNodeBase
    {
        private double _referenceValue = AlgorithmScaleReferenceDefaults.ForDb;
        private Scale _scaleType = Scale.dB;

        public OverallLevelSpectralNode() : base(nameof(OverallLevelSpectralNode), "整体声级谱")
        {
        }

        [Order(2, 0)]
        [Display(Name = "谱线数", GroupName = "参数")]
        public int SpectrumLines { get; set; } = 2048;

        [Display(Name = "参考值", GroupName = "参数", Order = 2)]
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

        [Display(Name = "窗函数", GroupName = "参数", Order = 3)]
        public Window WindowType { get; set; } = Window.Hanning;

        [Display(Name = "计权", GroupName = "参数", Order = 4)]
        public Weight WeightType { get; set; } = Weight.A;

        [Display(Name = "刻度", GroupName = "参数", Order = 5)]
        public Scale ScaleType
        {
            get => _scaleType;
            set => SetScaleWithReferenceSync(ref _scaleType, value, ref _referenceValue, nameof(ScaleType), nameof(ReferenceValue));
        }

        [Display(Name = "重叠率", GroupName = "参数", Order = 6)]
        public double Overlap { get; set; } = 0.5;

        protected override IEnumerable<string> EnumerateDesignTimeScalarLogicalNames(string channelLabel)
        {
            yield return $"整体声级峰值({channelLabel})";
        }

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var specs = ResolveInputSpecs();
            if (specs.Count == 0)
                return Task.FromResult(ExecutionResult.Failed("请至少选择一个通道，或确保上游存在可用采集卡（未选通道时将使用各卡首通道）。"));

            if (!AlgorithmInputLoader.TryLoadMultipleVibrations(context, Id, specs, out var entries, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            try
            {
                var results = new (string Label, ChartDisplayPayload Chart, double Peak)[entries.Count];
                Parallel.For(0, entries.Count, i =>
                {
                    var e = entries[i];

                    double increment = SpectrumLines * 2 * Overlap / (1.0 / e.Signal.DeltaTime);

                    var data = Nvh.OverallLevelSpectral(e.Signal, SpectrumLines, increment, ReferenceValue,
                        WindowType, WeightType, ScaleType, out var timeAxis);
                    var chart = SpectralChartPayloadHelper.CreateAdaptive(timeAxis, data, "时间 (s)", "幅值");
                    results[i] = (e.Label, chart, AlgorithmScalarMath.MaxAbs(data));
                });
                var charts = results.Select(r => (r.Label, r.Chart)).ToList();
                var chartResult = PublishMultiChart(context, "OverallLevelSpectral", charts, tag: "spectral");
                var scalars = results.Select(r => ($"整体声级峰值({r.Label})", r.Peak, string.Empty)).ToList();
                return Task.FromResult(AppendScalarsToChartResult(context, chartResult, scalars, "spectral", "OverallLevelSpectral"));
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

    /// <summary>倍频程分析（先求平均谱再倍频程）。</summary>
    public sealed class OctaveAnalysisNode : AlgorithmNodeBase
    {
        private Scale _octaveScale = Scale.dB;
        private double _octaveReference = AlgorithmScaleReferenceDefaults.ForDb;

        public OctaveAnalysisNode() : base(nameof(OctaveAnalysisNode), "倍频程分析")
        {
            // 多路柱状图默认分子图，便于对比各采集卡倍频程
            ChartDisplayLayout = ChartLayoutMode.SubPlots;
        }

        [Order(2, 0)]
        [Display(Name = "谱计算", GroupName = "谱选项")]
        public SpectraCalcType CalcType { get; set; } = SpectraCalcType.Resolution;

        [Display(Name = "谱计算值", GroupName = "谱选项", Order = 1)]
        public double CalcValue { get; set; } = 1;

        [Display(Name = "步进类型", GroupName = "谱选项", Order = 2)]
        public SpectraStepType StepType { get; set; } = SpectraStepType.Increment;

        [Display(Name = "步进值", GroupName = "谱选项", Order = 3)]
        public double StepValue { get; set; } = 0.2;

        [Display(Name = "幅值格式", GroupName = "谱选项", Order = 4)]
        public Format SpectrumFormat { get; set; } = Format.Rms;

        [Display(Name = "平均方式", GroupName = "谱选项", Order = 5)]
        public Average AverageType { get; set; } = Average.Mean;

        [Display(Name = "窗函数", GroupName = "谱选项", Order = 6)]
        public Window SpectrumWindow { get; set; } = Window.Hanning;

        [Display(Name = "计权", GroupName = "谱选项", Order = 7)]
        public Weight SpectrumWeight { get; set; } = Weight.A;

        [Display(Name = "倍频程分辨率", GroupName = "倍频程", Order = 0)]
        public Astra.Plugins.Algorithms.Enums.Octave OctaveResolution { get; set; } = Astra.Plugins.Algorithms.Enums.Octave.ThirdOctave;

        [Display(Name = "倍频程刻度", GroupName = "倍频程", Order = 1)]
        public Scale OctaveScale
        {
            get => _octaveScale;
            set => SetScaleWithReferenceSync(ref _octaveScale, value, ref _octaveReference, nameof(OctaveScale), nameof(OctaveReference));
        }

        [Display(Name = "参考值", GroupName = "倍频程", Order = 2)]
        public double OctaveReference
        {
            get => _octaveReference;
            set
            {
                if (Math.Abs(_octaveReference - value) < double.Epsilon)
                    return;
                _octaveReference = value;
                OnPropertyChanged();
            }
        }

        protected override IEnumerable<string> EnumerateDesignTimeScalarLogicalNames(string channelLabel)
        {
            yield return $"倍频程峰值({channelLabel})";
        }

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var specs = ResolveInputSpecs();
            if (specs.Count == 0)
                return Task.FromResult(ExecutionResult.Failed("请至少选择一个通道，或确保上游存在可用采集卡（未选通道时将使用各卡首通道）。"));

            if (!AlgorithmInputLoader.TryLoadMultipleVibrations(context, Id, specs, out var entries, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            try
            {
                var results = new (string Label, ChartDisplayPayload Chart, double Peak)[entries.Count];
                Parallel.For(0, entries.Count, i =>
                {
                    var e = entries[i];
                    var calcOpt = new SpectraCalcOptions(CalcType, CalcValue);
                    var stepOpt = new SpectraStepOptions(StepType, StepValue);
                    var scaleAvg = new EnumsScaleOptions(Scale.Linear, 1.0);
                    var spectrum = Nvh.AveragedSpectrum(e.Signal, calcOpt, stepOpt, scaleAvg, SpectrumFormat, AverageType,
                        SpectrumWindow, SpectrumWeight);

                    SpectraOptionHelpers.ResolveSpectrumLinesAndIncrement(e.Signal, calcOpt, stepOpt, out var spectrumLines, out _);
                    var freqStep = 1.0 / (e.Signal.DeltaTime * 2 * spectrumLines);

                    var bandLevels = Nvh.Octave(spectrum, freqStep, SpectrumWindow, OctaveResolution,
                        new EnumsScaleOptions(OctaveScale, OctaveReference),
                        out var bandCenters, out _, out _);

                    var labels = bandCenters.Select(x => x.ToString("G4")).ToArray();
                    var chart = ChartDisplayPayloadFactory.Bar(labels, bandLevels, "中心频率 (Hz)", "幅值");
                    results[i] = (e.Label, chart, AlgorithmScalarMath.Max(bandLevels));
                });
                var charts = results.Select(r => (r.Label, r.Chart)).ToList();
                var chartResult = PublishMultiChart(context, "Octave", charts, tag: "spectral");
                var scalars = results.Select(r => ($"倍频程峰值({r.Label})", r.Peak, string.Empty)).ToList();
                return Task.FromResult(AppendScalarsToChartResult(context, chartResult, scalars, "spectral", "Octave"));
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

    /// <summary>平均自功率谱。</summary>
    public sealed class AveragedSpectrumNode : AlgorithmNodeBase
    {
        private Scale _outputScale = Scale.dB;
        private double _referenceValue = AlgorithmScaleReferenceDefaults.ForDb;

        public AveragedSpectrumNode() : base(nameof(AveragedSpectrumNode), "平均谱")
        {
        }

        [Order(2, 0)]
        [Display(Name = "谱计算类型", GroupName = "参数")]
        public SpectraCalcType CalcType { get; set; } = SpectraCalcType.Resolution;

        [Display(Name = "谱计算值", GroupName = "参数", Order = 1)]
        public double CalcValue { get; set; } = 1;

        [Display(Name = "步进类型", GroupName = "参数", Order = 2)]
        public SpectraStepType StepType { get; set; } = SpectraStepType.Increment;

        [Display(Name = "步进值", GroupName = "参数", Order = 3)]
        public double StepValue { get; set; } = 0.2;

        [Display(Name = "输出刻度", GroupName = "参数", Order = 4)]
        public Scale OutputScale
        {
            get => _outputScale;
            set => SetScaleWithReferenceSync(ref _outputScale, value, ref _referenceValue, nameof(OutputScale), nameof(ReferenceValue));
        }

        [Display(Name = "参考值", GroupName = "参数", Order = 5)]
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

        [Display(Name = "幅值格式", GroupName = "参数", Order = 6)]
        public Format SpectrumFormat { get; set; } = Format.Rms;

        [Display(Name = "平均方式", GroupName = "参数", Order = 7)]
        public Average AverageType { get; set; } = Average.Mean;

        [Display(Name = "窗函数", GroupName = "参数", Order = 8)]
        public Window WindowType { get; set; } = Window.Hanning;

        [Display(Name = "计权", GroupName = "参数", Order = 9)]
        public Weight WeightType { get; set; } = Weight.A;

        protected override IEnumerable<string> EnumerateDesignTimeScalarLogicalNames(string channelLabel)
        {
            yield return $"谱线峰值({channelLabel})";
        }

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var specs = ResolveInputSpecs();
            if (specs.Count == 0)
                return Task.FromResult(ExecutionResult.Failed("请至少选择一个通道，或确保上游存在可用采集卡（未选通道时将使用各卡首通道）。"));

            if (!AlgorithmInputLoader.TryLoadMultipleVibrations(context, Id, specs, out var entries, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            try
            {
                var results = new (string Label, ChartDisplayPayload Chart, double Peak)[entries.Count];
                Parallel.For(0, entries.Count, i =>
                {
                    var e = entries[i];
                    var calcOpt = new SpectraCalcOptions(CalcType, CalcValue);
                    var stepOpt = new SpectraStepOptions(StepType, StepValue);
                    var scaleOpt = new EnumsScaleOptions(OutputScale, ReferenceValue);
                    var data = Nvh.AveragedSpectrum(e.Signal, calcOpt, stepOpt, scaleOpt, SpectrumFormat, AverageType, WindowType, WeightType);
                    SpectraOptionHelpers.ResolveSpectrumLinesAndIncrement(e.Signal, calcOpt, stepOpt, out var spectrumLines, out _);
                    var n = data.Length;
                    var freq = new double[n];
                    var df = 1.0 / (e.Signal.DeltaTime * 2 * spectrumLines);
                    for (var k = 0; k < n; k++)
                        freq[k] = k * df;

                    var chart = SpectralChartPayloadHelper.CreateAdaptive(freq, data, "频率 (Hz)",
                        OutputScale == Scale.dB ? "幅值 (dB)" : "幅值");
                    results[i] = (e.Label, chart, AlgorithmScalarMath.MaxAbs(data));
                });
                var charts = results.Select(r => (r.Label, r.Chart)).ToList();
                var chartResult = PublishMultiChart(context, "AveragedSpectrum", charts, tag: "spectral");
                var scalars = results.Select(r => ($"谱线峰值({r.Label})", r.Peak, string.Empty)).ToList();
                return Task.FromResult(AppendScalarsToChartResult(context, chartResult, scalars, "spectral", "AveragedSpectrum"));
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
}
