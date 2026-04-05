using Astra.Contract.Communication.Abstractions;
using Astra.Core.Configuration.Abstractions;
using Astra.Core.Devices.Interfaces;
using Astra.Core.Foundation.Common;
using Astra.UI.Abstractions.Home;
using Astra.Plugins.PLC.Configs;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Astra.Plugins.PLC.Services
{
    /// <summary>
    /// 将 IO 配置中勾选「首页监控」的点位绑定到首页模块并轮询刷新。
    /// </summary>
    internal sealed class PlcHomeIoMonitorRuntime : IHomeIoMonitorRuntime
    {
        private readonly PlcPlugin _plugin;
        private readonly object _gate = new();
        private ObservableCollection<IoMonitorPointItem>? _bound;

        /// <summary>Attach 时记录的 UI 调度器；Dispose 时 <see cref="Application.Current"/> 可能已为 null，不能依赖其 Dispatcher。</summary>
        private Dispatcher? _boundDispatcher;

        private CancellationTokenSource? _cts;
        private Task? _pollTask;
        private List<(IoPointModel Model, IoMonitorPointItem Item)> _rows = new();

        public PlcHomeIoMonitorRuntime(PlcPlugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        }

        public void Attach(ObservableCollection<IoMonitorPointItem> points)
        {
            Detach();
            _bound = points;
            _boundDispatcher = Application.Current?.Dispatcher;

            // 必须从 ConfigurationManager 拉全量 IO 配置：仅用插件内 _ioConfigs 可能与磁盘/缓存不一致，导致只显示部分点位。
            var configs = ResolveIoConfigsForAttach();

            lock (_gate)
            {
                _rows.Clear();
                points.Clear();

                foreach (var cfg in configs)
                {
                    if (cfg?.IOs == null)
                    {
                        continue;
                    }

                    foreach (var io in cfg.IOs)
                    {
                        if (io == null || !io.IsEnabled || !io.MonitorOnHome)
                        {
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(io.Name) || string.IsNullOrWhiteSpace(io.Address))
                        {
                            continue;
                        }

                        var item = new IoMonitorPointItem
                        {
                            Name = io.Name.Trim(),
                            Address = io.Address.Trim(),
                            Value = "…",
                            IsOn = false
                        };
                        points.Add(item);
                        _rows.Add((io, item));
                    }
                }

                if (_rows.Count == 0)
                {
                    return;
                }
            }

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _pollTask = Task.Run(() => PollLoopAsync(token), token);
        }

        /// <summary>
        /// 优先从 <see cref="IConfigurationManager"/> 取最新 IO 配置并写回插件缓存；失败时退回插件内存列表。
        /// </summary>
        private IReadOnlyList<IOConfig> ResolveIoConfigsForAttach()
        {
            var mgr = PlcPlugin.GetConfigurationManager();
            if (mgr != null)
            {
                try
                {
                    var r = Task.Run(async () => await mgr.GetAllAsync<IOConfig>().ConfigureAwait(false)).GetAwaiter().GetResult();
                    if (r.Success && r.Data != null)
                    {
                        var list = r.Data.ToList();
                        _plugin.SyncIoConfigsCache(list);
                        return list;
                    }
                }
                catch
                {
                    // 回退到插件缓存
                }
            }

            return _plugin.GetAllIoConfigs().ToList();
        }

        public void Detach()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _pollTask = null;

            var uiDispatcher = _boundDispatcher;
            ObservableCollection<IoMonitorPointItem>? bound;
            lock (_gate)
            {
                _rows.Clear();
                bound = _bound;
                _bound = null;
            }

            _boundDispatcher = null;

            // ObservableCollection 由首页 VM 绑定到 UI；Dispose/作用域释放可能在非 UI 线程触发，必须在创建绑定时记录的 Dispatcher 上 Clear。
            if (bound != null)
            {
                ClearBoundCollectionOnUiThread(bound, uiDispatcher);
            }
        }

        private static void ClearBoundCollectionOnUiThread(ObservableCollection<IoMonitorPointItem> bound, Dispatcher? capturedUiDispatcher)
        {
            var dispatcher = capturedUiDispatcher ?? Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                // 应用已退出或未拿到 UI 调度器：无法安全修改仍可能挂在 CollectionView 上的集合，跳过以免跨线程异常。
                return;
            }

            void ClearSafe()
            {
                bound.Clear();
            }

            if (dispatcher.CheckAccess())
            {
                ClearSafe();
                return;
            }

            try
            {
                if (dispatcher.HasShutdownStarted)
                    return;

                // 必须用 BeginInvoke：插件卸载在线程池执行时，同步 Invoke 会与主线程上
                // CleanupCriticalResources().GetResult() 互相等待导致死锁。
                dispatcher.BeginInvoke(DispatcherPriority.Send, ClearSafe);
            }
            catch
            {
                // 关闭过程中投递失败则放弃清理
            }
        }

        public void ReloadFromConfiguration()
        {
            ObservableCollection<IoMonitorPointItem>? pts;
            lock (_gate)
            {
                pts = _bound;
            }

            if (pts == null)
            {
                return;
            }

            Attach(pts);
        }

        private async Task PollLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await ReadAllOnceAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // 单次轮询失败，继续下一轮
                }

                try
                {
                    await Task.Delay(450, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task ReadAllOnceAsync(CancellationToken ct)
        {
            List<(IoPointModel Model, IoMonitorPointItem Item)> snapshot;
            lock (_gate)
            {
                snapshot = _rows.ToList();
            }

            if (snapshot.Count == 0)
            {
                return;
            }

            var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

            foreach (var (model, item) in snapshot)
            {
                ct.ThrowIfCancellationRequested();

                var plc = ResolvePlc(model);
                if (plc == null)
                {
                    await dispatcher.InvokeAsync(() =>
                    {
                        item.Value = "无PLC";
                        item.IsOn = false;
                    }, DispatcherPriority.Normal);
                    continue;
                }

                var connectResult = await EnsureConnectedAsync(plc, ct).ConfigureAwait(false);
                if (!connectResult.Success)
                {
                    await dispatcher.InvokeAsync(() =>
                    {
                        item.Value = "离线";
                        item.IsOn = false;
                    }, DispatcherPriority.Normal);
                    continue;
                }

                var read = await plc.ReadAsync<object>(model.Address.Trim(), ct).ConfigureAwait(false);
                if (!read.Success)
                {
                    await dispatcher.InvokeAsync(() =>
                    {
                        item.Value = "读取失败";
                        item.IsOn = false;
                    }, DispatcherPriority.Normal);
                    continue;
                }

                var raw = read.Data;
                if (raw != null && model.TryApplyScaleOffset(raw, out var scaled))
                {
                    raw = scaled;
                }

                await dispatcher.InvokeAsync(() => ApplyDisplay(model, raw, item), DispatcherPriority.Normal);
            }
        }

        private static void ApplyDisplay(IoPointModel model, object? raw, IoMonitorPointItem item)
        {
            switch (raw)
            {
                case bool b:
                    item.Value = b ? "True" : "False";
                    item.IsOn = b;
                    return;
                case null:
                    item.Value = "—";
                    item.IsOn = false;
                    return;
            }

            if (model.DataType == PlcIODataType.Bool && raw is not bool)
            {
                if (TryCoerceBool(raw, out var asBool))
                {
                    item.Value = asBool ? "True" : "False";
                    item.IsOn = asBool;
                    return;
                }
            }

            item.Value = FormatValue(raw);
            item.IsOn = true;
        }

        private static bool TryCoerceBool(object raw, out bool b)
        {
            switch (raw)
            {
                case bool bb:
                    b = bb;
                    return true;
                case byte by:
                    b = by != 0;
                    return true;
                case short s:
                    b = s != 0;
                    return true;
                case int i:
                    b = i != 0;
                    return true;
                case long l:
                    b = l != 0;
                    return true;
                default:
                    if (raw is IConvertible)
                    {
                        try
                        {
                            b = Convert.ToInt32(raw, CultureInfo.InvariantCulture) != 0;
                            return true;
                        }
                        catch
                        {
                        }
                    }

                    b = false;
                    return false;
            }
        }

        private static string FormatValue(object raw)
        {
            return raw switch
            {
                float f => f.ToString("G6", CultureInfo.InvariantCulture),
                double d => d.ToString("G8", CultureInfo.InvariantCulture),
                IFormattable fmt => fmt.ToString(null, CultureInfo.InvariantCulture),
                _ => raw.ToString() ?? string.Empty
            };
        }

        private IPLC? ResolvePlc(IoPointModel model)
        {
            var name = model.PlcDeviceName?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return _plugin.GetAllPlcs().FirstOrDefault();
            }

            return _plugin.FindPlcByDeviceName(name);
        }

        private static async Task<OperationResult> EnsureConnectedAsync(IPLC plc, CancellationToken cancellationToken)
        {
            if (plc is not IDevice device)
            {
                return OperationResult.Failure("PLC 实例不支持 IDevice 接口");
            }

            if (device.IsOnline)
            {
                return OperationResult.Succeed("PLC 已在线");
            }

            return await device.ConnectAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
