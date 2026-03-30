using Astra.Core.Nodes.Models;
using Astra.Plugins.Algorithms.Helpers;
using Astra.Plugins.Algorithms.APIs;
using Astra.Plugins.Algorithms.Enums;
using Astra.UI.Abstractions.Nodes;
using System.ComponentModel.DataAnnotations;

namespace Astra.Plugins.Algorithms.Nodes
{
    /// <summary>稳态响度。</summary>
    public sealed class StationaryLoudnessNode : AlgorithmNodeBase
    {
        public StationaryLoudnessNode() : base(nameof(StationaryLoudnessNode), "稳态响度")
        {
        }

        [Display(Name = "声场", GroupName = "参数", Order = 0)]
        public SoundField SoundField { get; set; } = SoundField.Free;

        [Display(Name = "跳过时长 (s)", GroupName = "参数", Order = 1)]
        public double SkipSeconds { get; set; }

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            if (!AlgorithmInputLoader.TryLoadVibration(context, Id, DataAcquisitionDeviceName, ResolveChannelKey(),
                    out _, out var signal, out var dispose, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            try
            {
                var (loudness, spec) = Nvh.StationaryLoudnessAnalyze(signal, SoundField, SkipSeconds, out var barkAxis, out _);
                var labels = barkAxis.Select(b => b.ToString("G4")).ToArray();
                var chart = ChartDisplayPayloadFactory.Bar(labels, spec, "Bark", "特定响度");
                var scalars = new List<(string Name, double Value, string Unit)>
                {
                    ("整体响度", loudness, "sone")
                };
                return Task.FromResult(AlgorithmResultPublisher.SuccessWithChartAndScalars(context, Id, "StationaryLoudness", chart, scalars, tag: "psycho"));
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

    /// <summary>时变响度。</summary>
    public sealed class TimeVaryingLoudnessNode : AlgorithmNodeBase
    {
        public TimeVaryingLoudnessNode() : base(nameof(TimeVaryingLoudnessNode), "时变响度")
        {
        }

        [Display(Name = "声场", GroupName = "参数", Order = 0)]
        public SoundField SoundField { get; set; } = SoundField.Free;

        [Display(Name = "跳过时长 (s)", GroupName = "参数", Order = 1)]
        public double SkipSeconds { get; set; }

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            if (!AlgorithmInputLoader.TryLoadVibration(context, Id, DataAcquisitionDeviceName, ResolveChannelKey(),
                    out _, out var signal, out var dispose, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            try
            {
                var (_, spec) = Nvh.TimeVaryingLoudnessAnalyze(signal, SoundField, SkipSeconds, out var barkAxis, out _, out var timeAxis);
                var chart = ChartDisplayPayloadFactory.Heatmap(spec, timeAxis, barkAxis, "时间 (s)", "Bark");
                return Task.FromResult(AlgorithmResultPublisher.SuccessWithChart(context, Id, "TimeVaryingLoudness", chart, tag: "psycho"));
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

    /// <summary>稳态锐度。</summary>
    public sealed class StationarySharpnessNode : AlgorithmNodeBase
    {
        public StationarySharpnessNode() : base(nameof(StationarySharpnessNode), "稳态锐度")
        {
        }

        [Display(Name = "锐度加权", GroupName = "参数", Order = 0)]
        public SharpnessWeighting SharpnessWeighting { get; set; } = SharpnessWeighting.Din;

        [Display(Name = "声场", GroupName = "参数", Order = 1)]
        public SoundField SoundField { get; set; } = SoundField.Free;

        [Display(Name = "跳过时长 (s)", GroupName = "参数", Order = 2)]
        public double SkipSeconds { get; set; }

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            if (!AlgorithmInputLoader.TryLoadVibration(context, Id, DataAcquisitionDeviceName, ResolveChannelKey(),
                    out _, out var signal, out var dispose, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            try
            {
                var sharp = Nvh.StationarySharpnessAnalyze(signal, SharpnessWeighting, SoundField, SkipSeconds, out var spec, out var barkAxis, out _);
                var chart = ChartDisplayPayloadFactory.XYLine(barkAxis, spec, "Bark", "特定锐度");
                var scalars = new List<(string Name, double Value, string Unit)> { ("锐度", sharp, "acum") };
                return Task.FromResult(AlgorithmResultPublisher.SuccessWithChartAndScalars(context, Id, "StationarySharpness", chart, scalars, tag: "psycho"));
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

    /// <summary>时变锐度。</summary>
    public sealed class TimeVaryingSharpnessNode : AlgorithmNodeBase
    {
        public TimeVaryingSharpnessNode() : base(nameof(TimeVaryingSharpnessNode), "时变锐度")
        {
        }

        [Display(Name = "锐度加权", GroupName = "参数", Order = 0)]
        public SharpnessWeighting SharpnessWeighting { get; set; } = SharpnessWeighting.Din;

        [Display(Name = "声场", GroupName = "参数", Order = 1)]
        public SoundField SoundField { get; set; } = SoundField.Free;

        [Display(Name = "跳过时长 (s)", GroupName = "参数", Order = 2)]
        public double SkipSeconds { get; set; }

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            if (!AlgorithmInputLoader.TryLoadVibration(context, Id, DataAcquisitionDeviceName, ResolveChannelKey(),
                    out _, out var signal, out var dispose, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            try
            {
                var sharpSeries = Nvh.TimeVaryingSharpnessAnalyze(signal, SharpnessWeighting, SoundField, SkipSeconds, out var spec2d, out var barkAxis, out _, out var timeAxis);
                var chart = ChartDisplayPayloadFactory.Heatmap(spec2d, timeAxis, barkAxis, "时间 (s)", "Bark");
                var meanSharp = sharpSeries.Length > 0 ? sharpSeries.Average() : double.NaN;
                var scalars = new List<(string Name, double Value, string Unit)> { ("平均锐度", meanSharp, "acum") };
                return Task.FromResult(AlgorithmResultPublisher.SuccessWithChartAndScalars(context, Id, "TimeVaryingSharpness", chart, scalars, tag: "psycho"));
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

    /// <summary>粗糙度。</summary>
    public sealed class RoughnessNode : AlgorithmNodeBase
    {
        public RoughnessNode() : base(nameof(RoughnessNode), "粗糙度")
        {
        }

        [Display(Name = "声场", GroupName = "参数", Order = 0)]
        public SoundField SoundField { get; set; } = SoundField.Free;

        [Display(Name = "跳过时长 (s)", GroupName = "参数", Order = 1)]
        public double SkipSeconds { get; set; }

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            if (!AlgorithmInputLoader.TryLoadVibration(context, Id, DataAcquisitionDeviceName, ResolveChannelKey(),
                    out _, out var signal, out var dispose, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            try
            {
                var overall = Nvh.RoughnessAnalyze(signal, SoundField, SkipSeconds, out _, out var spec2d, out _, out var bandAxis, out _, out _, out var timeAxis);
                var chart = ChartDisplayPayloadFactory.Heatmap(spec2d, timeAxis, bandAxis, "时间 (s)", "频带");
                var scalars = new List<(string Name, double Value, string Unit)> { ("粗糙度", overall, "asper") };
                return Task.FromResult(AlgorithmResultPublisher.SuccessWithChartAndScalars(context, Id, "Roughness", chart, scalars, tag: "psycho"));
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

    /// <summary>波动度。</summary>
    public sealed class FluctuationStrengthNode : AlgorithmNodeBase
    {
        public FluctuationStrengthNode() : base(nameof(FluctuationStrengthNode), "波动度")
        {
        }

        [Display(Name = "方法", GroupName = "参数", Order = 0)]
        public FluctuationMethod Method { get; set; } = FluctuationMethod.Stationary;

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            if (!AlgorithmInputLoader.TryLoadVibration(context, Id, DataAcquisitionDeviceName, ResolveChannelKey(),
                    out _, out var signal, out var dispose, out var err))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            try
            {
                var total = Nvh.FluctuationStrengthAnalyze(signal, Method, out _, out var spec2d, out _, out var bandAxis, out _, out var timeAxis);
                var chart = ChartDisplayPayloadFactory.Heatmap(spec2d, timeAxis, bandAxis, "时间 (s)", "频带");
                var scalars = new List<(string Name, double Value, string Unit)> { ("波动度", total, "vacil") };
                return Task.FromResult(AlgorithmResultPublisher.SuccessWithChartAndScalars(context, Id, "FluctuationStrength", chart, scalars, tag: "psycho"));
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
