using Astra.Core.Triggers.Configuration;
using Astra.Core.Triggers;
using Astra.Core.Triggers.Enums;
using Astra.Core.Triggers.Args;
using System.Collections.Concurrent;

namespace Astra.Core.Triggers.Manager
{
    #region ========== 触发器管理器 ==========

    /// <summary>
    /// 触发器管理器
    /// </summary>
    public class TriggerManager
    {
        #region 私有字段

        private readonly Dictionary<string, ITrigger> _triggers;
        private readonly List<ITriggerObserver> _observers;
        private ITrigger _currentTrigger;
        private WorkMode _currentMode;

        private TestExecutionConfig _executionConfig;
        private SemaphoreSlim _concurrencySemaphore;
        private readonly ConcurrentQueue<TriggerEventArgs> _testQueue;
        private int _runningTests;
        private int _completedTests;

        private Task _queueProcessorTask;
        private CancellationTokenSource _queueCts;

        private readonly ConcurrentDictionary<string, int> _triggerStatistics;

        #endregion

        #region 属性

        public WorkMode CurrentMode => _currentMode;
        public ITrigger CurrentTrigger => _currentTrigger;
        public TestExecutionConfig ExecutionConfig => _executionConfig;
        public int RunningTestCount => _runningTests;
        public int QueuedTestCount => _testQueue.Count;
        public int CompletedTestCount => _completedTests;
        public int TriggerCount => _triggers.Count;
        public int ObserverCount => _observers.Count;

        #endregion

        #region 构造函数

        public TriggerManager(TestExecutionConfig executionConfig = null)
        {
            _triggers = new Dictionary<string, ITrigger>();
            _observers = new List<ITriggerObserver>();
            _testQueue = new ConcurrentQueue<TriggerEventArgs>();
            _triggerStatistics = new ConcurrentDictionary<string, int>();

            _executionConfig = executionConfig ?? new TestExecutionConfig();
            _concurrencySemaphore = new SemaphoreSlim(
                _executionConfig.MaxConcurrency,
                _executionConfig.MaxConcurrency
            );

            _runningTests = 0;
            _completedTests = 0;

            Console.WriteLine("[TriggerManager] 初始化完成");
            Console.WriteLine($"  执行模式: {_executionConfig.ExecutionMode}");
            Console.WriteLine($"  最大并发: {_executionConfig.MaxConcurrency}");
        }

        #endregion

        #region 触发器管理

        /// <summary>
        /// 注册触发器
        /// </summary>
        public void RegisterTrigger(string triggerId, ITrigger trigger)
        {
            if (string.IsNullOrWhiteSpace(triggerId))
            {
                throw new ArgumentException("触发器ID不能为空", nameof(triggerId));
            }

            if (trigger == null)
            {
                throw new ArgumentNullException(nameof(trigger));
            }

            lock (_triggers)
            {
                if (_triggers.ContainsKey(triggerId))
                {
                    Console.WriteLine($"[TriggerManager] ⚠ 触发器 {triggerId} 已存在，将被覆盖");

                    var oldTrigger = _triggers[triggerId];
                    oldTrigger.OnTriggeredAsync -= Trigger_OnTriggeredAsync;
                }

                // 自动设置触发器的 TriggerId
                trigger.TriggerId = triggerId;

                _triggers[triggerId] = trigger;
                _triggerStatistics.TryAdd(triggerId, 0);

                // 订阅事件
                trigger.OnTriggeredAsync += Trigger_OnTriggeredAsync;

                Console.WriteLine($"[TriggerManager] ✓ 触发器注册成功");
                Console.WriteLine($"  ID: {triggerId}");
                Console.WriteLine($"  名称: {trigger.TriggerName}");
                Console.WriteLine($"  当前注册数: {_triggers.Count}");
            }
        }

        /// <summary>
        /// 注销触发器
        /// </summary>
        public async Task<bool> UnregisterTriggerAsync(string triggerId)
        {
            lock (_triggers)
            {
                if (!_triggers.ContainsKey(triggerId))
                {
                    Console.WriteLine($"[TriggerManager] ⚠ 触发器 {triggerId} 不存在");
                    return false;
                }

                var trigger = _triggers[triggerId];

                if (trigger.IsRunning)
                {
                    Console.WriteLine($"[TriggerManager] 停止触发器 {triggerId}...");
                    _ = trigger.StopAsync();
                }

                trigger.OnTriggeredAsync -= Trigger_OnTriggeredAsync;
                _triggers.Remove(triggerId);

                Console.WriteLine($"[TriggerManager] ✓ 触发器 {triggerId} 已注销");
                return true;
            }
        }

