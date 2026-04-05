using Astra.Core.Nodes.Models;
using Astra.Plugins.Algorithms.APIs;
using Astra.Plugins.Algorithms.Enums;
using Astra.Plugins.Algorithms.Helpers;
using Astra.UI.Abstractions.Attributes;
using Astra.UI.Abstractions.Nodes;
using Astra.Workflow.AlgorithmChannel.Helpers;
using Astra.Workflow.AlgorithmChannel.Nodes;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Astra.Plugins.Algorithms.Nodes
{
    /// <summary>稳态响度。</summary>
    public sealed class StationaryLoudnessNode : AlgorithmNodeBase
    {
        public StationaryLoudnessNode() : base(nameof(StationaryLoudnessNode), "稳态响度")
        {
        }

        [Order(2, 0)]
        [Display(Name = "声场", GroupName = "参数")]
        public SoundField SoundField { get; set; } = SoundField.Free;

        [Display(Name = "跳过时长 (s)", GroupName = "参数", Order = 1)]
        public double SkipSeconds { get; set; }

        protected override IEnumerable<string> EnumerateDesignTimeScalarLogicalNames(string channelLabel)
        {
            yield return $"整体响度({channelLabel})";
        }

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var specs = ResolveInputSpecs();
            if (specs.Count == 0)
                return Task.FromResult(ExecutionResult.Failed("请至少选择一个通道，或确保上游存在可用采集卡（未选通道时将使用各卡首通道）。"));

            if (!AlgorithmInputLoader.TryLoadMultipleVibrations(context, Id, specs, out var entries, out var err, AnalysisWindowStartSeconds, AnalysisWindowEndSeconds))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            try
            {
                var results = new (string Label, ChartDisplayPayload Chart, string ScalarName, double ScalarValue, string ScalarUnit)[entries.Count];
                AlgorithmParallel.For(0, entries.Count, cancellationToken, i =>
                {
                    var e = entries[i];
                    var (loudness, spec) = Nvh.StationaryLoudnessAnalyze(e.Signal, SoundField, SkipSeconds, out var barkAxis, out _);
                    var labels = barkAxis.Select(b => b.ToString("G4")).ToArray();
                    results[i] = (e.Label, ChartDisplayPayloadFactory.Bar(labels, spec, "Bark", "特定响度"),
                        $"整体响度({e.Label})", loudness, "sone");
                });
                var charts = results.Select(r => (r.Label, r.Chart)).ToList();
                var scalars = results.Select(r => (r.ScalarName, r.ScalarValue, r.ScalarUnit)).ToList();
                return Task.FromResult(PublishMultiChartAndScalars(context, "StationaryLoudness", charts, scalars, tag: "psycho"));
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

    /// <summary>时变响度。</summary>
    public sealed class TimeVaryingLoudnessNode : AlgorithmNodeBase
    {
        public TimeVaryingLoudnessNode() : base(nameof(TimeVaryingLoudnessNode), "时变响度")
        {
        }

        [Order(2, 0)]
        [Display(Name = "声场", GroupName = "参数")]
        public SoundField SoundField { get; set; } = SoundField.Free;

        [Display(Name = "跳过时长 (s)", GroupName = "参数", Order = 1)]
        public double SkipSeconds { get; set; }

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var specs = ResolveInputSpecs();
            if (specs.Count == 0)
                return Task.FromResult(ExecutionResult.Failed("请至少选择一个通道，或确保上游存在可用采集卡（未选通道时将使用各卡首通道）。"));

            if (!AlgorithmInputLoader.TryLoadMultipleVibrations(context, Id, specs, out var entries, out var err, AnalysisWindowStartSeconds, AnalysisWindowEndSeconds))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            try
            {
                var results = new (string Label, ChartDisplayPayload Chart)[entries.Count];
                AlgorithmParallel.For(0, entries.Count, cancellationToken, i =>
                {
                    var e = entries[i];
                    var (_, spec) = Nvh.TimeVaryingLoudnessAnalyze(e.Signal, SoundField, SkipSeconds, out var barkAxis, out _, out var timeAxis);
                    AlgorithmTimeAxisHelper.ApplyAnalysisOriginInPlace(timeAxis, e.TimeAxisOriginSeconds);
                    results[i] = (e.Label, ChartDisplayPayloadFactory.Heatmap(spec, timeAxis, barkAxis, "时间 (s)", "Bark"));
                });
                return Task.FromResult(PublishMultiChart(context, "TimeVaryingLoudness", results.ToList(), tag: "psycho"));
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

    /// <summary>稳态锐度。</summary>
    public sealed class StationarySharpnessNode : AlgorithmNodeBase
    {
        public StationarySharpnessNode() : base(nameof(StationarySharpnessNode), "稳态锐度")
        {
        }

        [Order(2, 0)]
        [Display(Name = "锐度加权", GroupName = "参数")]
        public SharpnessWeighting SharpnessWeighting { get; set; } = SharpnessWeighting.Din;

        [Display(Name = "声场", GroupName = "参数", Order = 1)]
        public SoundField SoundField { get; set; } = SoundField.Free;

        [Display(Name = "跳过时长 (s)", GroupName = "参数", Order = 2)]
        public double SkipSeconds { get; set; }

        protected override IEnumerable<string> EnumerateDesignTimeScalarLogicalNames(string channelLabel)
        {
            yield return $"锐度({channelLabel})";
        }

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var specs = ResolveInputSpecs();
            if (specs.Count == 0)
                return Task.FromResult(ExecutionResult.Failed("请至少选择一个通道，或确保上游存在可用采集卡（未选通道时将使用各卡首通道）。"));

            if (!AlgorithmInputLoader.TryLoadMultipleVibrations(context, Id, specs, out var entries, out var err, AnalysisWindowStartSeconds, AnalysisWindowEndSeconds))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            try
            {
                var results = new (string Label, ChartDisplayPayload Chart, string ScalarName, double ScalarValue, string ScalarUnit)[entries.Count];
                AlgorithmParallel.For(0, entries.Count, cancellationToken, i =>
                {
                    var e = entries[i];
                    var sharp = Nvh.StationarySharpnessAnalyze(e.Signal, SharpnessWeighting, SoundField, SkipSeconds, out var spec, out var barkAxis, out _);
                    results[i] = (e.Label, ChartDisplayPayloadFactory.XYLine(barkAxis, spec, "Bark", "特定锐度"),
                        $"锐度({e.Label})", sharp, "acum");
                });
                var charts = results.Select(r => (r.Label, r.Chart)).ToList();
                var scalars = results.Select(r => (r.ScalarName, r.ScalarValue, r.ScalarUnit)).ToList();
                return Task.FromResult(PublishMultiChartAndScalars(context, "StationarySharpness", charts, scalars, tag: "psycho"));
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

    /// <summary>时变锐度。</summary>
    public sealed class TimeVaryingSharpnessNode : AlgorithmNodeBase
    {
        public TimeVaryingSharpnessNode() : base(nameof(TimeVaryingSharpnessNode), "时变锐度")
        {
        }

        [Order(2, 0)]
        [Display(Name = "锐度加权", GroupName = "参数")]
        public SharpnessWeighting SharpnessWeighting { get; set; } = SharpnessWeighting.Din;

        [Display(Name = "声场", GroupName = "参数", Order = 1)]
        public SoundField SoundField { get; set; } = SoundField.Free;

        [Display(Name = "跳过时长 (s)", GroupName = "参数", Order = 2)]
        public double SkipSeconds { get; set; }

        protected override IEnumerable<string> EnumerateDesignTimeScalarLogicalNames(string channelLabel)
        {
            yield return $"平均锐度({channelLabel})";
        }

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var specs = ResolveInputSpecs();
            if (specs.Count == 0)
                return Task.FromResult(ExecutionResult.Failed("请至少选择一个通道，或确保上游存在可用采集卡（未选通道时将使用各卡首通道）。"));

            if (!AlgorithmInputLoader.TryLoadMultipleVibrations(context, Id, specs, out var entries, out var err, AnalysisWindowStartSeconds, AnalysisWindowEndSeconds))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            try
            {
                var results = new (string Label, ChartDisplayPayload Chart, string ScalarName, double ScalarValue, string ScalarUnit)[entries.Count];
                AlgorithmParallel.For(0, entries.Count, cancellationToken, i =>
                {
                    var e = entries[i];
                    var sharpSeries = Nvh.TimeVaryingSharpnessAnalyze(e.Signal, SharpnessWeighting, SoundField, SkipSeconds, out var spec2d, out var barkAxis, out _, out var timeAxis);
                    AlgorithmTimeAxisHelper.ApplyAnalysisOriginInPlace(timeAxis, e.TimeAxisOriginSeconds);
                    var meanSharp = sharpSeries.Length > 0 ? sharpSeries.Average() : double.NaN;
                    results[i] = (e.Label, ChartDisplayPayloadFactory.Heatmap(spec2d, timeAxis, barkAxis, "时间 (s)", "Bark"),
                        $"平均锐度({e.Label})", meanSharp, "acum");
                });
                var charts = results.Select(r => (r.Label, r.Chart)).ToList();
                var scalars = results.Select(r => (r.ScalarName, r.ScalarValue, r.ScalarUnit)).ToList();
                return Task.FromResult(PublishMultiChartAndScalars(context, "TimeVaryingSharpness", charts, scalars, tag: "psycho"));
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

    /// <summary>粗糙度。</summary>
    public sealed class RoughnessNode : AlgorithmNodeBase
    {
        public RoughnessNode() : base(nameof(RoughnessNode), "粗糙度")
        {
        }

        [Order(2, 0)]
        [Display(Name = "声场", GroupName = "参数")]
        public SoundField SoundField { get; set; } = SoundField.Free;

        [Display(Name = "跳过时长 (s)", GroupName = "参数", Order = 1)]
        public double SkipSeconds { get; set; }

        protected override IEnumerable<string> EnumerateDesignTimeScalarLogicalNames(string channelLabel)
        {
            yield return $"粗糙度({channelLabel})";
        }

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var specs = ResolveInputSpecs();
            if (specs.Count == 0)
                return Task.FromResult(ExecutionResult.Failed("请至少选择一个通道，或确保上游存在可用采集卡（未选通道时将使用各卡首通道）。"));

            if (!AlgorithmInputLoader.TryLoadMultipleVibrations(context, Id, specs, out var entries, out var err, AnalysisWindowStartSeconds, AnalysisWindowEndSeconds))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            try
            {
                var results = new (string Label, ChartDisplayPayload Chart, string ScalarName, double ScalarValue, string ScalarUnit)[entries.Count];
                AlgorithmParallel.For(0, entries.Count, cancellationToken, i =>
                {
                    var e = entries[i];
                    var overall = Nvh.RoughnessAnalyze(e.Signal, SoundField, SkipSeconds, out _, out var spec2d, out _, out var bandAxis, out _, out _, out var timeAxis);
                    AlgorithmTimeAxisHelper.ApplyAnalysisOriginInPlace(timeAxis, e.TimeAxisOriginSeconds);
                    results[i] = (e.Label, ChartDisplayPayloadFactory.Heatmap(spec2d, timeAxis, bandAxis, "时间 (s)", "频带"),
                        $"粗糙度({e.Label})", overall, "asper");
                });
                var charts = results.Select(r => (r.Label, r.Chart)).ToList();
                var scalars = results.Select(r => (r.ScalarName, r.ScalarValue, r.ScalarUnit)).ToList();
                return Task.FromResult(PublishMultiChartAndScalars(context, "Roughness", charts, scalars, tag: "psycho"));
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

    /// <summary>波动度。</summary>
    public sealed class FluctuationStrengthNode : AlgorithmNodeBase
    {
        public FluctuationStrengthNode() : base(nameof(FluctuationStrengthNode), "波动度")
        {
        }

        [Order(2, 0)]
        [Display(Name = "方法", GroupName = "参数")]
        public FluctuationMethod Method { get; set; } = FluctuationMethod.Stationary;

        protected override IEnumerable<string> EnumerateDesignTimeScalarLogicalNames(string channelLabel)
        {
            yield return $"波动度({channelLabel})";
        }

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var specs = ResolveInputSpecs();
            if (specs.Count == 0)
                return Task.FromResult(ExecutionResult.Failed("请至少选择一个通道，或确保上游存在可用采集卡（未选通道时将使用各卡首通道）。"));

            if (!AlgorithmInputLoader.TryLoadMultipleVibrations(context, Id, specs, out var entries, out var err, AnalysisWindowStartSeconds, AnalysisWindowEndSeconds))
                return Task.FromResult(ExecutionResult.Failed(err ?? "输入错误"));

            try
            {
                var results = new (string Label, ChartDisplayPayload Chart, string ScalarName, double ScalarValue, string ScalarUnit)[entries.Count];
                AlgorithmParallel.For(0, entries.Count, cancellationToken, i =>
                {
                    var e = entries[i];
                    var total = Nvh.FluctuationStrengthAnalyze(e.Signal, Method, out _, out var spec2d, out _, out var bandAxis, out _, out var timeAxis);
                    AlgorithmTimeAxisHelper.ApplyAnalysisOriginInPlace(timeAxis, e.TimeAxisOriginSeconds);
                    results[i] = (e.Label, ChartDisplayPayloadFactory.Heatmap(spec2d, timeAxis, bandAxis, "时间 (s)", "频带"),
                        $"波动度({e.Label})", total, "vacil");
                });
                var charts = results.Select(r => (r.Label, r.Chart)).ToList();
                var scalars = results.Select(r => (r.ScalarName, r.ScalarValue, r.ScalarUnit)).ToList();
                return Task.FromResult(PublishMultiChartAndScalars(context, "FluctuationStrength", charts, scalars, tag: "psycho"));
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
