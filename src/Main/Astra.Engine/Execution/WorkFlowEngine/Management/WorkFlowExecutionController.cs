using Astra.Core.Nodes.Management;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Engine.Execution.WorkFlowEngine.Management
{
    /// <summary>
    /// 工作流执行控制器：提供暂停、恢复、取消能力。
    /// </summary>
    public sealed class WorkFlowExecutionController : IWorkflowExecutionController, IDisposable
    {
        private readonly CancellationTokenSource _internalCts = new CancellationTokenSource();
        private readonly AsyncManualResetEvent _pauseGate = new AsyncManualResetEvent(initialState: true);
        private readonly object _stateLock = new object();
        private bool _isPaused;
        private TimeSpan _accumulatedPausedDuration = TimeSpan.Zero;
        private DateTime? _pauseStartedAtUtc;

        /// <inheritdoc />
        public event EventHandler? Pausing;

        /// <inheritdoc />
        public event EventHandler? Resumed;

        /// <inheritdoc />
        public event EventHandler? Cancelling;

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

        /// <summary>
        /// 总暂停时长（若当前处于暂停中，包含本次暂停进行中的时长）。
        /// </summary>
        public TimeSpan TotalPausedDuration
        {
            get
            {
                lock (_stateLock)
                {
                    var total = _accumulatedPausedDuration;
                    if (_isPaused && _pauseStartedAtUtc.HasValue)
                    {
                        total += DateTime.UtcNow - _pauseStartedAtUtc.Value;
                    }

                    return total;
                }
            }
        }

        public void Pause()
        {
            var becamePaused = false;
            lock (_stateLock)
            {
                if (_isPaused) return;
                _isPaused = true;
                _pauseStartedAtUtc = DateTime.UtcNow;
                _pauseGate.Reset();
                becamePaused = true;
            }

            // 锁外触发，避免订阅方回调再次进入控制器导致死锁
            if (becamePaused)
            {
                var h = Pausing;
                h?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Resume()
        {
            var becameRunning = false;
            lock (_stateLock)
            {
                if (!_isPaused) return;
                _isPaused = false;
                if (_pauseStartedAtUtc.HasValue)
                {
                    _accumulatedPausedDuration += DateTime.UtcNow - _pauseStartedAtUtc.Value;
                    _pauseStartedAtUtc = null;
                }
                _pauseGate.Set();
                becameRunning = true;
            }

            if (becameRunning)
            {
                var h = Resumed;
                h?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Cancel()
        {
            var firstRequest = !_internalCts.IsCancellationRequested;
            _internalCts.Cancel();
            _pauseGate.Set(); // 防止暂停态下无法响应取消

            if (firstRequest)
            {
                var h = Cancelling;
                h?.Invoke(this, EventArgs.Empty);
            }
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
