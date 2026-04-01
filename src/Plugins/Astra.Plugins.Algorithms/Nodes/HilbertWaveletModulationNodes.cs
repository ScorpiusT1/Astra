using Astra.Core.Nodes.Models;
using Astra.Plugins.Algorithms.APIs;
using Astra.Plugins.Algorithms.Helpers;
using Astra.UI.Abstractions.Attributes;
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
            var specs = ResolveInputSpecs();
            if (specs.Count == 0)
                return Task.FromResult(ExecutionResult.Failed("请至少选择一个采集卡。"));

            if (!AlgorithmInputLoader.TryLoadMultipleVibrations(context, Id, specs, out var entries, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            try
            {
                var results = new (string Label, ChartDisplayPayload Chart)[entries.Count];
                Parallel.For(0, entries.Count, i =>
                {
                    var e = entries[i];
                    var env = Nvh.HilbertEnvelope(e.Signal);
                    var n = env.Length;
                    var t = new double[n];
                    var dt = e.Signal.DeltaTime;
                    for (var k = 0; k < n; k++)
                        t[k] = k * dt;
                    results[i] = (e.Label, ChartDisplayPayloadFactory.XYLine(t, env, "时间 (s)", "包络"));
                });
                return Task.FromResult(AlgorithmResultPublisher.SuccessWithMultiChart(context, Id, "HilbertEnvelope", results.ToList(), tag: "hilbert"));
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

    /// <summary>希尔伯特包络（固定频带）。</summary>
    public sealed class HilbertEnvelopeExFixedNode : AlgorithmNodeBase
    {
        public HilbertEnvelopeExFixedNode() : base(nameof(HilbertEnvelopeExFixedNode), "包络(固定带)")
        {
        }

        [Order(2, 0)]
        [Display(Name = "中心频率 (Hz)", GroupName = "参数")]
        public double CenterFrequency { get; set; } = 1000;

        [Display(Name = "带宽 (Hz)", GroupName = "参数", Order = 1)]
        public double BandwidthHz { get; set; } = 100;

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var specs = ResolveInputSpecs();
            if (specs.Count == 0)
                return Task.FromResult(ExecutionResult.Failed("请至少选择一个采集卡。"));

            if (!AlgorithmInputLoader.TryLoadMultipleVibrations(context, Id, specs, out var entries, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            try
            {
                var results = new (string Label, ChartDisplayPayload Chart)[entries.Count];
                Parallel.For(0, entries.Count, i =>
                {
                    var e = entries[i];
                    var opt = new EnvelopeExOptions(BandwidthHz, CenterFrequency);
                    var env = Nvh.HilbertEnvelopeEx(e.Signal, opt);
                    var n = env.Length;
                    var t = new double[n];
                    var dt = e.Signal.DeltaTime;
                    for (var k = 0; k < n; k++)
                        t[k] = k * dt;
                    results[i] = (e.Label, ChartDisplayPayloadFactory.XYLine(t, env, "时间 (s)", "包络"));
                });
                return Task.FromResult(AlgorithmResultPublisher.SuccessWithMultiChart(context, Id, "HilbertEnvelopeExFixed", results.ToList(), tag: "hilbert"));
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

    /// <summary>希尔伯特包络（跟踪阶次，需转速通道）。</summary>
    public sealed class HilbertEnvelopeExTrackedNode : AlgorithmNodeWithRpmBase
    {
        public HilbertEnvelopeExTrackedNode() : base(nameof(HilbertEnvelopeExTrackedNode), "包络(跟踪带)")
        {
        }

        [Order(2, 0)]
        [Display(Name = "中心阶次", GroupName = "参数")]
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
            var specs = ResolveInputSpecs();
            if (specs.Count == 0)
                return Task.FromResult(ExecutionResult.Failed("请至少选择一个采集卡。"));

            if (!AlgorithmInputLoader.TryLoadMultipleVibrations(context, Id, specs, out var entries, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            var (rpmDevice, rpmChannel) = ResolveRpmSpec();
            if (string.IsNullOrEmpty(rpmChannel))
            {
                AlgorithmInputLoader.DisposeAll(entries);
                return Task.FromResult(ExecutionResult.Failed("请指定转速通道。"));
            }

            try
            {
                var rpmData = new double[entries.Count][];
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    var rpmFile = (!string.IsNullOrEmpty(rpmDevice) && rpmDevice != e.Label.Split('/')[0])
                        ? entries.FirstOrDefault(x => x.Label.StartsWith(rpmDevice!))?.File ?? e.File
                        : e.File;
                    if (!AlgorithmNvhSampleUtil.TryExtractAsDoubleArray(rpmFile, AlgorithmRawArtifactHelper.NvhSignalGroupName, rpmChannel, out var rpmSamples) ||
                        rpmSamples.Length == 0)
                        return Task.FromResult(ExecutionResult.Failed($"无法从 {rpmDevice ?? e.Label} 读取转速通道样本。"));
                    rpmData[i] = rpmSamples;
                }

                var results = new (string Label, ChartDisplayPayload Chart)[entries.Count];
                Parallel.For(0, entries.Count, i =>
                {
                    var e = entries[i];
                    var opt = new EnvelopeExOptions(CenterOrder, OrderBandwidth, WindowLength, MinFrequency, MaxFrequency, rpmData[i]);
                    var env = Nvh.HilbertEnvelopeEx(e.Signal, opt);
                    var n = env.Length;
                    var t = new double[n];
                    var dt = e.Signal.DeltaTime;
                    for (var k = 0; k < n; k++)
                        t[k] = k * dt;
                    results[i] = (e.Label, ChartDisplayPayloadFactory.XYLine(t, env, "时间 (s)", "包络"));
                });
                return Task.FromResult(AlgorithmResultPublisher.SuccessWithMultiChart(context, Id, "HilbertEnvelopeExTracked", results.ToList(), tag: "hilbert"));
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

    /// <summary>希尔伯特包络谱。</summary>
    public sealed class HilbertEnvelopeSpectraNode : AlgorithmNodeBase
    {
        public HilbertEnvelopeSpectraNode() : base(nameof(HilbertEnvelopeSpectraNode), "包络谱")
        {
        }

        [Order(2, 0)]
        [Display(Name = "窗函数", GroupName = "参数")]
        public Window WindowType { get; set; } = Window.Hanning;

        [Display(Name = "幅值格式", GroupName = "参数", Order = 1)]
        public Format SpectrumFormat { get; set; } = Format.Rms;

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var specs = ResolveInputSpecs();
            if (specs.Count == 0)
                return Task.FromResult(ExecutionResult.Failed("请至少选择一个采集卡。"));

            if (!AlgorithmInputLoader.TryLoadMultipleVibrations(context, Id, specs, out var entries, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            try
            {
                var results = new (string Label, ChartDisplayPayload Chart)[entries.Count];
                Parallel.For(0, entries.Count, i =>
                {
                    var e = entries[i];
                    var data = Nvh.HilbertEnvelopeSpectra(e.Signal, WindowType, SpectrumFormat, out var freqAxis);
                    results[i] = (e.Label, ChartDisplayPayloadFactory.XYLine(freqAxis, data, "频率 (Hz)", "幅值"));
                });
                return Task.FromResult(AlgorithmResultPublisher.SuccessWithMultiChart(context, Id, "HilbertEnvelopeSpectra", results.ToList(), tag: "hilbert"));
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

    /// <summary>希尔伯特包络平均谱。</summary>
    public sealed class HilbertEnvelopeAvgSpectraNode : AlgorithmNodeBase
    {
        public HilbertEnvelopeAvgSpectraNode() : base(nameof(HilbertEnvelopeAvgSpectraNode), "包络平均谱")
        {
        }

        [Order(2, 0)]
        [Display(Name = "谱计算类型", GroupName = "参数")]
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
            var specs = ResolveInputSpecs();
            if (specs.Count == 0)
                return Task.FromResult(ExecutionResult.Failed("请至少选择一个采集卡。"));

            if (!AlgorithmInputLoader.TryLoadMultipleVibrations(context, Id, specs, out var entries, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            try
            {
                var results = new (string Label, ChartDisplayPayload Chart)[entries.Count];
                Parallel.For(0, entries.Count, i =>
                {
                    var e = entries[i];
                    var calcOpt = new SpectraCalcOptions(CalcType, CalcValue);
                    var stepOpt = new SpectraStepOptions(StepType, StepValue);
                    var data = Nvh.HilbertEnvelopeAvgSpectra(e.Signal, calcOpt, stepOpt, SpectrumFormat, AverageType, WindowType, WeightType, out var freqAxis);
                    results[i] = (e.Label, ChartDisplayPayloadFactory.XYLine(freqAxis, data, "频率 (Hz)", "幅值"));
                });
                return Task.FromResult(AlgorithmResultPublisher.SuccessWithMultiChart(context, Id, "HilbertEnvelopeAvgSpectra", results.ToList(), tag: "hilbert"));
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

    /// <summary>Morlet 小波（指定频率轴）。</summary>
    public sealed class MorletWaveletTransformNode : AlgorithmNodeBase
    {
        private Scale _outputScale = Scale.dB;
        private double _referenceValue = AlgorithmScaleReferenceDefaults.ForDb;

        public MorletWaveletTransformNode() : base(nameof(MorletWaveletTransformNode), "Morlet 小波")
        {
        }

        [Order(2, 0)]
        [Display(Name = "起始时间 (s)", GroupName = "参数")]
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
            var specs = ResolveInputSpecs();
            if (specs.Count == 0)
                return Task.FromResult(ExecutionResult.Failed("请至少选择一个采集卡。"));

            if (!AlgorithmInputLoader.TryLoadMultipleVibrations(context, Id, specs, out var entries, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            var fCount = Math.Max(2, FrequencyPointCount);
            var freqAxis = new double[fCount];
            for (var i = 0; i < fCount; i++)
                freqAxis[i] = MinFrequency + (MaxFrequency - MinFrequency) * i / (fCount - 1);

            try
            {
                var results = new (string Label, ChartDisplayPayload Chart)[entries.Count];
                Parallel.For(0, entries.Count, i =>
                {
                    var e = entries[i];
                    var scaleOpt = new EnumsScaleOptions(OutputScale, ReferenceValue);
                    var z = Nvh.MorletWaveletTransform(e.Signal, scaleOpt, StartTimeSeconds, freqAxis, NCycles, out var timeAxis);
                    results[i] = (e.Label, ChartDisplayPayloadFactory.Heatmap(z, timeAxis, freqAxis, "时间 (s)", "频率 (Hz)"));
                });
                return Task.FromResult(AlgorithmResultPublisher.SuccessWithMultiChart(context, Id, "MorletWavelet", results.ToList(), tag: "wavelet"));
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

    /// <summary>LMS Morlet 小波（对数频率轴）。</summary>
    public sealed class LmsMorletWaveletTransformNode : AlgorithmNodeBase
    {
        private Scale _outputScale = Scale.dB;
        private double _referenceValue = AlgorithmScaleReferenceDefaults.ForDb;

        public LmsMorletWaveletTransformNode() : base(nameof(LmsMorletWaveletTransformNode), "LMS Morlet 小波")
        {
        }

        [Order(2, 0)]
        [Display(Name = "起始时间 (s)", GroupName = "参数")]
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
            var specs = ResolveInputSpecs();
            if (specs.Count == 0)
                return Task.FromResult(ExecutionResult.Failed("请至少选择一个采集卡。"));

            if (!AlgorithmInputLoader.TryLoadMultipleVibrations(context, Id, specs, out var entries, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            try
            {
                var results = new (string Label, ChartDisplayPayload Chart)[entries.Count];
                Parallel.For(0, entries.Count, i =>
                {
                    var e = entries[i];
                    var scaleOpt = new EnumsScaleOptions(OutputScale, ReferenceValue);
                    var z = Nvh.LmsMorletWaveletTransform(e.Signal, scaleOpt, StartTimeSeconds, MinFrequency, MaxFrequency, BandsPerOctave, out var timeAxis, out var freqAxis);
                    results[i] = (e.Label, ChartDisplayPayloadFactory.Heatmap(z, timeAxis, freqAxis, "时间 (s)", "频率 (Hz)"));
                });
                return Task.FromResult(AlgorithmResultPublisher.SuccessWithMultiChart(context, Id, "LmsMorletWavelet", results.ToList(), tag: "wavelet"));
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

    /// <summary>调制谱（分辨率模式）。</summary>
    public sealed class ModulationSpectrumResolutionNode : AlgorithmNodeBase
    {
        private Scale _outputScale = Scale.dB;
        private double _referenceValue = AlgorithmScaleReferenceDefaults.ForDb;

        public ModulationSpectrumResolutionNode() : base(nameof(ModulationSpectrumResolutionNode), "调制谱(分辨率)")
        {
        }

        [Order(2, 0)]
        [Display(Name = "频率分辨率 (Hz)", GroupName = "参数")]
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
            var specs = ResolveInputSpecs();
            if (specs.Count == 0)
                return Task.FromResult(ExecutionResult.Failed("请至少选择一个采集卡。"));

            if (!AlgorithmInputLoader.TryLoadMultipleVibrations(context, Id, specs, out var entries, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            try
            {
                var results = new (string Label, ChartDisplayPayload Chart)[entries.Count];
                Parallel.For(0, entries.Count, i =>
                {
                    var e = entries[i];
                    var scaleOpt = new EnumsScaleOptions(OutputScale, ReferenceValue);
                    var z = Nvh.ModulationSpectrumAnalysis(e.Signal, FrequencyResolution, CutoffFrequency, scaleOpt,
                        out var freqAxis, out var timeAxis, out _, out _);
                    results[i] = (e.Label, ChartDisplayPayloadFactory.Heatmap(z, timeAxis, freqAxis, "时间 (s)", "频率 (Hz)"));
                });
                return Task.FromResult(AlgorithmResultPublisher.SuccessWithMultiChart(context, Id, "ModulationSpectrumRes", results.ToList(), tag: "modulation"));
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

    /// <summary>调制谱（STFT）。</summary>
    public sealed class ModulationSpectrumStftNode : AlgorithmNodeBase
    {
        private Scale _outputScale = Scale.dB;
        private double _referenceValue = AlgorithmScaleReferenceDefaults.ForDb;

        public ModulationSpectrumStftNode() : base(nameof(ModulationSpectrumStftNode), "调制谱(STFT)")
        {
        }

        [Order(2, 0)]
        [Display(Name = "窗长(点)", GroupName = "参数")]
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
            var specs = ResolveInputSpecs();
            if (specs.Count == 0)
                return Task.FromResult(ExecutionResult.Failed("请至少选择一个采集卡。"));

            if (!AlgorithmInputLoader.TryLoadMultipleVibrations(context, Id, specs, out var entries, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            try
            {
                var results = new (string Label, ChartDisplayPayload Chart)[entries.Count];
                Parallel.For(0, entries.Count, i =>
                {
                    var e = entries[i];
                    var scaleOpt = new EnumsScaleOptions(OutputScale, ReferenceValue);
                    var z = Nvh.ModulationSpectrumAnalysis(e.Signal, WindowSize, HopSize, CutoffFrequency, scaleOpt,
                        out var freqAxis, out var timeAxis, out _, out _);
                    results[i] = (e.Label, ChartDisplayPayloadFactory.Heatmap(z, timeAxis, freqAxis, "时间 (s)", "频率 (Hz)"));
                });
                return Task.FromResult(AlgorithmResultPublisher.SuccessWithMultiChart(context, Id, "ModulationSpectrumStft", results.ToList(), tag: "modulation"));
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
