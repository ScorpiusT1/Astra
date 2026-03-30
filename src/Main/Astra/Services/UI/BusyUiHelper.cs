using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Astra.UI.Abstractions.Services;

namespace Astra.Services.UI;

/// <summary>
/// 忙碌遮罩与 UI 刷新：在同一 UI 线程上若立刻执行耗时/导航逻辑，遮罩可能来不及完成首帧绘制。
/// </summary>
internal static class BusyUiHelper
{
    /// <summary>
    /// 让调度器处理到 ApplicationIdle，使 IsBusy 等绑定有机会完成布局与渲染后再继续。
    /// </summary>
    public static async Task AllowOverlayToPaintAsync()
    {
        var d = Application.Current?.Dispatcher;
        if (d == null)
            return;
        await d.InvokeAsync(static () => { }, DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// 保证遮罩至少可见指定时长，避免快速操作时“闪一下”。
    /// </summary>
    public static async Task EnsureMinimumVisibleAsync(DateTime shownAtUtc, int minimumVisibleMs = 350)
    {
        if (minimumVisibleMs <= 0)
            return;

        var elapsed = (int)(DateTime.UtcNow - shownAtUtc).TotalMilliseconds;
        var remaining = minimumVisibleMs - elapsed;
        if (remaining > 0)
            await Task.Delay(remaining);
    }

    /// <summary>
    /// 一行完成：忙碌遮罩 → 首帧绘制 → 执行异步操作 → 最短可见时长。
    /// </summary>
    public static async Task<T> RunWithNavigationBusyAsync<T>(
        IBusyService busyService,
        string message,
        Func<Task<T>> workAsync,
        BusyRequestOptions? options = null,
        int minimumVisibleMs = 500)
    {
        using (busyService.BeginBusy(message, options ?? new BusyRequestOptions { AllowCancel = false }))
        {
            await AllowOverlayToPaintAsync();
            var shownAtUtc = DateTime.UtcNow;
            var result = await workAsync();
            await EnsureMinimumVisibleAsync(shownAtUtc, minimumVisibleMs);
            return result;
        }
    }

    /// <summary>
    /// 无返回值版本（如仅导航、无结果对象）。
    /// </summary>
    public static async Task RunWithNavigationBusyAsync(
        IBusyService busyService,
        string message,
        Func<Task> workAsync,
        BusyRequestOptions? options = null,
        int minimumVisibleMs = 350)
    {
        using (busyService.BeginBusy(message, options ?? new BusyRequestOptions { AllowCancel = false }))
        {
            await AllowOverlayToPaintAsync();
            var shownAtUtc = DateTime.UtcNow;
            await workAsync();
            await EnsureMinimumVisibleAsync(shownAtUtc, minimumVisibleMs);
        }
    }

    /// <summary>
    /// 同步操作版本（如 <c>GoBack()</c> 非异步 API）。
    /// </summary>
    public static Task RunWithNavigationBusyAsync(
        IBusyService busyService,
        string message,
        Action syncWork,
        BusyRequestOptions? options = null,
        int minimumVisibleMs = 350) =>
        RunWithNavigationBusyAsync(
            busyService,
            message,
            () =>
            {
                syncWork();
                return Task.CompletedTask;
            },
            options,
            minimumVisibleMs);
}
