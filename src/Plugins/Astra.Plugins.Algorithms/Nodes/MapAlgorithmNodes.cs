using Astra.Core.Nodes.Models;
using Astra.Plugins.Algorithms.Helpers;
using Astra.Plugins.Algorithms.APIs;
using Astra.UI.Abstractions.Nodes;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

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

        protected override IEnumerable<string> EnumerateDesignTimeScalarLogicalNames(string channelLabel)
        {
            yield return $"时频图峰值({channelLabel})";
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
                    var z = Nvh.TimeFrequencyMap(e.Signal, SpectrumLines, TimeIncrementSeconds, ReferenceValue,
                        SpectrumFormat, WindowType, WeightType, ScaleType, out var timeAxis, out var freqAxis);
                    var chart = ChartDisplayPayloadFactory.Heatmap(z, freqAxis, timeAxis, "频率 (Hz)", "时间 (s)");
                    results[i] = (e.Label, chart, AlgorithmScalarMath.Max(z));
                });
                var charts = results.Select(r => (r.Label, r.Chart)).ToList();
                var chartResult = PublishMultiChart(context, "TimeFrequencyMap", charts, tag: "map");
                var scalars = results.Select(r => ($"时频图峰值({r.Label})", r.Peak, string.Empty)).ToList();
                return Task.FromResult(AppendScalarsToChartResult(context, chartResult, scalars, "map", "TimeFrequencyMap"));
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

        protected override IEnumerable<string> EnumerateDesignTimeScalarLogicalNames(string channelLabel)
        {
            yield return $"阶次截面峰值({channelLabel})";
        }

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var specs = ResolveInputSpecs();
            if (specs.Count == 0)
                return Task.FromResult(ExecutionResult.Failed("请至少选择一个通道，或确保上游存在可用采集卡（未选通道时将使用各卡首通道）。"));

            if (!AlgorithmInputLoader.TryLoadMultipleVibrations(context, Id, specs, out var entries, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            var (_, rpmChannel) = ResolveRpmSpec();

            var rpms = new (Rpm rpm, Action dispose)[entries.Count];
            for (int j = 0; j < entries.Count; j++)
            {
                if (!AlgorithmInputLoader.TryLoadRpm(entries[j].File, rpmChannel, out var rpm, out var disposeRpm, out var errRpm))
                {
                    for (int k = 0; k < j; k++) rpms[k].dispose();
                    AlgorithmInputLoader.DisposeAll(entries);
                    return Task.FromResult(ExecutionResult.Failed(errRpm ?? "转速"));
                }
                rpms[j] = (rpm, disposeRpm);
            }

            try
            {
                var results = new (string Label, ChartDisplayPayload Chart, double Peak)[entries.Count];
                Parallel.For(0, entries.Count, i =>
                {
                    var e = entries[i];
                    var data = Nvh.OrderSection(e.Signal, rpms[i].rpm, SpectrumLines, TargetOrder, OrderBandwidth,
                        MinRpm, MaxRpm, RpmStep, ReferenceValue, SpectrumFormat, WindowType, WeightType, ScaleType,
                        RpmTriggerType, out var rpmAxis);
                    var chart = ChartDisplayPayloadFactory.XYLine(rpmAxis, data, "转速 (RPM)", "幅值");
                    results[i] = (e.Label, chart, AlgorithmScalarMath.MaxAbs(data));
                });
                var charts = results.Select(r => (r.Label, r.Chart)).ToList();
                var chartResult = PublishMultiChart(context, "OrderSection", charts, tag: "order");
                var scalars = results.Select(r => ($"阶次截面峰值({r.Label})", r.Peak, string.Empty)).ToList();
                return Task.FromResult(AppendScalarsToChartResult(context, chartResult, scalars, "order", "OrderSection"));
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
                foreach (var (_, dispose) in rpms) dispose();
                AlgorithmInputLoader.DisposeAll(entries);
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

        protected override IEnumerable<string> EnumerateDesignTimeScalarLogicalNames(string channelLabel)
        {
            yield return $"转速频率图峰值({channelLabel})";
        }

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var specs = ResolveInputSpecs();
            if (specs.Count == 0)
                return Task.FromResult(ExecutionResult.Failed("请至少选择一个通道，或确保上游存在可用采集卡（未选通道时将使用各卡首通道）。"));

            if (!AlgorithmInputLoader.TryLoadMultipleVibrations(context, Id, specs, out var entries, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            var (_, rpmChannel) = ResolveRpmSpec();

            var rpms = new (Rpm rpm, Action dispose)[entries.Count];
            for (int j = 0; j < entries.Count; j++)
            {
                if (!AlgorithmInputLoader.TryLoadRpm(entries[j].File, rpmChannel, out var rpm, out var disposeRpm, out var errRpm))
                {
                    for (int k = 0; k < j; k++) rpms[k].dispose();
                    AlgorithmInputLoader.DisposeAll(entries);
                    return Task.FromResult(ExecutionResult.Failed(errRpm ?? "转速"));
                }
                rpms[j] = (rpm, disposeRpm);
            }

            try
            {
                var results = new (string Label, ChartDisplayPayload Chart, double Peak)[entries.Count];
                Parallel.For(0, entries.Count, i =>
                {
                    var e = entries[i];
                    var z = Nvh.RpmFrequencyMap(e.Signal, rpms[i].rpm, SpectrumLines, MinRpm, MaxRpm, RpmStep, ReferenceValue,
                        SpectrumFormat, WindowType, WeightType, ScaleType, RpmTriggerType, out var rpmAxis, out var freqAxis);
                    var chart = ChartDisplayPayloadFactory.Heatmap(z, freqAxis, rpmAxis, "频率 (Hz)", "转速 (RPM)");
                    results[i] = (e.Label, chart, AlgorithmScalarMath.Max(z));
                });
                var charts = results.Select(r => (r.Label, r.Chart)).ToList();
                var chartResult = PublishMultiChart(context, "RpmFrequencyMap", charts, tag: "map");
                var scalars = results.Select(r => ($"转速频率图峰值({r.Label})", r.Peak, string.Empty)).ToList();
                return Task.FromResult(AppendScalarsToChartResult(context, chartResult, scalars, "map", "RpmFrequencyMap"));
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
                foreach (var (_, dispose) in rpms) dispose();
                AlgorithmInputLoader.DisposeAll(entries);
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

        protected override IEnumerable<string> EnumerateDesignTimeScalarLogicalNames(string channelLabel)
        {
            yield return $"转速阶次图峰值({channelLabel})";
        }

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var specs = ResolveInputSpecs();
            if (specs.Count == 0)
                return Task.FromResult(ExecutionResult.Failed("请至少选择一个通道，或确保上游存在可用采集卡（未选通道时将使用各卡首通道）。"));

            if (!AlgorithmInputLoader.TryLoadMultipleVibrations(context, Id, specs, out var entries, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            var (_, rpmChannel) = ResolveRpmSpec();

            var rpms = new (Rpm rpm, Action dispose)[entries.Count];
            for (int j = 0; j < entries.Count; j++)
            {
                if (!AlgorithmInputLoader.TryLoadRpm(entries[j].File, rpmChannel, out var rpm, out var disposeRpm, out var errRpm))
                {
                    for (int k = 0; k < j; k++) rpms[k].dispose();
                    AlgorithmInputLoader.DisposeAll(entries);
                    return Task.FromResult(ExecutionResult.Failed(errRpm ?? "转速"));
                }
                rpms[j] = (rpm, disposeRpm);
            }

            try
            {
                var results = new (string Label, ChartDisplayPayload Chart, double Peak)[entries.Count];
                Parallel.For(0, entries.Count, i =>
                {
                    var e = entries[i];
                    var z = Nvh.RpmOrderMap(e.Signal, rpms[i].rpm, MaxOrder, OrderResolution, MinRpm, MaxRpm, RpmStep, ReferenceValue,
                        SpectrumFormat, WindowType, WeightType, ScaleType, out var rpmAxis, out var orderAxis);
                    var chart = ChartDisplayPayloadFactory.Heatmap(z, orderAxis, rpmAxis, "阶次", "转速 (RPM)");
                    results[i] = (e.Label, chart, AlgorithmScalarMath.Max(z));
                });
                var charts = results.Select(r => (r.Label, r.Chart)).ToList();
                var chartResult = PublishMultiChart(context, "RpmOrderMap", charts, tag: "map");
                var scalars = results.Select(r => ($"转速阶次图峰值({r.Label})", r.Peak, string.Empty)).ToList();
                return Task.FromResult(AppendScalarsToChartResult(context, chartResult, scalars, "map", "RpmOrderMap"));
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
                foreach (var (_, dispose) in rpms) dispose();
                AlgorithmInputLoader.DisposeAll(entries);
            }
        }
    }
}
