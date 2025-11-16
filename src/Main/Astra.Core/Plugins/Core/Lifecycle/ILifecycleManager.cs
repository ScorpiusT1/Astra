using Astra.Core.Plugins.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Lifecycle
{
    /// <summary>
    /// 插件生命周期管理器接口
    /// </summary>
    public interface ILifecycleManager
    {
        Task OnInitializingAsync(IPlugin plugin);
        Task OnInitializedAsync(IPlugin plugin);
        Task OnStartingAsync(IPlugin plugin);
        Task OnStartedAsync(IPlugin plugin);
        Task OnStoppingAsync(IPlugin plugin);
        Task OnStoppedAsync(IPlugin plugin);
        Task OnDisposingAsync(IPlugin plugin);
        Task OnDisposedAsync(IPlugin plugin);
        Task OnErrorAsync(IPlugin plugin, Exception error);
    }
}
