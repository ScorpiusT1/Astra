using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Engine.Execution.Strategies
{
    /// <summary>
    /// 同一执行层内节点数不超过 <see cref="ParallelOptions.MaxDegreeOfParallelism"/> 时，
    /// 用就绪栅栏统一放行，缩小 <see cref="Parallel.ForEachAsync"/> 线程池调度带来的启动时间差。
    /// </summary>
    internal static class SynchronizedParallelLayerExecutor
    {
        public static async Task ForEachAsync<T>(
            IReadOnlyList<T> items,
            int maxDegreeOfParallelism,
            CancellationToken cancellationToken,
            Func<T, CancellationToken, Task> bodyAsync)
        {
            ArgumentNullException.ThrowIfNull(items);
            if (items.Count == 0)
            {
                return;
            }

            if (items.Count == 1)
            {
                await bodyAsync(items[0], cancellationToken).ConfigureAwait(false);
                return;
            }

            if (items.Count > maxDegreeOfParallelism)
            {
                var fallback = new ParallelOptions
                {
                    MaxDegreeOfParallelism = maxDegreeOfParallelism,
                    CancellationToken = cancellationToken
                };
                await Parallel.ForEachAsync(
                    items,
                    fallback,
                    (item, ct) => new ValueTask(bodyAsync(item, ct))).ConfigureAwait(false);
                return;
            }

            using var ready = new CountdownEvent(items.Count);
            var go = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var workerTasks = new Task[items.Count];
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                workerTasks[i] = Task.Run(async () =>
                {
                    ready.Signal();
                    await go.Task.ConfigureAwait(false);
                    await bodyAsync(item, cancellationToken).ConfigureAwait(false);
                }, cancellationToken);
            }

            try
            {
                await Task.Run(() => ready.Wait(cancellationToken), cancellationToken).ConfigureAwait(false);
                go.TrySetResult(true);
                await Task.WhenAll(workerTasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                go.TrySetCanceled(cancellationToken);
                throw;
            }
        }
    }
}
