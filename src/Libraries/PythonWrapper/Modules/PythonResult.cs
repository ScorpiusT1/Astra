using Python.Runtime;
using PythonWrapper.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PythonWrapper.Modules
{
    // ─────────────────────────────────────────────────────────────  
    // PythonResult — 原始 PyObject 包装器（懒转换）  
    // CallRawAsync 的返回值，携带调用阶段的成功/失败状态  
    // ─────────────────────────────────────────────────────────────  
    public sealed class PythonResult : IAsyncDisposable, IDisposable
    {
        private readonly PyObject _pyObj;
        private readonly PythonService _service;
        private bool _disposed;

        // ── 状态（调用阶段）──────────────────────────  
        public bool IsSuccess { get; }
        public bool IsCancelled { get; }
        public bool IsFaulted => !IsSuccess && !IsCancelled;
        public string ErrorMessage { get; }
        public Exception Exception { get; }

        /// <summary>Python 类型名快照（仅 IsSuccess=true 时有效）</summary>  
        public string PythonTypeName { get; }

        // ── 成功构造 ─────────────────────────────────  
        internal PythonResult(PyObject pyObj, string typeName, PythonService service)
        {
            _pyObj = pyObj;
            _service = service;
            PythonTypeName = typeName;
            IsSuccess = true;
        }

        // ── 失败构造 ─────────────────────────────────  
        internal PythonResult(string errorMessage, Exception ex, PythonService service)
        {
            _service = service;
            IsSuccess = false;
            IsCancelled = ex is OperationCanceledException;
            ErrorMessage = errorMessage;
            Exception = ex;
            PythonTypeName = string.Empty;
        }

        // ── 工厂方法 ─────────────────────────────────  
        internal static PythonResult Ok(PyObject pyObj, string typeName, PythonService svc)
            => new PythonResult(pyObj, typeName, svc);

        internal static PythonResult Fail(string msg, Exception ex, PythonService svc)
            => new PythonResult(msg, ex, svc);

        // ═════════════════════════════════════════════  
        // AsAsync — 转换阶段，返回 PythonResult<T>  
        // 任何异常都被捕获，不向上抛出  
        // ═════════════════════════════════════════════  

        /// <summary>自动 Mapper 映射（支持 [PyMapped] / List / Dict / 基础类型）</summary>  
        public Task<PythonResult<T>> AsAsync<T>(
            CancellationToken cancellationToken = default)
        {
            // 调用阶段已失败，直接透传错误，不再尝试转换  
            if (!IsSuccess)
                return Task.FromResult(IsCancelled
                    ? PythonResult<T>.Cancel()
                    : PythonResult<T>.Fail(ErrorMessage, Exception));

            EnsureAlive();
            return ConvertAsync<T>(
                pyObj => PythonObjectMapper.Map<T>(pyObj),
                cancellationToken);
        }

        /// <summary>自定义 lambda 转换（PyObject 版）</summary>  
        public Task<PythonResult<T>> AsAsync<T>(
            Func<PyObject, T> converter,
            CancellationToken cancellationToken = default)
        {
            if (!IsSuccess)
                return Task.FromResult(IsCancelled
                    ? PythonResult<T>.Cancel()
                    : PythonResult<T>.Fail(ErrorMessage, Exception));

            EnsureAlive();
            return ConvertAsync<T>(converter, cancellationToken);
        }

        /// <summary>自定义 lambda 转换（dynamic 版）</summary>  
        public Task<PythonResult<T>> AsAsync<T>(
            Func<dynamic, T> converter,
            CancellationToken cancellationToken = default)
        {
            if (!IsSuccess)
                return Task.FromResult(IsCancelled
                    ? PythonResult<T>.Cancel()
                    : PythonResult<T>.Fail(ErrorMessage, Exception));

            EnsureAlive();
            return ConvertAsync<T>(pyObj => converter(pyObj), cancellationToken);
        }

        // ── 快捷转换方法 ──────────────────────────────  
        public Task<PythonResult<string>>
            AsStringAsync(CancellationToken ct = default) => AsAsync<string>(ct);

        public Task<PythonResult<int>>
            AsIntAsync(CancellationToken ct = default) => AsAsync<int>(ct);

        public Task<PythonResult<double>>
            AsDoubleAsync(CancellationToken ct = default) => AsAsync<double>(ct);

        public Task<PythonResult<bool>>
            AsBoolAsync(CancellationToken ct = default) => AsAsync<bool>(ct);

        public Task<PythonResult<double[][]>>
            AsJaggedArrayAsync(CancellationToken ct = default) => AsAsync<double[][]>(ct);

        public Task<PythonResult<List<List<double>>>>
            AsNestedListAsync(CancellationToken ct = default) => AsAsync<List<List<double>>>(ct);

        public Task<PythonResult<Dictionary<string, object>>>
            AsDictAsync(CancellationToken ct = default) => AsAsync<Dictionary<string, object>>(ct);

        public Task<PythonResult<List<T>>>
            AsListAsync<T>(CancellationToken ct = default) => AsAsync<List<T>>(ct);

        /// <summary>调试用：获取 Python repr</summary>  
        public Task<PythonResult<string>> ToReprAsync(CancellationToken ct = default)
        {
            if (!IsSuccess)
                return Task.FromResult(PythonResult<string>.Fail(ErrorMessage, Exception));
            EnsureAlive();
            return ConvertAsync<string>(pyObj => pyObj.Repr(), ct);
        }

        // ═════════════════════════════════════════════  
        // 核心转换执行（统一捕获异常）  
        // ═════════════════════════════════════════════  
        private Task<PythonResult<T>> ConvertAsync<T>(
            Func<PyObject, T> converter,
            CancellationToken cancellationToken)
        {
            return _service.ScheduleAsync<PythonResult<T>>(
                () =>
                {
                    try
                    {
                        using (Py.GIL())
                            return PythonResult<T>.Ok(converter(_pyObj));
                    }
                    catch (OperationCanceledException)
                    {
                        return PythonResult<T>.Cancel();
                    }
                    catch (Exception ex)
                    {
                        return PythonResult<T>.Fail(
                            $"转换为 {typeof(T).Name} 失败: {ex.Message}", ex);
                    }
                },
                cancellationToken);
        }

        // ═════════════════════════════════════════════  
        // Dispose（必须在专用线程 + GIL 内释放 PyObject）  
        // ═════════════════════════════════════════════  
        public async ValueTask DisposeAsync()
        {
            if (_disposed || _pyObj == null) return;
            _disposed = true;
            await _service.ScheduleAsync(
                () => { using (Py.GIL()) _pyObj.Dispose(); },
                CancellationToken.None);
        }

        public void Dispose()
        {
            if (_disposed || _pyObj == null) return;
            _disposed = true;
            _service.ScheduleAsync(
                () => { using (Py.GIL()) _pyObj.Dispose(); },
                CancellationToken.None).Wait(3000);
        }

        private void EnsureAlive()
        {
            if (_disposed) throw new ObjectDisposedException(
                nameof(PythonResult), "PythonResult 已释放，请勿重复使用");
        }
    }

    // ─────────────────────────────────────────────────────────────  
    // 转换结果包装器 PythonResult<T>  
    // AsAsync 的返回值，携带转换阶段的成功/失败状态  
    // 纯 C# 对象，可在任意线程安全访问  
    // ─────────────────────────────────────────────────────────────  
    public sealed class PythonResult<T>
    {
        // ── 状态 ──────────────────────────────────────  
        public bool IsSuccess { get; }
        public bool IsCancelled { get; }
        public bool IsFaulted => !IsSuccess && !IsCancelled;
        public string ErrorMessage { get; }
        public Exception Exception { get; }

        // ── 数据 ──────────────────────────────────────  
        public T Value { get; }

        // ── 构造（内部创建）──────────────────────────  
        internal static PythonResult<T> Ok(T value) =>
            new PythonResult<T>(value, true, false, null, null);

        internal static PythonResult<T> Fail(string message, Exception ex) =>
            new PythonResult<T>(default(T), false, false, message, ex);

        internal static PythonResult<T> Cancel() =>
            new PythonResult<T>(default(T), false, true, "操作已取消", null);

        private PythonResult(T value, bool success, bool cancelled,
                             string message, Exception ex)
        {
            Value = value;
            IsSuccess = success;
            IsCancelled = cancelled;
            ErrorMessage = message;
            Exception = ex;
        }

        // ── 解构支持 ─────────────────────────────────  
        public void Deconstruct(out bool isSuccess, out T value, out string error)
        {
            isSuccess = IsSuccess;
            value = Value;
            error = ErrorMessage;
        }

        public override string ToString() => IsSuccess
            ? $"[OK]        {typeof(T).Name}"
            : IsCancelled
                ? $"[Cancelled] {typeof(T).Name}"
                : $"[Faulted]   {typeof(T).Name} → {ErrorMessage}";
    }
}
