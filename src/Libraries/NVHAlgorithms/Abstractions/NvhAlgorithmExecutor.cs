namespace NVHAlgorithms.Abstractions;

/// <summary>单任务调用时合成「用户取消 + <see cref="NvhRunOptions"/> 超时」。</summary>
public static class NvhAlgorithmExecutor
{
    public static async Task<NvhAlgorithmResult<TOut>> ExecuteAsync<TIn, TOut>(
        INvhAlgorithm<TIn, TOut> algorithm,
        TIn input,
        NvhRunOptions? runOptions,
        CancellationToken cancellationToken = default)
    {
        var opt = runOptions ?? new NvhRunOptions();
        TimeSpan? timeout = opt.SuppressDefaultTimeout
            ? null
            : opt.Timeout
              ?? algorithm.Descriptor.DefaultTimeoutOverride
              ?? NvhExecutionDefaults.AlgorithmTimeout;

        CancellationTokenSource? timeoutCts = null;
        try
        {
            CancellationToken taskCt;
            if (timeout == null || timeout == Timeout.InfiniteTimeSpan)
            {
                taskCt = cancellationToken;
            }
            else
            {
                timeoutCts = new CancellationTokenSource();
                timeoutCts.CancelAfter(timeout.Value);
                taskCt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token).Token;
            }

            return await algorithm.RunAsync(input, opt, taskCt).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested)
        {
            return NvhAlgorithmResult<TOut>.TimedOut();
        }
    }
}