        /// <summary>
        /// 获取触发器
        /// </summary>
        public ITrigger GetTrigger(string triggerId)
        {
            lock (_triggers)
            {
                return _triggers.TryGetValue(triggerId, out var trigger) ? trigger : null;
            }
        }

        /// <summary>
        /// 获取所有触发器
        /// </summary>
        public List<ITrigger> GetAllTriggers()
        {
            lock (_triggers)
            {
                return new List<ITrigger>(_triggers.Values);
            }
        }

        /// <summary>
        /// 获取所有触发器ID
        /// </summary>
        public List<string> GetAllTriggerIds()
        {
            lock (_triggers)
            {
                return new List<string>(_triggers.Keys);
            }
        }

        /// <summary>
        /// 检查触发器是否存在
        /// </summary>
        public bool ContainsTrigger(string triggerId)
        {
            lock (_triggers)
            {
                return _triggers.ContainsKey(triggerId);
            }
        }

        #endregion

        #region 观察者管理

        /// <summary>
        /// 注册观察者（测试流程）
        /// </summary>
        public void RegisterObserver(ITriggerObserver observer)
        {
            if (observer == null)
            {
                throw new ArgumentNullException(nameof(observer));
            }

            lock (_observers)
            {
                if (_observers.Contains(observer))
                {
                    Console.WriteLine("[TriggerManager] ⚠ 观察者已存在");
                    return;
                }

                _observers.Add(observer);
                Console.WriteLine($"[TriggerManager] ✓ 观察者注册成功，当前数量: {_observers.Count}");
            }
        }

        /// <summary>
        /// 注销观察者
        /// </summary>
        public bool UnregisterObserver(ITriggerObserver observer)
        {
            lock (_observers)
            {
                var result = _observers.Remove(observer);
                if (result)
                {
                    Console.WriteLine($"[TriggerManager] ✓ 观察者已注销，当前数量: {_observers.Count}");
                }
                return result;
            }
        }

        /// <summary>
        /// 清空所有观察者
        /// </summary>
        public void ClearObservers()
        {
            lock (_observers)
            {
                _observers.Clear();
                Console.WriteLine("[TriggerManager] 所有观察者已清空");
            }
        }

        #endregion

        #region 事件处理 - 核心分发逻辑

        /// <summary>
        /// 触发器事件处理器（核心）
        /// </summary>
        private async Task Trigger_OnTriggeredAsync(object sender, TriggerEventArgs e)
        {
            var trigger = (ITrigger)sender;
            var triggerId = trigger.TriggerId;

            if (string.IsNullOrEmpty(triggerId))
            {
                Console.WriteLine("[TriggerManager] ⚠ 触发器ID未设置");
                return;
            }

            // 更新统计
            _triggerStatistics.AddOrUpdate(triggerId, 1, (key, value) => value + 1);

            Console.WriteLine($"[TriggerManager] 收到触发事件");
            Console.WriteLine($"  触发器ID: {triggerId}");
            Console.WriteLine($"  触发器名称: {trigger.TriggerName}");
            Console.WriteLine($"  SN: {e.GetSN() ?? "无"}");
            Console.WriteLine($"  触发源: {e.Source}");

            // 根据执行模式分发任务
            switch (_executionConfig.ExecutionMode)
            {
                case TestExecutionMode.Serial:
                    await HandleSerialModeAsync(e);
                    break;

                case TestExecutionMode.Parallel:
                    await HandleParallelModeAsync(e);
                    break;

                case TestExecutionMode.SkipIfBusy:
                    await HandleSkipIfBusyModeAsync(e);
                    break;
            }
        }

        #endregion

        #region 执行模式实现

        /// <summary>
        /// 串行模式：排队执行
        /// </summary>
        private Task HandleSerialModeAsync(TriggerEventArgs e)
        {
            if (_testQueue.Count >= _executionConfig.MaxQueueLength)
            {
                Console.WriteLine($"[TriggerManager] ⚠ 队列已满({_executionConfig.MaxQueueLength})，SN: {e.GetSN()} 被丢弃");
                return Task.CompletedTask;
            }

            _testQueue.Enqueue(e);
            Console.WriteLine($"[TriggerManager] 测试已加入队列");
            Console.WriteLine($"  SN: {e.GetSN()}");
            Console.WriteLine($"  队列长度: {_testQueue.Count}");
            Console.WriteLine($"  运行中: {_runningTests}");

            return Task.CompletedTask;
        }

