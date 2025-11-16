using Astra.Core.Plugins.Abstractions;

namespace Astra.Core.Plugins.Lifecycle
{
    /// <summary>
    /// 生命周期钩子接口
    /// </summary>
    public interface ILifecycleHook
    {
        Task OnInitializingAsync(IPlugin plugin) => Task.CompletedTask;
        Task OnInitializedAsync(IPlugin plugin) => Task.CompletedTask;
        Task OnStartingAsync(IPlugin plugin) => Task.CompletedTask;
        Task OnStartedAsync(IPlugin plugin) => Task.CompletedTask;
        Task OnStoppingAsync(IPlugin plugin) => Task.CompletedTask;
        Task OnStoppedAsync(IPlugin plugin) => Task.CompletedTask;
        Task OnDisposingAsync(IPlugin plugin) => Task.CompletedTask;
        Task OnDisposedAsync(IPlugin plugin) => Task.CompletedTask;
        Task OnErrorAsync(IPlugin plugin, Exception error) => Task.CompletedTask;
    }
}
