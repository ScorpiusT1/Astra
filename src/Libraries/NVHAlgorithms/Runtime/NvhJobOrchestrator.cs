using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NVHAlgorithms.Abstractions;

namespace NVHAlgorithms.Runtime;

/// <summary>
/// 多算法并行编排：每任务独立超时、默认尽力完成其余任务；可选首失败后取消同批。
/// </summary>
public sealed class NvhJobOrchestrator
{
    private readonly ILogger<NvhJobOrchestrator>? _logger;

    public NvhJobOrchestrator(ILogger<NvhJobOrchestrator>? logger = null)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<NvhSingleTaskResult>> RunBatchAsync(
        IReadOnlyList<NvhAlgorithmWorkItem> items,
        NvhParallelOptions? parallelOptions,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
            return Array.Empty<NvhSingleTaskResult>();

        var opt = parallelOptions ?? new NvhParallelOptions();
        using var batchFailCts = new CancellationTokenSource();
        CancellationToken parentToken = cancellationToken;
        if (!opt.ContinueOnTaskFailure)
            parentToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, batchFailCts.Token).Token;

        var parallelOptionsTpl = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, opt.MaxDegreeOfParallelism),
            CancellationToken = cancellationToken,
        };

        var bag = new ConcurrentBag<NvhSingleTaskResult>();

        try
        {
            await Parallel.ForEachAsync(items, parallelOptionsTpl, async (item, ct) =>
            {
                if (opt.ContinueOnTaskFailure == false && batchFailCts.IsCancellationRequested)
                    return;

                var timeout = ResolveTimeout(item.Descriptor, opt);
                CancellationTokenSource? timeoutCts = null;
                if (timeout != Timeout.InfiniteTimeSpan)
                {
                    timeoutCts = new CancellationTokenSource();
                    timeoutCts.CancelAfter(timeout);
                }

                var taskToken = timeoutCts == null
                    ? parentToken
                    : CancellationTokenSource.CreateLinkedTokenSource(parentToken, timeoutCts.Token).Token;

                NvhSingleTaskResult result;
                try
                {
                    _logger?.LogInformation(
                        "NVH task start {TaskId} {AlgorithmId} schema v{Schema}",
                        item.TaskId,
                        item.Descriptor.AlgorithmId,
                        item.Descriptor.SchemaVersion);

                    result = await item.Execute(taskToken).ConfigureAwait(false);

                    _logger?.LogInformation(
                        "NVH task end {TaskId} state {State}",
                        item.TaskId,
                        result.State);
                }
                catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested && timeout != Timeout.InfiniteTimeSpan)
                {
                    result = new NvhSingleTaskResult
                    {
                        TaskId = item.TaskId,
                        Descriptor = item.Descriptor,
                        State = NvhTaskCompletionState.TimedOut,
                        Error = new TimeoutException($"任务 {item.TaskId} 超过 {timeout.TotalSeconds} s。"),
                    };
                }
                catch (OperationCanceledException)
                {
                    result = new NvhSingleTaskResult
                    {
                        TaskId = item.TaskId,
                        Descriptor = item.Descriptor,
                        State = NvhTaskCompletionState.Canceled,
                    };
                }
                catch (Exception ex)
                {
                    result = new NvhSingleTaskResult
                    {
                        TaskId = item.TaskId,
                        Descriptor = item.Descriptor,
                        State = NvhTaskCompletionState.Failed,
                        Error = ex,
                    };
                }
                finally
                {
                    timeoutCts?.Dispose();
                }

                bag.Add(result);

                if (!opt.ContinueOnTaskFailure && result.State is NvhTaskCompletionState.Failed or NvhTaskCompletionState.TimedOut)
                    batchFailCts.Cancel();
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }

        return bag.ToList();
    }

    private static TimeSpan ResolveTimeout(NvhAlgorithmDescriptor descriptor, NvhParallelOptions opt)
    {
        if (opt.SuppressDefaultPerTaskTimeout)
            return Timeout.InfiniteTimeSpan;

        return opt.PerTaskTimeout
               ?? descriptor.DefaultTimeoutOverride
               ?? NvhExecutionDefaults.AlgorithmTimeout;
    }
}
