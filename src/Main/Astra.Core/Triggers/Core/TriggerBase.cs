using Astra.Core.Triggers.Args;
using Astra.Core.Triggers.Configuration;
using Astra.Core.Triggers.Enums;
using Astra.Core.Triggers.Models;
using System.Collections.Concurrent;

namespace Astra.Core.Triggers
{
    #region ========== 触发器基类（改进版）==========

    /// <summary>
    /// 触发器基类（改进版 - 正确的模板方法模式）
    /// </summary>
    public abstract class TriggerBase : ITrigger
    {
        #region 属性

        public string TriggerId { get; set; }
        public abstract string TriggerName { get; }
        public bool IsRunning { get; private set; }
        public AntiRepeatConfig AntiRepeatConfig { get; set; }
        public event AsyncEventHandler<TriggerEventArgs> OnTriggeredAsync;

        /// <summary>
        /// 触发器工作类型（子类必须指定）
        /// </summary>
        protected abstract TriggerWorkType WorkType { get; }

        /// <summary>
        /// 轮询间隔（毫秒）- 仅轮询型触发器有效
        /// </summary>
        protected virtual int PollIntervalMs => 100;

        #endregion

        #region 私有字段

        private CancellationTokenSource _cts;
        private Task _triggerTask;
        private readonly ConcurrentDictionary<string, DateTime> _triggerHistory;
        private DateTime _lastTriggerTime;
        private int _triggerCount;
        private int _blockedCount;

        #endregion

        #region 构造函数

        protected TriggerBase()
        {
            _triggerHistory = new ConcurrentDictionary<string, DateTime>();
            AntiRepeatConfig = new AntiRepeatConfig();
            _lastTriggerTime = DateTime.MinValue;
            _triggerCount = 0;
            _blockedCount = 0;
        }

        #endregion

        #region 启动/停止

        public async Task StartAsync()
        {
            if (IsRunning)
            {
                Console.WriteLine($"[{TriggerName}] ⚠ 触发器已在运行");
                return;
            }

            Console.WriteLine($"[{TriggerName}] 启动触发器 (ID: {TriggerId ?? "未设置"})");
            Console.WriteLine($"  工作类型: {WorkType}");

            if (!await OnBeforeStartAsync())
            {
                Console.WriteLine($"[{TriggerName}] ✗ 启动前检查失败");
                return;
            }

            _cts = new CancellationTokenSource();
            IsRunning = true;

            _triggerTask = Task.Run(async () =>
            {
                try
                {
                    await ExecuteTriggerLoopAsync(_cts.Token);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"[{TriggerName}] 触发器已取消");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{TriggerName}] ✗ 触发器异常: {ex.Message}");
                }
            });