        /// <summary>
        /// 并行模式：并发执行（限制最大并发数）
        /// </summary>
        private async Task HandleParallelModeAsync(TriggerEventArgs e)
        {
            // 尝试获取信号量槽位（非阻塞）
            if (!await _concurrencySemaphore.WaitAsync(0))
            {
                Console.WriteLine($"[TriggerManager] 达到最大并发({_executionConfig.MaxConcurrency})");
                Console.WriteLine($"  SN: {e.GetSN()} 等待空闲槽位...");

                // 阻塞等待槽位释放
                await _concurrencySemaphore.WaitAsync();

                Console.WriteLine($"  SN: {e.GetSN()} 获得槽位 ✓");
            }
            else
            {
                Console.WriteLine($"[TriggerManager] 获得槽位");
                Console.WriteLine($"  可用槽位: {_concurrencySemaphore.CurrentCount}/{_executionConfig.MaxConcurrency}");
            }

            // Fire-and-forget 启动测试任务
            _ = Task.Run(async () =>
            {
                try
                {
                    await ExecuteTestAsync(e);
                }
                finally
                {
                    // 释放信号量槽位
                    _concurrencySemaphore.Release();
                    Console.WriteLine($"[TriggerManager] 释放槽位 - SN: {e.GetSN()}");
                    Console.WriteLine($"  可用槽位: {_concurrencySemaphore.CurrentCount}/{_executionConfig.MaxConcurrency}");
                }
            });
        }

        /// <summary>
        /// 跳过模式：如果有测试在运行则跳过
        /// </summary>
        private async Task HandleSkipIfBusyModeAsync(TriggerEventArgs e)
        {
            if (_runningTests > 0)
            {
                Console.WriteLine($"[TriggerManager] 有测试正在执行，跳过 - SN: {e.GetSN()}");
                return;
            }

            Console.WriteLine($"[TriggerManager] 开始执行（SkipIfBusy模式）- SN: {e.GetSN()}");
            await ExecuteTestAsync(e);
        }

        #endregion

        #region 测试执行

        /// <summary>
        /// 执行测试（通知所有观察者）
        /// </summary>
        private async Task ExecuteTestAsync(TriggerEventArgs e)
        {
            Interlocked.Increment(ref _runningTests);

            try
            {
                Console.WriteLine($"\n[TriggerManager] ▶ 开始执行测试");
                Console.WriteLine($"  SN: {e.GetSN()}");
                Console.WriteLine($"  触发器ID: {e.GetTriggerId()}");
                Console.WriteLine($"  触发器名称: {e.GetTriggerName()}");
                Console.WriteLine($"  运行中: {_runningTests}");

                // 获取观察者列表（线程安全）
                List<ITriggerObserver> observers;
                lock (_observers)
                {
                    observers = new List<ITriggerObserver>(_observers);
                }

                // 并发执行所有观察者
                var tasks = observers.Select(observer =>
                    ExecuteObserverWithTimeoutAsync(observer, e));

                await Task.WhenAll(tasks);

                // 增加完成计数
                Interlocked.Increment(ref _completedTests);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TriggerManager] ✗ 测试执行异常 - SN: {e.GetSN()}");
                Console.WriteLine($"  错误: {ex.Message}");
            }
            finally
            {
                Interlocked.Decrement(ref _runningTests);
                Console.WriteLine($"[TriggerManager] ■ 测试完成 - SN: {e.GetSN()}");
                Console.WriteLine($"  运行中: {_runningTests}");
                Console.WriteLine($"  已完成: {_completedTests}\n");
            }
        }

