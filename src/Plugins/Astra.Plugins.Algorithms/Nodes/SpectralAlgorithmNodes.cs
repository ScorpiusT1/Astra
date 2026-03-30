using Astra.Core.Nodes.Models;
using Astra.Plugins.Algorithms.Helpers;
using Astra.Plugins.Algorithms.APIs;
using Astra.UI.Abstractions.Nodes;
using Astra.UI.Abstractions.Attributes;
using Astra.UI.PropertyEditors;
using System.ComponentModel.DataAnnotations;
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

        [Display(Name = "谱线数", GroupName = "参数", Order = 0)]
        public int SpectrumLines { get; set; } = 1024;

        [Display(Name = "时间增量(s)", GroupName = "参数", Order = 1)]
        public double TimeIncrementSeconds { get; set; } = 0.1;

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

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            if (!AlgorithmInputLoader.TryLoadVibration(context, Id, DataAcquisitionDeviceName, ResolveChannelKey(),
                    out _, out var signal, out var dispose, out var err))
            {
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));
            }

            try
            {
                var data = Nvh.OverallLevelSpectral(signal, SpectrumLines, TimeIncrementSeconds, ReferenceValue,
                    WindowType, WeightType, ScaleType, out var timeAxis);
                var chart = ChartDisplayPayloadFactory.XYLine(timeAxis, data, "时间 (s)", "幅值");
                return Task.FromResult(AlgorithmResultPublisher.SuccessWithChart(context, Id, "OverallLevelSpectral", chart, tag: "spectral"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(ExecutionResult.Failed(ex.Message, ex));
            }
            finally
            {
                dispose();
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
        }

        [Display(Name = "谱计算", GroupName = "谱选项", Order = 0)]
        public SpectraCalcType CalcType { get; set; } = SpectraCalcType.SpectrumLines;

        [Display(Name = "谱计算值", GroupName = "谱选项", Order = 1)]
        public double CalcValue { get; set; } = 1024;

        [Display(Name = "步进类型", GroupName = "谱选项", Order = 2)]
        public SpectraStepType StepType { get; set; } = SpectraStepType.Overlap;

        [Display(Name = "步进值", GroupName = "谱选项", Order = 3)]
        public double StepValue { get; set; } = 0.5;

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

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            if (!AlgorithmInputLoader.TryLoadVibration(context, Id, DataAcquisitionDeviceName, ResolveChannelKey(),
                    out _, out var signal, out var dispose, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            try
            {
                var calcOpt = new SpectraCalcOptions(CalcType, CalcValue);
                var stepOpt = new SpectraStepOptions(StepType, StepValue);
                var scaleAvg = new EnumsScaleOptions(Scale.Linear, 1.0);
                var spectrum = Nvh.AveragedSpectrum(signal, calcOpt, stepOpt, scaleAvg, SpectrumFormat, AverageType,
                    SpectrumWindow, SpectrumWeight);

                SpectraOptionHelpers.ResolveSpectrumLinesAndIncrement(signal, calcOpt, stepOpt, out var spectrumLines, out _);
                var freqStep = 1.0 / (signal.DeltaTime * 2 * spectrumLines);

                var bandLevels = Nvh.Octave(spectrum, freqStep, SpectrumWindow, OctaveResolution,
                    new EnumsScaleOptions(OctaveScale, OctaveReference),
                    out var bandCenters, out _, out _);

                var labels = bandCenters.Select(x => x.ToString("G4")).ToArray();
                var chart = ChartDisplayPayloadFactory.Bar(labels, bandLevels, "中心频率 (Hz)", "幅值");
                return Task.FromResult(AlgorithmResultPublisher.SuccessWithChart(context, Id, "Octave", chart, tag: "spectral"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(ExecutionResult.Failed(ex.Message, ex));
            }
            finally
            {
                dispose();
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

        [Display(Name = "谱计算类型", GroupName = "参数", Order = 0)]
        public SpectraCalcType CalcType { get; set; } = SpectraCalcType.SpectrumLines;

        [Display(Name = "谱计算值", GroupName = "参数", Order = 1)]
        public double CalcValue { get; set; } = 1024;

        [Display(Name = "步进类型", GroupName = "参数", Order = 2)]
        public SpectraStepType StepType { get; set; } = SpectraStepType.Overlap;

        [Display(Name = "步进值", GroupName = "参数", Order = 3)]
        public double StepValue { get; set; } = 0.5;

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

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            if (!AlgorithmInputLoader.TryLoadVibration(context, Id, DataAcquisitionDeviceName, ResolveChannelKey(),
                    out _, out var signal, out var dispose, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            try
            {
                var calcOpt = new SpectraCalcOptions(CalcType, CalcValue);
                var stepOpt = new SpectraStepOptions(StepType, StepValue);
                var scaleOpt = new EnumsScaleOptions(OutputScale, ReferenceValue);
                var data = Nvh.AveragedSpectrum(signal, calcOpt, stepOpt, scaleOpt, SpectrumFormat, AverageType, WindowType, WeightType);
                SpectraOptionHelpers.ResolveSpectrumLinesAndIncrement(signal, calcOpt, stepOpt, out var spectrumLines, out _);
                var n = data.Length;
                var freq = new double[n];
                var df = 1.0 / (signal.DeltaTime * 2 * spectrumLines);
                for (var i = 0; i < n; i++)
                    freq[i] = i * df;

                var chart = ChartDisplayPayloadFactory.XYLine(freq, data, "频率 (Hz)",
                    OutputScale == Scale.dB ? "幅值 (dB)" : "幅值");
                return Task.FromResult(AlgorithmResultPublisher.SuccessWithChart(context, Id, "AveragedSpectrum", chart, tag: "spectral"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(ExecutionResult.Failed(ex.Message, ex));
            }
            finally
            {
                dispose();
            }
        }
    }
}