            Console.WriteLine($"[{TriggerName}] ✓ 触发器已启动");
        }

        public async Task StopAsync()
        {
            if (!IsRunning)
            {
                Console.WriteLine($"[{TriggerName}] ⚠ 触发器未运行");
                return;
            }

            Console.WriteLine($"[{TriggerName}] 停止触发器 (ID: {TriggerId ?? "未设置"})");

            await OnBeforeStopAsync();

            _cts?.Cancel();
            IsRunning = false;

            if (_triggerTask != null)
            {
                try
                {
                    await _triggerTask;
                }
                catch (Exception)
                {
                    // 忽略取消异常
                }
            }

            Console.WriteLine($"[{TriggerName}] ✓ 触发器已停止");
        }

        #endregion

        #region 核心逻辑（父类实现 - 模板方法模式）

        /// <summary>
        /// 触发器主循环（父类实现，不可重写）
        /// </summary>
        private async Task ExecuteTriggerLoopAsync(CancellationToken cancellationToken)
        {
            if (WorkType == TriggerWorkType.Polling)
            {
                // 【轮询模式】：父类循环调用子类的 CheckTriggerAsync
                Console.WriteLine($"[{TriggerName}] 启动轮询模式，间隔: {PollIntervalMs}ms");

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // 调用子类的检查方法
                        var result = await CheckTriggerAsync();

                        // 如果触发了，父类自动处理
                        if (result != null && result.IsTriggered)
                        {
                            await ProcessTriggerInternalAsync(result);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[{TriggerName}] 检查触发异常: {ex.Message}");
                    }

                    // 等待下次轮询
                    await Task.Delay(PollIntervalMs, cancellationToken);
                }
            }
            else if (WorkType == TriggerWorkType.EventDriven)
            {
                // 【事件模式】：保持运行状态，等待子类主动调用 RaiseTrigger
                Console.WriteLine($"[{TriggerName}] 启动事件驱动模式");

                // 调用子类的初始化方法
                await InitializeEventDrivenAsync(cancellationToken);

                // 保持运行
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
        }

        /// <summary>
        /// 处理触发（父类统一处理）
        /// </summary>
        private async Task ProcessTriggerInternalAsync(TriggerResult result)
        {
            if (!IsRunning)
            {
                return;
            }

            // 1. 验证数据
            if (!await ValidateTriggerDataAsync(result.Data))
            {
                return;
            }

            // 2. 防重复检查（如果有 SN）
            if (result.Data.TryGetValue("SN", out var snObj) && snObj != null)
            {
                var sn = snObj.ToString();
                if (!CheckAntiRepeat(sn))
                {
                    Interlocked.Increment(ref _blockedCount);
                    return;
                }

                // 记录触发
                RecordTrigger(sn);
            }

            // 3. 创建事件参数
            var args = new TriggerEventArgs(result.Source)
            {
                AdditionalData = result.Data
            };

            // 4. 自动添加触发器信息
            args.AdditionalData["TriggerId"] = TriggerId;
            args.AdditionalData["TriggerName"] = TriggerName;

            // 5. 增强事件参数（子类可重写）
            await EnhanceTriggerArgsAsync(args);

            // 6. 异步触发事件（Fire-and-Forget）
            _ = RaiseTriggerEventAsync(args);

            // 7. 增加计数
            Interlocked.Increment(ref _triggerCount);
        }

        private async Task RaiseTriggerEventAsync(TriggerEventArgs args)
        {
            Console.WriteLine($"[{TriggerName}] ✓ 触发测试");
            Console.WriteLine($"  触发器ID: {TriggerId}");
            Console.WriteLine($"  SN: {args.GetSN() ?? "无"}");

            var handler = OnTriggeredAsync;
            if (handler != null)
            {
                var delegates = handler.GetInvocationList();
                var tasks = delegates
                    .Cast<AsyncEventHandler<TriggerEventArgs>>()
                    .Select(d => InvokeHandlerAsync(d, args));

                await Task.WhenAll(tasks);
            }
        }

        private async Task InvokeHandlerAsync(
            AsyncEventHandler<TriggerEventArgs> handler,
            TriggerEventArgs args)
        {
            try
            {
                await handler(this, args);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{TriggerName}] 事件处理器异常: {ex.Message}");
            }
        }

        #endregion

        #region 供事件驱动型触发器使用的方法

        /// <summary>
        /// 【事件驱动型触发器】主动触发（供子类调用）
        /// 例如：扫码枪接收到数据时调用此方法
        /// </summary>
        protected async Task RaiseTriggerAsync(TriggerResult result)
        {
            if (!IsRunning)
            {
                return;
            }

            if (result == null || !result.IsTriggered)
            {
                return;
            }

            await ProcessTriggerInternalAsync(result);
        }

        /// <summary>
        /// 【事件驱动型触发器】便捷方法：直接触发
        /// </summary>
        protected async Task RaiseTriggerAsync(TriggerSource source, string sn, Dictionary<string, object> additionalData = null)
        {
            var result = TriggerResult.TriggeredWithSN(source, sn, additionalData);
            await RaiseTriggerAsync(result);
        }

        #endregion

        #region 防重复检查

        private bool CheckAntiRepeat(string sn)
        {
            if (!AntiRepeatConfig.Enabled)
            {
                return true;
            }

            var now = DateTime.Now;

            // 全局最小间隔检查
            if (AntiRepeatConfig.GlobalMinIntervalMs > 0)
            {
                var timeSinceLastTrigger = (now - _lastTriggerTime).TotalMilliseconds;
                if (timeSinceLastTrigger < AntiRepeatConfig.GlobalMinIntervalMs)
                {
                    Console.WriteLine($"[{TriggerName}] ⊘ 全局间隔过短: {timeSinceLastTrigger:F0}ms");
                    return false;
                }
            }

            // 相同SN间隔检查
            if (AntiRepeatConfig.MinIntervalMs > 0)
            {
                if (_triggerHistory.TryGetValue(sn, out var lastTime))
                {
                    var interval = (now - lastTime).TotalMilliseconds;
                    if (interval < AntiRepeatConfig.MinIntervalMs)
                    {
                        Console.WriteLine($"[{TriggerName}] ⊘ 重复触发: {sn}, 间隔: {interval:F0}ms");
                        return false;
                    }
                }
            }

            return true;
        }

        private void RecordTrigger(string sn)
        {
            _lastTriggerTime = DateTime.Now;
            _triggerHistory.AddOrUpdate(sn, _lastTriggerTime, (key, oldValue) => _lastTriggerTime);

            // 清理旧记录
            if (_triggerHistory.Count > 1000)
            {
                var expireTime = DateTime.Now.AddSeconds(-AntiRepeatConfig.MinIntervalMs / 1000 * 2);
                var expiredKeys = _triggerHistory
                    .Where(kvp => kvp.Value < expireTime)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    _triggerHistory.TryRemove(key, out _);
                }
            }
        }

        #endregion

        #region 统计信息

        public Dictionary<string, object> GetTriggerStatistics()
        {
            return new Dictionary<string, object>
            {
                { "TriggerId", TriggerId ?? "未设置" },
                { "TriggerName", TriggerName },
                { "WorkType", WorkType },
                { "IsRunning", IsRunning },
                { "TriggerCount", _triggerCount },
                { "BlockedCount", _blockedCount },
                { "HistoryCount", _triggerHistory.Count },
                { "LastTriggerTime", _lastTriggerTime }
            };
        }

        #endregion

        #region 抽象/虚方法（子类可选实现）

        /// <summary>
        /// 【轮询型触发器】检查是否触发（子类实现检测逻辑）
        /// 返回 null 或 TriggerResult.NotTriggered() 表示未触发
        /// 返回 TriggerResult.Triggered(...) 表示触发了
        /// </summary>
        protected virtual Task<TriggerResult> CheckTriggerAsync()
        {
            return Task.FromResult<TriggerResult>(null);
        }

        /// <summary>
        /// 【事件驱动型触发器】初始化（例如订阅设备事件）
        /// </summary>
        protected virtual Task InitializeEventDrivenAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 启动前回调
        /// </summary>
        protected virtual Task<bool> OnBeforeStartAsync() => Task.FromResult(true);

        /// <summary>
        /// 停止前回调
        /// </summary>
        protected virtual Task OnBeforeStopAsync() => Task.CompletedTask;

        /// <summary>
        /// 验证触发数据
        /// </summary>
        protected virtual Task<bool> ValidateTriggerDataAsync(Dictionary<string, object> data) => Task.FromResult(true);

        /// <summary>
        /// 增强触发事件参数
        /// </summary>
        protected virtual Task EnhanceTriggerArgsAsync(TriggerEventArgs args) => Task.CompletedTask;

        #endregion
    }

    #endregion
}
