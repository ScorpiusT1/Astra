using Astra.UI.Abstractions.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace Astra.Services.UI;

/// <summary>
/// 主窗口忙碌遮罩状态（单例；支持嵌套 <see cref="BeginBusy"/>）。
/// </summary>
public sealed class BusyService : ObservableObject, IBusyService
{
    private readonly object _lock = new();
    private readonly Stack<BusyScopeContext> _stack = new();

    public bool IsBusy
    {
        get
        {
            lock (_lock) return _stack.Count > 0;
        }
    }

    public string BusyMessage
    {
        get
        {
            lock (_lock) return _stack.Count > 0 ? _stack.Peek().Message : string.Empty;
        }
    }

    public bool ShowCancelButton
    {
        get
        {
            lock (_lock) return _stack.Count > 0 && _stack.Peek().Options.AllowCancel;
        }
    }

    public string CancelButtonText
    {
        get
        {
            lock (_lock)
            {
                if (_stack.Count == 0)
                    return "取消";
                var t = _stack.Peek().Options.CancelButtonText;
                return string.IsNullOrWhiteSpace(t) ? "取消" : t.Trim();
            }
        }
    }

    /// <summary>最内层忙碌域的取消令牌；无 CTS 时为 <see cref="CancellationToken.None"/>。</summary>
    public CancellationToken CurrentCancellationToken
    {
        get
        {
            lock (_lock)
            {
                if (_stack.Count == 0)
                    return CancellationToken.None;
                return _stack.Peek().Cts?.Token ?? CancellationToken.None;
            }
        }
    }

    public IDisposable BeginBusy(string message, BusyRequestOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(message))
            message = "请稍候…";

        var opt = options ?? new BusyRequestOptions();
        var cts = opt.AllowCancel ? new CancellationTokenSource() : null;
        var ctx = new BusyScopeContext(message.Trim(), opt, cts);

        lock (_lock)
        {
            _stack.Push(ctx);
        }

        NotifyUi();
        return new BusyScope(this, ctx);
    }

    public void RequestCancel()
    {
        BusyScopeContext? top;
        lock (_lock)
        {
            if (_stack.Count == 0)
                return;
            top = _stack.Peek();
        }

        if (!top.Options.AllowCancel)
            return;

        if (top.Options.ConfirmBeforeCancel)
        {
            var msg = string.IsNullOrWhiteSpace(top.Options.ConfirmCancelMessage)
                ? "确定要取消当前操作吗？"
                : top.Options.ConfirmCancelMessage.Trim();
            var owner = Application.Current?.MainWindow;
            var result = owner != null
                ? MessageBox.Show(owner, msg, "确认", MessageBoxButton.YesNo, MessageBoxImage.Question)
                : MessageBox.Show(msg, "确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;
        }

        try
        {
            top.Cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // ignore
        }
    }

    private void PopScope(BusyScopeContext ctx)
    {
        lock (_lock)
        {
            if (_stack.Count == 0)
                return;
            if (!ReferenceEquals(_stack.Peek(), ctx))
                throw new InvalidOperationException("忙碌域必须按嵌套顺序释放（与 BeginBusy 的 using 配对）。");
            _stack.Pop();
            ctx.DisposeCts();
        }

        NotifyUi();
    }

    private void NotifyUi()
    {
        var d = Application.Current?.Dispatcher;
        if (d == null)
        {
            RaiseAll();
            return;
        }

        if (d.CheckAccess())
            RaiseAll();
        else
            d.BeginInvoke(RaiseAll, DispatcherPriority.Normal);
    }

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(BusyMessage));
        OnPropertyChanged(nameof(ShowCancelButton));
        OnPropertyChanged(nameof(CancelButtonText));
        OnPropertyChanged(nameof(CurrentCancellationToken));
    }

    private sealed class BusyScopeContext
    {
        public BusyScopeContext(string message, BusyRequestOptions options, CancellationTokenSource? cts)
        {
            Message = message;
            Options = options;
            Cts = cts;
        }

        public string Message { get; }
        public BusyRequestOptions Options { get; }
        public CancellationTokenSource? Cts { get; }

        public void DisposeCts()
        {
            try
            {
                Cts?.Dispose();
            }
            catch
            {
                // ignore
            }
        }
    }

    private sealed class BusyScope : IDisposable
    {
        private BusyService? _owner;
        private readonly BusyScopeContext _ctx;
        private bool _disposed;

        public BusyScope(BusyService owner, BusyScopeContext ctx)
        {
            _owner = owner;
            _ctx = ctx;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _owner?.PopScope(_ctx);
            _owner = null;
        }
    }
}
