using Python.Runtime;
using PythonWrapper.Modules;
using PythonWrapper.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PythonWrapper
{
    public sealed class PythonService : IDisposable
    {
        // ── 单例 ──────────────────────────────────────  
        public static PythonService Instance { get; } = new PythonService();

        // ── 专用线程 & 任务队列 ───────────────────────  
        private readonly Thread _thread;
        private readonly BlockingCollection<Action> _queue = new BlockingCollection<Action>();

        // ── 模块缓存 ──────────────────────────────────  
        private readonly Dictionary<string, CachedModule> _moduleCache = new Dictionary<string, CachedModule>();

        // ── 统计 ──────────────────────────────────────  
        private int _totalImportCount;
        private int _totalHitCount;

        // ── 状态 ──────────────────────────────────────  
        private bool _initialized;
        private bool _disposed;

        // ── 当前执行任务的 Python 线程 ID（原子读写）────────
        // 0 表示当前没有正在执行的 Python 任务
        private ulong _currentPyThreadId;
        private PythonService()
        {
            _thread = new Thread(RunLoop)
            {
                Name = "Python-Dedicated-Thread",
                IsBackground = true
            };
            _thread.Start();
        }

        // ═════════════════════════════════════════════  
        // 专用线程消息循环  
        // ═════════════════════════════════════════════  
        private void RunLoop()
        {
            foreach (var action in _queue.GetConsumingEnumerable())
                action();
        }

        // ═════════════════════════════════════════════════
        // 任务调度（支持 CancellationToken）
        // ═════════════════════════════════════════════════

        internal Task ScheduleAsync(Action action, CancellationToken ct = default)
        {
            var tcs = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                _queue.Add(() =>
                {
                    if (ct.IsCancellationRequested)
                    {
                        tcs.SetCanceled(ct);
                        return;
                    }
                    try { action(); tcs.SetResult(); }
                    catch (OperationCanceledException e) { tcs.SetCanceled(e.CancellationToken); }
                    catch (Exception ex) { tcs.SetException(ex); }
                });
            }
            catch (InvalidOperationException ex)
            {
                tcs.SetException(new ObjectDisposedException(nameof(PythonService), ex.Message));
            }

            return tcs.Task;
        }

        internal Task<T> ScheduleAsync<T>(Func<T> func, CancellationToken ct = default)
        {
            var tcs = new TaskCompletionSource<T>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                _queue.Add(() =>
                {
                    if (ct.IsCancellationRequested)
                    {
                        tcs.SetCanceled(ct);
                        return;
                    }
                    try { tcs.SetResult(func()); }
                    catch (OperationCanceledException e) { tcs.SetCanceled(e.CancellationToken); }
                    catch (Exception ex) { tcs.SetException(ex); }
                });
            }
            catch (InvalidOperationException ex)
            {
                tcs.SetException(new ObjectDisposedException(nameof(PythonService), ex.Message));
            }

            return tcs.Task;
        }

        // ═════════════════════════════════════════════  
        // ① 初始化 & 路径管理（同前）  
        // ═════════════════════════════════════════════  
        public Task InitializeAsync(string pythonDllPath, params string[] scriptPaths)
        {
            return ScheduleAsync(() =>
            {
                if (_initialized) return;
                Runtime.PythonDLL = pythonDllPath;
                PythonEngine.Initialize();
                PythonEngine.BeginAllowThreads();
                _initialized = true;

                if (scriptPaths?.Length > 0) AddScriptPathAsync(scriptPaths);
            });
        }

        public Task AddScriptPathAsync(params string[] paths)
        {
            EnsureInit();
            return ScheduleAsync(() => { using (Py.GIL()) AddPathsInternal(paths); });
        }

        public Task<IReadOnlyList<string>> GetSysPathAsync()
        {
            EnsureInit();
            return ScheduleAsync<IReadOnlyList<string>>(() =>
            {
                using (Py.GIL())
                {
                    dynamic sys = Py.Import("sys");
                    var result = new List<string>();
                    foreach (var p in sys.path) result.Add((string)p);
                    return result;
                }
            });
        }


        // ═════════════════════════════════════════════════
        // Python 线程 ID 管理（用于 Interrupt）
        // ═════════════════════════════════════════════════

        /// <summary>
        /// 在专用线程 + GIL 内获取当前 Python 线程 ID
        /// ⭐ 必须在任务执行前调用，结果存入 _currentPyThreadId
        /// </summary>
        private ulong CapturePyThreadId()
        {
            // 已在专用线程 + GIL 内调用
            dynamic threading = Py.Import("threading");
            var id = (ulong)threading.current_thread().ident;
            Interlocked.Exchange(ref _currentPyThreadId, id);
            return id;
        }

        private void ClearPyThreadId() =>
            Interlocked.Exchange(ref _currentPyThreadId, 0UL);

        /// <summary>
        /// 向当前正在执行的 Python 任务注入 KeyboardInterrupt
        /// ⭐ 可在任意线程调用（通常在 CancellationToken 回调中）
        /// </summary>
        public void InterruptCurrentPythonTask()
        {
            var id = Interlocked.Read(ref _currentPyThreadId);
            if (id != 0)
            {
                try { PythonEngine.Interrupt(id); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[PythonService] Interrupt 失败: {ex.Message}");
                }
            }
        }

        // ═════════════════════════════════════════════════
        //  CallAsync<T>（有返回值，带状态返回 + 取消支持）
        // ═════════════════════════════════════════════════
        public Task<PythonResult<T>> CallAsync<T>(
            string moduleName,
            string methodName,
            CancellationToken cancellationToken = default,
            params object[] args)
        {
            EnsureInit();

            return ScheduleAsync<PythonResult<T>>(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return PythonResult<T>.Cancel();

                // 注册取消回调：CancellationToken 触发时注入 KeyboardInterrupt
                using (var reg = cancellationToken.Register(InterruptCurrentPythonTask))
                {

                try
                {
                    using (Py.GIL())
                    {
                        // 记录 Python 线程 ID，供 Interrupt 使用
                        CapturePyThreadId();

                        var module = GetOrImportModule(moduleName);
                        using (var r = module.InvokeMethod(methodName, ConvertArgs(args)))
                        {
                            var value = PythonObjectMapper.Map<T>(r);

                            return PythonResult<T>.Ok(value);
                        }
                    }
                }
                catch (PythonException pyEx)
                {
                    string message = $"调用 {moduleName}.{methodName} 失败 " +
               $"[{PythonExceptionHelper.GetTypeName(pyEx)}]: " +
               $"{PythonExceptionHelper.GetFullMessage(pyEx)}";

                    return PythonResult<T>.Fail(message, pyEx);


                }
                catch (OperationCanceledException)
                {
                    return PythonResult<T>.Cancel();
                }

                catch (Exception ex)
                {
                    return PythonResult<T>.Fail(ex.Message, ex);
                }
                finally
                {
                    ClearPyThreadId();
                }
                }
            }, cancellationToken);
        }


        // ═════════════════════════════════════════════  
        //  CallVoidAsync — 无返回值（带取消 & 状态）  
        // ═════════════════════════════════════════════  
        public Task<PythonResult<bool>> CallVoidAsync(
            string moduleName,
            string methodName,
            CancellationToken cancellationToken = default,
            params object[] args)
        {
            EnsureInit();
            return ScheduleAsync<PythonResult<bool>>(() =>
            {              
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    // 注册取消回调：CancellationToken 触发时注入 KeyboardInterrupt
                    using (var reg = cancellationToken.Register(InterruptCurrentPythonTask))
                    {
                        using (Py.GIL())
                        {
                            CapturePyThreadId();
                            var module = GetOrImportModule(moduleName);
                            using (var __ = module.InvokeMethod(methodName, ConvertArgs(args)))
                            {
                            }
                            return PythonResult<bool>.Ok(true);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    return PythonResult<bool>.Cancel();
                }
                catch (PythonException pyEx)
                {
                    string message = $"调用 {moduleName}.{methodName} 失败 " +
                       $"[{PythonExceptionHelper.GetTypeName(pyEx)}]: " +
                       $"{PythonExceptionHelper.GetFullMessage(pyEx)}";

                    return PythonResult<bool>.Fail(message, pyEx);
                }
                catch (Exception ex)
                {
                    return PythonResult<bool>.Fail(
                        $"调用 {moduleName}.{methodName} 失败: {ex.Message}", ex);
                }
                finally { ClearPyThreadId(); }
            }, cancellationToken);
        }

        // ═════════════════════════════════════════════  
        //  CallRawAsync — 懒转换（带取消 & 状态）  
        // ═════════════════════════════════════════════  
        public Task<PythonResult> CallRawAsync(
            string moduleName,
            string methodName,
            CancellationToken cancellationToken = default,
            params object[] args)
        {
            EnsureInit();
            return ScheduleAsync<PythonResult>(() =>
            {               
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    // 注册取消回调：CancellationToken 触发时注入 KeyboardInterrupt
                    using (var reg = cancellationToken.Register(InterruptCurrentPythonTask))
                    {
                        using (Py.GIL())
                        {
                            CapturePyThreadId();
                            var module = GetOrImportModule(moduleName);
                            var pyResult = module.InvokeMethod(methodName, ConvertArgs(args));
                            var typeName = pyResult.GetPythonType().Name;
                            return PythonResult.Ok(pyResult, typeName, this);
                        }
                    }
                }
                catch (OperationCanceledException oce)
                {
                    return PythonResult.Fail("操作已取消", oce, this);
                }
                catch (Exception ex)
                {
                    return PythonResult.Fail(
                        $"调用 {moduleName}.{methodName} 失败: {ex.Message}", ex, this);
                }
                finally { ClearPyThreadId(); }
            }, cancellationToken);
        }

        // ═════════════════════════════════════════════  
        // RunAsync — 完全自定义 lambda（带取消）  
        // ═════════════════════════════════════════════  
        public Task<PythonResult<T>> RunAsync<T>(
            string moduleName,
            Func<dynamic, T> func,
            CancellationToken cancellationToken = default,
            bool forceReload = false)
        {
            EnsureInit();
            return ScheduleAsync<PythonResult<T>>(() =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    // 注册取消回调：CancellationToken 触发时注入 KeyboardInterrupt
                    using (var reg = cancellationToken.Register(InterruptCurrentPythonTask))
                    {
                        using (Py.GIL())
                        {
                            var module = GetOrImportModule(moduleName, forceReload);
                            return PythonResult<T>.Ok(func(module));
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    return PythonResult<T>.Cancel();
                }
                catch (Exception ex)
                {
                    return PythonResult<T>.Fail(
                        $"RunAsync {moduleName} 失败: {ex.Message}", ex);
                }
                finally { ClearPyThreadId(); }
            }, cancellationToken);
        }

        /// <summary>  
        /// 调用耗时 Python 算法，支持真正的运行时中断  
        /// Token 取消时自动调用 PythonEngine.Interrupt()  
        /// Python 端须未屏蔽 KeyboardInterrupt，才能响应中断  
        /// </summary>  
        
        public Task<PythonResult<T>> CallLongRunningAsync<T>(
            string moduleName,
            string methodName,
            CancellationToken cancellationToken = default,
            params object[] args)
        {
            EnsureInit();

            var tcs = new TaskCompletionSource<PythonResult<T>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            // 注册取消回调：CancellationToken 触发时注入 KeyboardInterrupt
            var registration = cancellationToken.Register(InterruptCurrentPythonTask);
            try
            {
                _queue.Add(() =>
                {
                    // 出队前已取消
                    if (cancellationToken.IsCancellationRequested)
                    {
                        tcs.SetResult(PythonResult<T>.Cancel());
                        return;
                    }

                    try
                    {
                        using (Py.GIL())
                        {
                            CapturePyThreadId();
                            var module = GetOrImportModule(moduleName);
                            using (var r = module.InvokeMethod(methodName, ConvertArgs(args)))
                            {
                                tcs.SetResult(PythonResult<T>.Ok(PythonObjectMapper.Map<T>(r)));
                            }
                        }
                    }
                    catch (PythonException pex)
                        // ✅ 修复：用工具方法判断，不用 PythonTypeName
                        when (PythonExceptionHelper.IsKeyboardInterrupt(pex) ||
                              cancellationToken.IsCancellationRequested)
                    {
                        tcs.SetResult(PythonResult<T>.Cancel());
                    }
                    catch (OperationCanceledException)
                    {
                        tcs.SetResult(PythonResult<T>.Cancel());
                    }
                    catch (PythonException pex)
                    {
                        // ✅ 修复：用 Format() 获取含 Traceback 的完整信息
                        tcs.SetResult(PythonResult<T>.Fail(
                            $"调用 {moduleName}.{methodName} 失败 " +
                            $"[{PythonExceptionHelper.GetTypeName(pex)}]: " +
                            $"{PythonExceptionHelper.GetFullMessage(pex)}",
                            pex));
                    }
                    catch (Exception ex)
                    {
                        tcs.SetResult(PythonResult<T>.Fail(
                            $"调用 {moduleName}.{methodName} 失败: {ex.Message}", ex));
                    }
                    finally
                    {
                        registration.Dispose();
                        ClearPyThreadId();
                    }
                });
            }
            catch (InvalidOperationException ex)
            {
                registration.Dispose();
                tcs.SetException(new ObjectDisposedException(nameof(PythonService), ex.Message));
            }

            return tcs.Task;
        }

        // ═════════════════════════════════════════════  
        // 快捷语义方法  
        // ═════════════════════════════════════════════  
        public Task<PythonResult<List<T>>> CallListAsync<T>(
            string m, string method, CancellationToken ct = default, params object[] args)
            => CallAsync<List<T>>(m, method, ct, args);

        public Task<PythonResult<Dictionary<string, object>>> CallDictAsync(
            string m, string method, CancellationToken ct = default, params object[] args)
            => CallAsync<Dictionary<string, object>>(m, method, ct, args);

        public Task<PythonResult<double[][]>> CallJaggedArrayAsync(
            string m, string method, CancellationToken ct = default, params object[] args)
            => CallAsync<double[][]>(m, method, ct, args);

        // ═════════════════════════════════════════════  
        // 缓存管理
        // ═════════════════════════════════════════════  
        public Task WarmUpAsync(params string[] moduleNames)
        {
            EnsureInit();
            return ScheduleAsync(() =>
            {
                using (Py.GIL())
                    foreach (var n in moduleNames)
                        GetOrImportModule(n);
            });
        }

        public Task ReloadModuleAsync(string moduleName)
        {
            EnsureInit();
            return ScheduleAsync(() =>
            {
                using (Py.GIL())
                    GetOrImportModule(moduleName, forceReload: true);
            });
        }

        public Task InvalidateAsync(string moduleName)
        {
            return ScheduleAsync(() =>
            {
                using (Py.GIL())
                {
                    if (_moduleCache.TryGetValue(moduleName, out var e))
                    {
                        e.Dispose();
                        _moduleCache.Remove(moduleName);
                    }
                }
            });
        }

        // ═════════════════════════════════════════════  
        // 私有工具方法  
        // ═════════════════════════════════════════════  
        private PyObject GetOrImportModule(string name, bool forceReload = false)
        {
            if (forceReload && _moduleCache.TryGetValue(name, out var old))
            {
                old.Dispose();
                _moduleCache.Remove(name);
            }
            if (_moduleCache.TryGetValue(name, out var cached))
            {
                cached.RecordHit();
                Interlocked.Increment(ref _totalHitCount);
                return cached.Module;
            }
            var module = Py.Import(name);
            _moduleCache[name] = new CachedModule(module);
            Interlocked.Increment(ref _totalImportCount);
            return module;
        }

        private static PyObject[] ConvertArgs(object[] args)
        {
            if (args is null || args.Length == 0) return Array.Empty<PyObject>();
            var result = new PyObject[args.Length];
            for (int i = 0; i < args.Length; i++)
                result[i] = PythonTypeConverter.ToPython(args[i]);
            return result;
        }

        private void AddPathsInternal(string[] paths)
        {
            dynamic sys = Py.Import("sys");
            foreach (var raw in paths)
            {
                var norm = System.IO.Path.GetFullPath(raw).Replace('\\', '/');
                bool exists = false;
                foreach (var p in sys.path)
                    if ((string)p == norm) { exists = true; break; }
                if (!exists) sys.path.insert(0, norm);
            }
        }

        private void EnsureInit()
        {
            if (!_initialized)
                throw new InvalidOperationException("请先调用 InitializeAsync()");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ScheduleAsync(() =>
            {
                using (Py.GIL())
                {
                    foreach (var m in _moduleCache.Values) m.Dispose();
                    _moduleCache.Clear();
                }
                if (_initialized) PythonEngine.Shutdown();
            }).Wait(5000);
            _queue.CompleteAdding();
        }
    }
}
