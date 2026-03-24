using System;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Engine.Execution.WorkFlowEngine.Management
{
    /// <summary>
    /// 工作流执行控制器：提供暂停、恢复、取消能力。
    /// </summary>
    public sealed class WorkFlowExecutionController : IDisposable
    {
        private readonly CancellationTokenSource _internalCts = new CancellationTokenSource();
        private readonly AsyncManualResetEvent _pauseGate = new AsyncManualResetEvent(initialState: true);
        private readonly object _stateLock = new object();
        private bool _isPaused;

        public bool IsPaused
        {
            get
            {
                lock (_stateLock)
                {
                    return _isPaused;
                }
            }
        }

        public CancellationToken Token => _internalCts.Token;

        public void Pause()
        {
            lock (_stateLock)
            {
                if (_isPaused) return;
                _isPaused = true;
                _pauseGate.Reset();
            }
        }

        public void Resume()
        {
            lock (_stateLock)
            {
                if (!_isPaused) return;
                _isPaused = false;
                _pauseGate.Set();
            }
        }

        public void Cancel()
        {
            _internalCts.Cancel();
            _pauseGate.Set(); // 防止暂停态下无法响应取消
        }

        public async Task WaitIfPausedAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _pauseGate.WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }

        public void Dispose()
        {
            _internalCts.Dispose();
        }
    }

    internal sealed class AsyncManualResetEvent
    {
        private readonly object _lock = new object();
        private volatile TaskCompletionSource<bool> _tcs;

        public AsyncManualResetEvent(bool initialState)
        {
            _tcs = CreateTcs();
            if (initialState)
            {
                _tcs.TrySetResult(true);
            }
        }

        public Task WaitAsync(CancellationToken cancellationToken)
        {
            var waitTask = _tcs.Task;
            if (!cancellationToken.CanBeCanceled || waitTask.IsCompleted)
            {
                return waitTask;
            }

            return waitTask.WaitAsync(cancellationToken);
        }

        public void Set()
        {
            _tcs.TrySetResult(true);
        }

        public void Reset()
        {
            lock (_lock)
            {
                if (_tcs.Task.IsCompleted)
                {
                    _tcs = CreateTcs();
                }
            }
        }

        private static TaskCompletionSource<bool> CreateTcs()
        {
            return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
