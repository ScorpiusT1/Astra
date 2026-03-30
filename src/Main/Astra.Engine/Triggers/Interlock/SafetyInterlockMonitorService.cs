using System.Linq;
using System.Threading;
using Astra.Core.Foundation.Common;
using Astra.Core.Triggers.Interlock;
using Microsoft.Extensions.Logging;

namespace Astra.Engine.Triggers.Interlock
{
    /// <summary>
    /// 按 IO 配置生成的规则轮询 IO，在满足规则时暂停/恢复/停止会话内跟踪的全部测试。
    /// 全局开关与周期来自 <see cref="ISafetyInterlockGlobalOptionsSource"/>（软件配置）。
    /// </summary>
    public sealed class SafetyInterlockMonitorService : ISafetyInterlockMonitor
    {
        private readonly ISafetyInterlockGlobalOptionsSource _globalOptions;
        private readonly ISafetyInterlockRulesProvider _rulesProvider;
        private readonly ISafetyInterlockIoReader _ioReader;
        private readonly ITestExecutionInterlockController _interlockController;
        private readonly ILogger<SafetyInterlockMonitorService> _logger;

        private readonly object _gate = new();
        private CancellationTokenSource? _cts;
        private Task? _loopTask;
        private readonly Dictionary<int, bool?> _lastByRuleIndex = new();
        private int _lastMergedRuleCount = -1;

        public SafetyInterlockMonitorService(
            ISafetyInterlockGlobalOptionsSource globalOptions,
            ISafetyInterlockRulesProvider rulesProvider,
            ISafetyInterlockIoReader ioReader,
            ITestExecutionInterlockController interlockController,
            ILogger<SafetyInterlockMonitorService> logger)
        {
            _globalOptions = globalOptions ?? throw new ArgumentNullException(nameof(globalOptions));
            _rulesProvider = rulesProvider ?? throw new ArgumentNullException(nameof(rulesProvider));
            _ioReader = ioReader ?? throw new ArgumentNullException(nameof(ioReader));
            _interlockController = interlockController ?? throw new ArgumentNullException(nameof(interlockController));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (_cts != null && !_cts.IsCancellationRequested)
                {
                    return Task.CompletedTask;
                }

                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var token = _cts.Token;
                _loopTask = Task.Run(() => RunLoopAsync(token), token);
            }

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            Task? toAwait;
            lock (_gate)
            {
                _cts?.Cancel();
                toAwait = _loopTask;
                _cts = null;
                _loopTask = null;
                _lastByRuleIndex.Clear();
                _lastMergedRuleCount = -1;
            }

            if (toAwait != null)
            {
                try
                {
                    await toAwait.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        private async Task RunLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var (pollMs, rules, interlockActive) = await LoadMergedAsync(cancellationToken).ConfigureAwait(false);
                    if (!interlockActive || rules.Count == 0)
                    {
                        await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    if (rules.Count != Volatile.Read(ref _lastMergedRuleCount))
                    {
                        lock (_gate)
                        {
                            _lastByRuleIndex.Clear();
                            Volatile.Write(ref _lastMergedRuleCount, rules.Count);
                        }
                    }

                    for (var i = 0; i < rules.Count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await EvaluateRuleAsync(i, rules[i], cancellationToken).ConfigureAwait(false);
                    }

                    var delay = Math.Clamp(pollMs, 50, 60_000);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "安全联锁监控循环异常，稍后重试");
                    try
                    {
                        await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private async Task EvaluateRuleAsync(int index, SafetyInterlockRuleItem rule, CancellationToken cancellationToken)
        {
            var read = await _ioReader.ReadBoolAsync(rule.PlcDeviceName, rule.IoPointName, cancellationToken).ConfigureAwait(false);
            if (!read.Success)
            {
                _logger.LogDebug("联锁「{Rule}」IO 读取未成功: {Message}", rule.RuleName, read.ErrorMessage ?? read.Message);
                return;
            }

            var current = read.Data;

            if (rule.EdgeTriggered)
            {
                bool? last;
                lock (_gate)
                {
                    if (!_lastByRuleIndex.TryGetValue(index, out last))
                    {
                        _lastByRuleIndex[index] = current;
                        return;
                    }
                }

                if (last == current)
                {
                    return;
                }

                var action = current ? rule.ActionOnTrue : rule.ActionOnFalse;
                Dispatch(action, rule.RuleName, current);
                lock (_gate)
                {
                    _lastByRuleIndex[index] = current;
                }
            }
            else
            {
                var action = current ? rule.ActionOnTrue : rule.ActionOnFalse;
                Dispatch(action, rule.RuleName, current);
            }
        }

        private void Dispatch(InterlockRuleAction action, string ruleName, bool value)
        {
            if (action == InterlockRuleAction.None)
            {
                return;
            }

            OperationResult r = action switch
            {
                InterlockRuleAction.PauseAllTests => _interlockController.PauseAllActiveTests(),
                InterlockRuleAction.ResumeAllTests => _interlockController.ResumeAllPausedTests(),
                InterlockRuleAction.StopAllTests => _interlockController.StopAllActiveTests(),
                _ => OperationResult.Succeed()
            };

            if (r.Success)
            {
                _logger.LogInformation(
                    "安全联锁: 规则 {Rule} IO={Value} 动作 {Action} 已执行",
                    string.IsNullOrWhiteSpace(ruleName) ? $"#{value}" : ruleName,
                    value,
                    action);
            }
            else
            {
                _logger.LogWarning(
                    "安全联锁: 规则 {Rule} 动作 {Action} 未成功: {Message}",
                    string.IsNullOrWhiteSpace(ruleName) ? "(未命名)" : ruleName,
                    action,
                    r.Message);
            }
        }

        private async Task<(int PollMs, List<SafetyInterlockRuleItem> Rules, bool InterlockActive)> LoadMergedAsync(
            CancellationToken cancellationToken)
        {
            var (enabled, pollMs) = await _globalOptions.GetOptionsAsync(cancellationToken).ConfigureAwait(false);
            if (!enabled)
            {
                return (100, new List<SafetyInterlockRuleItem>(), false);
            }

            var rules = _rulesProvider.GetRules();
            var list = rules.Count > 0 ? rules.ToList() : new List<SafetyInterlockRuleItem>();
            var active = enabled && list.Count > 0;
            return (pollMs, list, active);
        }
    }
}
