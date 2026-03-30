using Astra.Core.Nodes.Models;
using Astra.Plugins.Algorithms.Helpers;
using Astra.Plugins.Algorithms.APIs;
using Astra.UI.Abstractions.Nodes;
using System.ComponentModel.DataAnnotations;
using EnumsScaleOptions = Astra.Plugins.Algorithms.Enums.ScaleOptions;

namespace Astra.Plugins.Algorithms.Nodes
{
    /// <summary>希尔伯特包络（时域）。</summary>
    public sealed class HilbertEnvelopeNode : AlgorithmNodeBase
    {
        public HilbertEnvelopeNode() : base(nameof(HilbertEnvelopeNode), "希尔伯特包络")
        {
        }

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            if (!AlgorithmInputLoader.TryLoadVibration(context, Id, DataAcquisitionDeviceName, ResolveChannelKey(),
                    out _, out var signal, out var dispose, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            try
            {
                var env = Nvh.HilbertEnvelope(signal);
                var n = env.Length;
                var t = new double[n];
                var dt = signal.DeltaTime;
                for (var i = 0; i < n; i++)
                    t[i] = i * dt;
                var chart = ChartDisplayPayloadFactory.XYLine(t, env, "时间 (s)", "包络");
                return Task.FromResult(AlgorithmResultPublisher.SuccessWithChart(context, Id, "HilbertEnvelope", chart, tag: "hilbert"));
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

    /// <summary>希尔伯特包络（固定频带）。</summary>
    public sealed class HilbertEnvelopeExFixedNode : AlgorithmNodeBase
    {
        public HilbertEnvelopeExFixedNode() : base(nameof(HilbertEnvelopeExFixedNode), "包络(固定带)")
        {
        }

        [Display(Name = "中心频率 (Hz)", GroupName = "参数", Order = 0)]
        public double CenterFrequency { get; set; } = 1000;

        [Display(Name = "带宽 (Hz)", GroupName = "参数", Order = 1)]
        public double BandwidthHz { get; set; } = 100;

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            if (!AlgorithmInputLoader.TryLoadVibration(context, Id, DataAcquisitionDeviceName, ResolveChannelKey(),
                    out _, out var signal, out var dispose, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            try
            {
                var opt = new EnvelopeExOptions(BandwidthHz, CenterFrequency);
                var env = Nvh.HilbertEnvelopeEx(signal, opt);
                var n = env.Length;
                var t = new double[n];
                var dt = signal.DeltaTime;
                for (var i = 0; i < n; i++)
                    t[i] = i * dt;
                var chart = ChartDisplayPayloadFactory.XYLine(t, env, "时间 (s)", "包络");
                return Task.FromResult(AlgorithmResultPublisher.SuccessWithChart(context, Id, "HilbertEnvelopeExFixed", chart, tag: "hilbert"));
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

    /// <summary>希尔伯特包络（跟踪阶次，需转速通道）。</summary>
    public sealed class HilbertEnvelopeExTrackedNode : AlgorithmNodeWithRpmBase
    {
        public HilbertEnvelopeExTrackedNode() : base(nameof(HilbertEnvelopeExTrackedNode), "包络(跟踪带)")
        {
        }

        [Display(Name = "中心阶次", GroupName = "参数", Order = 0)]
        public double CenterOrder { get; set; } = 1;

        [Display(Name = "阶次带宽", GroupName = "参数", Order = 1)]
        public double OrderBandwidth { get; set; } = 0.5;

        [Display(Name = "滑动窗口长度", GroupName = "参数", Order = 2)]
        public int WindowLength { get; set; } = 1024;

        [Display(Name = "最小频率 (Hz)", GroupName = "参数", Order = 3)]
        public double MinFrequency { get; set; } = 10;

        [Display(Name = "最大频率 (Hz)", GroupName = "参数", Order = 4)]
        public double MaxFrequency { get; set; } = 8000;

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            if (!AlgorithmInputLoader.TryLoadVibration(context, Id, DataAcquisitionDeviceName, ResolveChannelKey(),
                    out var file, out var signal, out var disposeSig, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            var rpmKey = ResolveRpmChannelKey();
            if (string.IsNullOrEmpty(rpmKey))
                return Task.FromResult(ExecutionResult.Failed("请指定转速通道。"));

            if (!AlgorithmNvhSampleUtil.TryExtractAsDoubleArray(file, AlgorithmRawArtifactHelper.NvhSignalGroupName, rpmKey, out var rpmSamples) ||
                rpmSamples.Length == 0)
            {
                disposeSig();
                return Task.FromResult(ExecutionResult.Failed("无法读取转速通道样本。"));
            }

            try
            {
                var opt = new EnvelopeExOptions(CenterOrder, OrderBandwidth, WindowLength, MinFrequency, MaxFrequency, rpmSamples);
                var env = Nvh.HilbertEnvelopeEx(signal, opt);
                var n = env.Length;
                var t = new double[n];
                var dt = signal.DeltaTime;
                for (var i = 0; i < n; i++)
                    t[i] = i * dt;
                var chart = ChartDisplayPayloadFactory.XYLine(t, env, "时间 (s)", "包络");
                return Task.FromResult(AlgorithmResultPublisher.SuccessWithChart(context, Id, "HilbertEnvelopeExTracked", chart, tag: "hilbert"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(ExecutionResult.Failed(ex.Message, ex));
            }
            finally
            {
                disposeSig();
            }
        }
    }

    /// <summary>希尔伯特包络谱。</summary>
    public sealed class HilbertEnvelopeSpectraNode : AlgorithmNodeBase
    {
        public HilbertEnvelopeSpectraNode() : base(nameof(HilbertEnvelopeSpectraNode), "包络谱")
        {
        }

        [Display(Name = "窗函数", GroupName = "参数", Order = 0)]
        public Window WindowType { get; set; } = Window.Hanning;

        [Display(Name = "幅值格式", GroupName = "参数", Order = 1)]
        public Format SpectrumFormat { get; set; } = Format.Rms;

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            if (!AlgorithmInputLoader.TryLoadVibration(context, Id, DataAcquisitionDeviceName, ResolveChannelKey(),
                    out _, out var signal, out var dispose, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            try
            {
                var data = Nvh.HilbertEnvelopeSpectra(signal, WindowType, SpectrumFormat, out var freqAxis);
                var chart = ChartDisplayPayloadFactory.XYLine(freqAxis, data, "频率 (Hz)", "幅值");
                return Task.FromResult(AlgorithmResultPublisher.SuccessWithChart(context, Id, "HilbertEnvelopeSpectra", chart, tag: "hilbert"));
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

    /// <summary>希尔伯特包络平均谱。</summary>
    public sealed class HilbertEnvelopeAvgSpectraNode : AlgorithmNodeBase
    {
        public HilbertEnvelopeAvgSpectraNode() : base(nameof(HilbertEnvelopeAvgSpectraNode), "包络平均谱")
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

        [Display(Name = "幅值格式", GroupName = "参数", Order = 4)]
        public Format SpectrumFormat { get; set; } = Format.Rms;

        [Display(Name = "平均方式", GroupName = "参数", Order = 5)]
        public Average AverageType { get; set; } = Average.Mean;

        [Display(Name = "窗函数", GroupName = "参数", Order = 6)]
        public Window WindowType { get; set; } = Window.Hanning;

        [Display(Name = "计权", GroupName = "参数", Order = 7)]
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
                var data = Nvh.HilbertEnvelopeAvgSpectra(signal, calcOpt, stepOpt, SpectrumFormat, AverageType, WindowType, WeightType, out var freqAxis);
                var chart = ChartDisplayPayloadFactory.XYLine(freqAxis, data, "频率 (Hz)", "幅值");
                return Task.FromResult(AlgorithmResultPublisher.SuccessWithChart(context, Id, "HilbertEnvelopeAvgSpectra", chart, tag: "hilbert"));
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

    /// <summary>Morlet 小波（指定频率轴）。</summary>
    public sealed class MorletWaveletTransformNode : AlgorithmNodeBase
    {
        private Scale _outputScale = Scale.dB;
        private double _referenceValue = AlgorithmScaleReferenceDefaults.ForDb;

        public MorletWaveletTransformNode() : base(nameof(MorletWaveletTransformNode), "Morlet 小波")
        {
        }

        [Display(Name = "起始时间 (s)", GroupName = "参数", Order = 0)]
        public double StartTimeSeconds { get; set; }

        [Display(Name = "最低频率 (Hz)", GroupName = "参数", Order = 1)]
        public double MinFrequency { get; set; } = 10;

        [Display(Name = "最高频率 (Hz)", GroupName = "参数", Order = 2)]
        public double MaxFrequency { get; set; } = 8000;

        [Display(Name = "频率点数", GroupName = "参数", Order = 3)]
        public int FrequencyPointCount { get; set; } = 32;

        [Display(Name = "循环数 nCycles", GroupName = "参数", Order = 4)]
        public double NCycles { get; set; } = 3;

        [Display(Name = "刻度", GroupName = "参数", Order = 5)]
        public Scale OutputScale
        {
            get => _outputScale;
            set => SetScaleWithReferenceSync(ref _outputScale, value, ref _referenceValue, nameof(OutputScale), nameof(ReferenceValue));
        }

        [Display(Name = "参考值", GroupName = "参数", Order = 6)]
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

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            if (!AlgorithmInputLoader.TryLoadVibration(context, Id, DataAcquisitionDeviceName, ResolveChannelKey(),
                    out _, out var signal, out var dispose, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            var n = Math.Max(2, FrequencyPointCount);
            var freqAxis = new double[n];
            for (var i = 0; i < n; i++)
                freqAxis[i] = MinFrequency + (MaxFrequency - MinFrequency) * i / (n - 1);

            try
            {
                var scaleOpt = new EnumsScaleOptions(OutputScale, ReferenceValue);
                var z = Nvh.MorletWaveletTransform(signal, scaleOpt, StartTimeSeconds, freqAxis, NCycles, out var timeAxis);
                var chart = ChartDisplayPayloadFactory.Heatmap(z, timeAxis, freqAxis, "时间 (s)", "频率 (Hz)");
                return Task.FromResult(AlgorithmResultPublisher.SuccessWithChart(context, Id, "MorletWavelet", chart, tag: "wavelet"));
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

    /// <summary>LMS Morlet 小波（对数频率轴）。</summary>
    public sealed class LmsMorletWaveletTransformNode : AlgorithmNodeBase
    {
        private Scale _outputScale = Scale.dB;
        private double _referenceValue = AlgorithmScaleReferenceDefaults.ForDb;

        public LmsMorletWaveletTransformNode() : base(nameof(LmsMorletWaveletTransformNode), "LMS Morlet 小波")
        {
        }

        [Display(Name = "起始时间 (s)", GroupName = "参数", Order = 0)]
        public double StartTimeSeconds { get; set; }

        [Display(Name = "最低频率 (Hz)", GroupName = "参数", Order = 1)]
        public double MinFrequency { get; set; } = 10;

        [Display(Name = "最高频率 (Hz)", GroupName = "参数", Order = 2)]
        public double MaxFrequency { get; set; } = 8000;

        [Display(Name = "每倍频程划分数", GroupName = "参数", Order = 3)]
        public int BandsPerOctave { get; set; } = 12;

        [Display(Name = "刻度", GroupName = "参数", Order = 4)]
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

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            if (!AlgorithmInputLoader.TryLoadVibration(context, Id, DataAcquisitionDeviceName, ResolveChannelKey(),
                    out _, out var signal, out var dispose, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            try
            {
                var scaleOpt = new EnumsScaleOptions(OutputScale, ReferenceValue);
                var z = Nvh.LmsMorletWaveletTransform(signal, scaleOpt, StartTimeSeconds, MinFrequency, MaxFrequency, BandsPerOctave, out var timeAxis, out var freqAxis);
                var chart = ChartDisplayPayloadFactory.Heatmap(z, timeAxis, freqAxis, "时间 (s)", "频率 (Hz)");
                return Task.FromResult(AlgorithmResultPublisher.SuccessWithChart(context, Id, "LmsMorletWavelet", chart, tag: "wavelet"));
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

    /// <summary>调制谱（分辨率模式）。</summary>
    public sealed class ModulationSpectrumResolutionNode : AlgorithmNodeBase
    {
        private Scale _outputScale = Scale.dB;
        private double _referenceValue = AlgorithmScaleReferenceDefaults.ForDb;

        public ModulationSpectrumResolutionNode() : base(nameof(ModulationSpectrumResolutionNode), "调制谱(分辨率)")
        {
        }

        [Display(Name = "频率分辨率 (Hz)", GroupName = "参数", Order = 0)]
        public double FrequencyResolution { get; set; } = 1;

        [Display(Name = "调制截止频率 (Hz)", GroupName = "参数", Order = 1)]
        public double CutoffFrequency { get; set; } = 500;

        [Display(Name = "刻度", GroupName = "参数", Order = 2)]
        public Scale OutputScale
        {
            get => _outputScale;
            set => SetScaleWithReferenceSync(ref _outputScale, value, ref _referenceValue, nameof(OutputScale), nameof(ReferenceValue));
        }

        [Display(Name = "参考值", GroupName = "参数", Order = 3)]
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

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            if (!AlgorithmInputLoader.TryLoadVibration(context, Id, DataAcquisitionDeviceName, ResolveChannelKey(),
                    out _, out var signal, out var dispose, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            try
            {
                var scaleOpt = new EnumsScaleOptions(OutputScale, ReferenceValue);
                var z = Nvh.ModulationSpectrumAnalysis(signal, FrequencyResolution, CutoffFrequency, scaleOpt,
                    out var freqAxis, out var timeAxis, out _, out _);
                var chart = ChartDisplayPayloadFactory.Heatmap(z, timeAxis, freqAxis, "时间 (s)", "频率 (Hz)");
                return Task.FromResult(AlgorithmResultPublisher.SuccessWithChart(context, Id, "ModulationSpectrumRes", chart, tag: "modulation"));
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

    /// <summary>调制谱（STFT）。</summary>
    public sealed class ModulationSpectrumStftNode : AlgorithmNodeBase
    {
        private Scale _outputScale = Scale.dB;
        private double _referenceValue = AlgorithmScaleReferenceDefaults.ForDb;

        public ModulationSpectrumStftNode() : base(nameof(ModulationSpectrumStftNode), "调制谱(STFT)")
        {
        }

        [Display(Name = "窗长(点)", GroupName = "参数", Order = 0)]
        public int WindowSize { get; set; } = 2048;

        [Display(Name = "跳跃(点)", GroupName = "参数", Order = 1)]
        public int HopSize { get; set; } = 256;

        [Display(Name = "调制截止频率 (Hz)", GroupName = "参数", Order = 2)]
        public double CutoffFrequency { get; set; } = 500;

        [Display(Name = "刻度", GroupName = "参数", Order = 3)]
        public Scale OutputScale
        {
            get => _outputScale;
            set => SetScaleWithReferenceSync(ref _outputScale, value, ref _referenceValue, nameof(OutputScale), nameof(ReferenceValue));
        }

        [Display(Name = "参考值", GroupName = "参数", Order = 4)]
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

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            if (!AlgorithmInputLoader.TryLoadVibration(context, Id, DataAcquisitionDeviceName, ResolveChannelKey(),
                    out _, out var signal, out var dispose, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            try
            {
                var scaleOpt = new EnumsScaleOptions(OutputScale, ReferenceValue);
                var z = Nvh.ModulationSpectrumAnalysis(signal, WindowSize, HopSize, CutoffFrequency, scaleOpt,
                    out var freqAxis, out var timeAxis, out _, out _);
                var chart = ChartDisplayPayloadFactory.Heatmap(z, timeAxis, freqAxis, "时间 (s)", "频率 (Hz)");
                return Task.FromResult(AlgorithmResultPublisher.SuccessWithChart(context, Id, "ModulationSpectrumStft", chart, tag: "modulation"));
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
