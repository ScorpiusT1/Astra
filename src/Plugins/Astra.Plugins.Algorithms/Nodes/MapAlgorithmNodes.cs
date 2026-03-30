using Astra.Core.Nodes.Models;
using Astra.Plugins.Algorithms.Helpers;
using Astra.Plugins.Algorithms.APIs;
using Astra.UI.Abstractions.Nodes;
using System.ComponentModel.DataAnnotations;

namespace Astra.Plugins.Algorithms.Nodes
{
    /// <summary>时间-频率图（STFT 类图）。</summary>
    public sealed class TimeFrequencyMapNode : AlgorithmNodeBase
    {
        private double _referenceValue = AlgorithmScaleReferenceDefaults.ForDb;
        private Scale _scaleType = Scale.dB;

        public TimeFrequencyMapNode() : base(nameof(TimeFrequencyMapNode), "时间频率图")
        {
        }

        [Display(Name = "谱线数", GroupName = "参数", Order = 0)]
        public int SpectrumLines { get; set; } = 1024;

        [Display(Name = "时间增量(s)", GroupName = "参数", Order = 1)]
        public double TimeIncrementSeconds { get; set; } = 0.05;

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

        [Display(Name = "幅值格式", GroupName = "参数", Order = 3)]
        public Format SpectrumFormat { get; set; } = Format.Rms;

        [Display(Name = "窗函数", GroupName = "参数", Order = 4)]
        public Window WindowType { get; set; } = Window.Hanning;

        [Display(Name = "计权", GroupName = "参数", Order = 5)]
        public Weight WeightType { get; set; } = Weight.A;