        /// <summary>
        /// 带超时的观察者执行
        /// </summary>
        private async Task ExecuteObserverWithTimeoutAsync(ITriggerObserver observer, TriggerEventArgs e)
        {
            try
            {
                if (_executionConfig.TestTimeoutMs > 0)
                {
                    using var cts = new CancellationTokenSource(_executionConfig.TestTimeoutMs);
                    var task = observer.HandleTriggerAsync(e);

                    if (await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cts.Token)) == task)
                    {
                        await task;
                    }
                    else
                    {
                        Console.WriteLine($"[TriggerManager] ⏱ 测试超时 - SN: {e.GetSN()}");
                        Console.WriteLine($"  超时时间: {_executionConfig.TestTimeoutMs}ms");
                    }
                }
                else
                {
                    await observer.HandleTriggerAsync(e);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TriggerManager] ✗ 观察者执行异常 - SN: {e.GetSN()}");
                Console.WriteLine($"  错误: {ex.Message}");
            }
        }

        #endregion

        #region 队列处理器（串行模式）

        /// <summary>
        /// 启动队列处理器
        /// </summary>
        private void StartQueueProcessor()
        {
            if (_queueProcessorTask != null && !_queueProcessorTask.IsCompleted)
            {
                Console.WriteLine("[TriggerManager] 队列处理器已在运行");
                return;
            }

            _queueCts = new CancellationTokenSource();

            _queueProcessorTask = Task.Run(async () =>
            {
                Console.WriteLine("[TriggerManager] 队列处理器已启动");

                while (!_queueCts.Token.IsCancellationRequested)
                {
                    if (_testQueue.TryDequeue(out var args))
                    {
                        Console.WriteLine($"[TriggerManager] 从队列取出测试 - SN: {args.GetSN()}");
                        await ExecuteTestAsync(args);
                    }
                    else
                    {
                        await Task.Delay(100, _queueCts.Token);
                    }
                }

                Console.WriteLine("[TriggerManager] 队列处理器已停止");
            });
        }

        /// <summary>
        /// 停止队列处理器
        /// </summary>
        private void StopQueueProcessor()
        {
            if (_queueProcessorTask == null || _queueProcessorTask.IsCompleted)
            {
                return;
            }

            Console.WriteLine("[TriggerManager] 停止队列处理器...");
            _queueCts?.Cancel();

            try
            {
                _queueProcessorTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TriggerManager] 队列处理器停止异常: {ex.Message}");
            }
        }

        #endregion

        #region 模式切换

        /// <summary>
        /// 切换工作模式
        /// </summary>
        public async Task SwitchModeAsync(WorkMode mode, string triggerId)
        {
            Console.WriteLine($"\n[TriggerManager] ========== 模式切换 ==========");
            Console.WriteLine($"  目标模式: {mode}");
            Console.WriteLine($"  触发器ID: {triggerId}");

            lock (_triggers)
            {
                if (!_triggers.ContainsKey(triggerId))
                {
                    throw new ArgumentException($"触发器 {triggerId} 未注册", nameof(triggerId));
                }
            }

            // 停止当前触发器
            if (_currentTrigger != null && _currentTrigger.IsRunning)
            {
                Console.WriteLine($"[TriggerManager] 停止当前触发器: {_currentTrigger.TriggerName}");
                await _currentTrigger.StopAsync();
            }

            // 停止队列处理器
            StopQueueProcessor();

            // 切换到新触发器
            ITrigger newTrigger;
            lock (_triggers)
            {
                newTrigger = _triggers[triggerId];
            }

            _currentMode = mode;
            _currentTrigger = newTrigger;

            Console.WriteLine($"[TriggerManager] 当前触发器: {_currentTrigger.TriggerName}");
            Console.WriteLine($"[TriggerManager] 执行模式: {_executionConfig.ExecutionMode}");

            // 启动队列处理器（如果是串行模式）
            if (_executionConfig.ExecutionMode == TestExecutionMode.Serial)
            {
                StartQueueProcessor();
            }

            // 启动新触发器
            Console.WriteLine($"[TriggerManager] 启动触发器...");
            await _currentTrigger.StartAsync();

            Console.WriteLine($"[TriggerManager] ✓ 模式切换完成");
            Console.WriteLine($"[TriggerManager] =====================================\n");
        }

        /// <summary>
        /// 启动指定触发器
        /// </summary>
        public async Task StartTriggerAsync(string triggerId)
        {
            var trigger = GetTrigger(triggerId);
            if (trigger == null)
            {
                throw new ArgumentException($"触发器 {triggerId} 未注册");
            }

            if (trigger.IsRunning)
            {
                Console.WriteLine($"[TriggerManager] ⚠ 触发器 {triggerId} 已在运行");
                return;
            }

            Console.WriteLine($"[TriggerManager] 启动触发器: {triggerId}");
            await trigger.StartAsync();
        }

        /// <summary>
        /// 停止指定触发器
        /// </summary>
        public async Task StopTriggerAsync(string triggerId)
        {
            var trigger = GetTrigger(triggerId);
            if (trigger == null)
            {
                throw new ArgumentException($"触发器 {triggerId} 未注册");
            }

            if (!trigger.IsRunning)
            {
                Console.WriteLine($"[TriggerManager] ⚠ 触发器 {triggerId} 未运行");
                return;
            }

            Console.WriteLine($"[TriggerManager] 停止触发器: {triggerId}");
            await trigger.StopAsync();
        }

        /// <summary>
        /// 停止所有触发器
        /// </summary>
        public async Task StopAllAsync()
        {
            Console.WriteLine("\n[TriggerManager] 停止所有触发器...");

            // 停止所有运行中的触发器
            List<Task> stopTasks;
            lock (_triggers)
            {
                stopTasks = _triggers.Values
                    .Where(t => t.IsRunning)
                    .Select(t => t.StopAsync())
                    .ToList();
            }

            await Task.WhenAll(stopTasks);

            // 停止队列处理器
            StopQueueProcessor();

            _currentTrigger = null;

            // 等待所有测试完成
            Console.WriteLine($"[TriggerManager] 等待 {_runningTests} 个测试完成...");
            while (_runningTests > 0)
            {
                await Task.Delay(500);
            }

            Console.WriteLine("[TriggerManager] ✓ 所有触发器已停止\n");
        }

        #endregion

        #region 配置管理

        /// <summary>
        /// 配置防重复触发
        /// </summary>
        public void ConfigureAntiRepeat(string triggerId, AntiRepeatConfig config)
        {
            lock (_triggers)
            {
                if (_triggers.TryGetValue(triggerId, out var trigger))
                {
                    trigger.AntiRepeatConfig = config;
                    Console.WriteLine($"[TriggerManager] ✓ 触发器 {triggerId} 防重复配置已更新");
                }
                else
                {
                    Console.WriteLine($"[TriggerManager] ⚠ 触发器 {triggerId} 不存在");
                }
            }
        }

        /// <summary>
        /// 配置测试执行模式
        /// </summary>
        public void ConfigureTestExecution(TestExecutionConfig config)
        {
            _executionConfig = config ?? throw new ArgumentNullException(nameof(config));

            // 重新创建信号量
            _concurrencySemaphore?.Dispose();
            _concurrencySemaphore = new SemaphoreSlim(
                config.MaxConcurrency,
                config.MaxConcurrency
            );

            Console.WriteLine("[TriggerManager] ✓ 测试执行配置已更新");
            Console.WriteLine($"  执行模式: {config.ExecutionMode}");
            Console.WriteLine($"  最大并发: {config.MaxConcurrency}");
        }

        #endregion

        #region 统计和监控

        /// <summary>
        /// 获取所有触发器统计信息
        /// </summary>
        public List<Dictionary<string, object>> GetAllStatistics()
        {
            var result = new List<Dictionary<string, object>>();

            lock (_triggers)
            {
                foreach (var trigger in _triggers.Values)
                {
                    var stats = trigger.GetTriggerStatistics();
                    stats["TotalTriggers"] = _triggerStatistics.GetValueOrDefault(trigger.TriggerId, 0);
                    result.Add(stats);
                }
            }

            return result;
        }

        /// <summary>
        /// 获取系统运行状态
        /// </summary>
        public Dictionary<string, object> GetSystemStatus()
        {
            return new Dictionary<string, object>
            {
                { "CurrentMode", _currentMode },
                { "CurrentTrigger", _currentTrigger?.TriggerName ?? "无" },
                { "ExecutionMode", _executionConfig.ExecutionMode },
                { "MaxConcurrency", _executionConfig.MaxConcurrency },
                { "RunningTests", _runningTests },
                { "QueuedTests", _testQueue.Count },
                { "CompletedTests", _completedTests },
                { "RegisteredTriggers", _triggers.Count },
                { "RegisteredObservers", _observers.Count },
                { "AvailableSlots", _concurrencySemaphore.CurrentCount }
            };
        }

        /// <summary>
        /// 打印系统状态
        /// </summary>
        public void PrintStatus()
        {
            Console.WriteLine("\n========================================");
            Console.WriteLine("        TriggerManager 系统状态");
            Console.WriteLine("========================================");

            var status = GetSystemStatus();
            foreach (var kvp in status)
            {
                Console.WriteLine($"{kvp.Key}: {kvp.Value}");
            }

            Console.WriteLine("\n触发器统计:");
            var stats = GetAllStatistics();
            foreach (var stat in stats)
            {
                Console.WriteLine($"  [{stat["TriggerId"]}] {stat["TriggerName"]}");
                Console.WriteLine($"    运行中: {stat["IsRunning"]}");
                Console.WriteLine($"    触发次数: {stat["TriggerCount"]}");
                Console.WriteLine($"    被阻止: {stat["BlockedCount"]}");
            }

            Console.WriteLine("========================================\n");
        }

        #endregion

        #region 资源释放

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            StopAllAsync().Wait();
            _concurrencySemaphore?.Dispose();
            _queueCts?.Dispose();
        }

        #endregion
    }

    #endregion
}