        [Display(Name = "刻度", GroupName = "参数", Order = 6)]
        public Scale ScaleType
        {
            get => _scaleType;
            set => SetScaleWithReferenceSync(ref _scaleType, value, ref _referenceValue, nameof(ScaleType), nameof(ReferenceValue));
        }

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            if (!AlgorithmInputLoader.TryLoadVibration(context, Id, DataAcquisitionDeviceName, ResolveChannelKey(),
                    out _, out var signal, out var dispose, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            try
            {
                var z = Nvh.TimeFrequencyMap(signal, SpectrumLines, TimeIncrementSeconds, ReferenceValue,
                    SpectrumFormat, WindowType, WeightType, ScaleType, out var timeAxis, out var freqAxis);
                // z[time, freq]：列=频率（横轴），行=时间（纵轴）
                var chart = ChartDisplayPayloadFactory.Heatmap(z, freqAxis, timeAxis, "频率 (Hz)", "时间 (s)");
                return Task.FromResult(AlgorithmResultPublisher.SuccessWithChart(context, Id, "TimeFrequencyMap", chart, tag: "map"));
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

    /// <summary>阶次截面。</summary>
    public sealed class OrderSectionNode : AlgorithmNodeWithRpmBase
    {
        private double _referenceValue = AlgorithmScaleReferenceDefaults.ForDb;
        private Scale _scaleType = Scale.dB;

        public OrderSectionNode() : base(nameof(OrderSectionNode), "阶次截面")
        {
        }

        [Display(Name = "谱线数", GroupName = "参数", Order = 0)]
        public int SpectrumLines { get; set; } = 1024;

        [Display(Name = "目标阶次", GroupName = "参数", Order = 1)]
        public double TargetOrder { get; set; } = 1;

        [Display(Name = "阶次带宽", GroupName = "参数", Order = 2)]
        public double OrderBandwidth { get; set; } = 0.5;

        [Display(Name = "最小转速", GroupName = "参数", Order = 3)]
        public double MinRpm { get; set; } = 500;

        [Display(Name = "最大转速", GroupName = "参数", Order = 4)]
        public double MaxRpm { get; set; } = 6000;

        [Display(Name = "转速步长", GroupName = "参数", Order = 5)]
        public double RpmStep { get; set; } = 10;

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

        [Display(Name = "幅值格式", GroupName = "参数", Order = 7)]
        public Format SpectrumFormat { get; set; } = Format.Rms;

        [Display(Name = "窗函数", GroupName = "参数", Order = 8)]
        public Window WindowType { get; set; } = Window.Hanning;

        [Display(Name = "计权", GroupName = "参数", Order = 9)]
        public Weight WeightType { get; set; } = Weight.A;

        [Display(Name = "刻度", GroupName = "参数", Order = 10)]
        public Scale ScaleType
        {
            get => _scaleType;
            set => SetScaleWithReferenceSync(ref _scaleType, value, ref _referenceValue, nameof(ScaleType), nameof(ReferenceValue));
        }

        [Display(Name = "转速触发", GroupName = "参数", Order = 11)]
        public RpmTrigger RpmTriggerType { get; set; } = RpmTrigger.Up;

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            if (!AlgorithmInputLoader.TryLoadVibration(context, Id, DataAcquisitionDeviceName, ResolveChannelKey(),
                    out var file, out var signal, out var disposeSig, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            if (!AlgorithmInputLoader.TryLoadRpm(file, ResolveRpmChannelKey(), out var rpm, out var disposeRpm, out var errRpm))
                return Task.FromResult(ExecutionResult.Failed(errRpm ?? "转速"));

            try
            {
                var data = Nvh.OrderSection(signal, rpm, SpectrumLines, TargetOrder, OrderBandwidth,
                    MinRpm, MaxRpm, RpmStep, ReferenceValue, SpectrumFormat, WindowType, WeightType, ScaleType,
                    RpmTriggerType, out var rpmAxis);
                var chart = ChartDisplayPayloadFactory.XYLine(rpmAxis, data, "转速 (RPM)", "幅值");
                return Task.FromResult(AlgorithmResultPublisher.SuccessWithChart(context, Id, "OrderSection", chart, tag: "order"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(ExecutionResult.Failed(ex.Message, ex));
            }
            finally
            {
                disposeSig();
                disposeRpm();
            }
        }
    }

    /// <summary>转速-频率图。</summary>
    public sealed class RpmFrequencyMapNode : AlgorithmNodeWithRpmBase
    {
        private double _referenceValue = AlgorithmScaleReferenceDefaults.ForDb;
        private Scale _scaleType = Scale.dB;

        public RpmFrequencyMapNode() : base(nameof(RpmFrequencyMapNode), "转速频率图")
        {
        }

        [Display(Name = "谱线数", GroupName = "参数", Order = 0)]
        public int SpectrumLines { get; set; } = 1024;

        [Display(Name = "最小转速", GroupName = "参数", Order = 1)]
        public double MinRpm { get; set; } = 500;

        [Display(Name = "最大转速", GroupName = "参数", Order = 2)]
        public double MaxRpm { get; set; } = 6000;

        [Display(Name = "转速步长", GroupName = "参数", Order = 3)]
        public double RpmStep { get; set; } = 10;

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

        [Display(Name = "幅值格式", GroupName = "参数", Order = 5)]
        public Format SpectrumFormat { get; set; } = Format.Rms;

        [Display(Name = "窗函数", GroupName = "参数", Order = 6)]
        public Window WindowType { get; set; } = Window.Hanning;

        [Display(Name = "计权", GroupName = "参数", Order = 7)]
        public Weight WeightType { get; set; } = Weight.A;

        [Display(Name = "刻度", GroupName = "参数", Order = 8)]
        public Scale ScaleType
        {
            get => _scaleType;
            set => SetScaleWithReferenceSync(ref _scaleType, value, ref _referenceValue, nameof(ScaleType), nameof(ReferenceValue));
        }

        [Display(Name = "转速触发", GroupName = "参数", Order = 9)]
        public RpmTrigger RpmTriggerType { get; set; } = RpmTrigger.Up;

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            if (!AlgorithmInputLoader.TryLoadVibration(context, Id, DataAcquisitionDeviceName, ResolveChannelKey(),
                    out var file, out var signal, out var disposeSig, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            if (!AlgorithmInputLoader.TryLoadRpm(file, ResolveRpmChannelKey(), out var rpm, out var disposeRpm, out var errRpm))
                return Task.FromResult(ExecutionResult.Failed(errRpm ?? "转速"));

            try
            {
                var z = Nvh.RpmFrequencyMap(signal, rpm, SpectrumLines, MinRpm, MaxRpm, RpmStep, ReferenceValue,
                    SpectrumFormat, WindowType, WeightType, ScaleType, RpmTriggerType, out var rpmAxis, out var freqAxis);
                var chart = ChartDisplayPayloadFactory.Heatmap(z, freqAxis, rpmAxis, "频率 (Hz)", "转速 (RPM)");
                return Task.FromResult(AlgorithmResultPublisher.SuccessWithChart(context, Id, "RpmFrequencyMap", chart, tag: "map"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(ExecutionResult.Failed(ex.Message, ex));
            }
            finally
            {
                disposeSig();
                disposeRpm();
            }
        }
    }

    /// <summary>转速-阶次图。</summary>
    public sealed class RpmOrderMapNode : AlgorithmNodeWithRpmBase
    {
        private double _referenceValue = AlgorithmScaleReferenceDefaults.ForDb;
        private Scale _scaleType = Scale.dB;

        public RpmOrderMapNode() : base(nameof(RpmOrderMapNode), "转速阶次图")
        {
        }

        [Display(Name = "最大阶次", GroupName = "参数", Order = 0)]
        public double MaxOrder { get; set; } = 64;

        [Display(Name = "阶次分辨率", GroupName = "参数", Order = 1)]
        public double OrderResolution { get; set; } = 0.5;

        [Display(Name = "最小转速", GroupName = "参数", Order = 2)]
        public double MinRpm { get; set; } = 500;

        [Display(Name = "最大转速", GroupName = "参数", Order = 3)]
        public double MaxRpm { get; set; } = 6000;

        [Display(Name = "转速步长", GroupName = "参数", Order = 4)]
        public double RpmStep { get; set; } = 10;

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

        [Display(Name = "窗函数", GroupName = "参数", Order = 7)]
        public Window WindowType { get; set; } = Window.Hanning;

        [Display(Name = "计权", GroupName = "参数", Order = 8)]
        public Weight WeightType { get; set; } = Weight.A;

        [Display(Name = "刻度", GroupName = "参数", Order = 9)]
        public Scale ScaleType
        {
            get => _scaleType;
            set => SetScaleWithReferenceSync(ref _scaleType, value, ref _referenceValue, nameof(ScaleType), nameof(ReferenceValue));
        }

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            if (!AlgorithmInputLoader.TryLoadVibration(context, Id, DataAcquisitionDeviceName, ResolveChannelKey(),
                    out var file, out var signal, out var disposeSig, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            if (!AlgorithmInputLoader.TryLoadRpm(file, ResolveRpmChannelKey(), out var rpm, out var disposeRpm, out var errRpm))
                return Task.FromResult(ExecutionResult.Failed(errRpm ?? "转速"));

            try
            {
                var z = Nvh.RpmOrderMap(signal, rpm, MaxOrder, OrderResolution, MinRpm, MaxRpm, RpmStep, ReferenceValue,
                    SpectrumFormat, WindowType, WeightType, ScaleType, out var rpmAxis, out var orderAxis);
                var chart = ChartDisplayPayloadFactory.Heatmap(z, orderAxis, rpmAxis, "阶次", "转速 (RPM)");
                return Task.FromResult(AlgorithmResultPublisher.SuccessWithChart(context, Id, "RpmOrderMap", chart, tag: "map"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(ExecutionResult.Failed(ex.Message, ex));
            }
            finally
            {
                disposeSig();
                disposeRpm();
            }
        }
    }
}
