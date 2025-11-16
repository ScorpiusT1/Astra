using Astra.Core.Plugins.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Lifecycle
{
    /// <summary>
    /// 插件生命周期管理器实现
    /// </summary>
    public class PluginLifecycleManager : ILifecycleManager
    {
        private readonly ConcurrentDictionary<string, PluginLifecycleState> _states = new();
        private readonly List<ILifecycleHook> _hooks = new();
        private readonly ResourceTracker _resourceTracker;

        public PluginLifecycleManager()
        {
            _resourceTracker = new ResourceTracker();
        }

        public void RegisterHook(ILifecycleHook hook)
        {
            _hooks.Add(hook);
        }

        public async Task OnInitializingAsync(IPlugin plugin)
        {
            var state = GetOrCreateState(plugin.Id);
            state.Phase = LifecyclePhase.Initializing;
            state.LastTransition = DateTime.UtcNow;

            foreach (var hook in _hooks)
            {
                await hook.OnInitializingAsync(plugin);
            }
        }

        public async Task OnInitializedAsync(IPlugin plugin)
        {
            var state = GetOrCreateState(plugin.Id);
            state.Phase = LifecyclePhase.Initialized;
            state.LastTransition = DateTime.UtcNow;

            foreach (var hook in _hooks)
            {
                await hook.OnInitializedAsync(plugin);
            }
        }

        public async Task OnStartingAsync(IPlugin plugin)
        {
            var state = GetOrCreateState(plugin.Id);
            state.Phase = LifecyclePhase.Starting;
            state.LastTransition = DateTime.UtcNow;

            foreach (var hook in _hooks)
            {
                await hook.OnStartingAsync(plugin);
            }
        }

        public async Task OnStartedAsync(IPlugin plugin)
        {
            var state = GetOrCreateState(plugin.Id);
            state.Phase = LifecyclePhase.Running;
            state.LastTransition = DateTime.UtcNow;
            state.StartCount++;

            foreach (var hook in _hooks)
            {
                await hook.OnStartedAsync(plugin);
            }
        }

        public async Task OnStoppingAsync(IPlugin plugin)
        {
            var state = GetOrCreateState(plugin.Id);
            state.Phase = LifecyclePhase.Stopping;
            state.LastTransition = DateTime.UtcNow;

            foreach (var hook in _hooks)
            {
                await hook.OnStoppingAsync(plugin);
            }
        }

        public async Task OnStoppedAsync(IPlugin plugin)
        {
            var state = GetOrCreateState(plugin.Id);
            state.Phase = LifecyclePhase.Stopped;
            state.LastTransition = DateTime.UtcNow;
            state.StopCount++;

            // 清理该插件的所有资源
            await _resourceTracker.ReleasePluginResourcesAsync(plugin.Id);

            foreach (var hook in _hooks)
            {
                await hook.OnStoppedAsync(plugin);
            }
        }

        public async Task OnDisposingAsync(IPlugin plugin)
        {
            var state = GetOrCreateState(plugin.Id);
            state.Phase = LifecyclePhase.Disposing;
            state.LastTransition = DateTime.UtcNow;

            foreach (var hook in _hooks)
            {
                await hook.OnDisposingAsync(plugin);
            }
        }

        public async Task OnDisposedAsync(IPlugin plugin)
        {
            var state = GetOrCreateState(plugin.Id);
            state.Phase = LifecyclePhase.Disposed;
            state.LastTransition = DateTime.UtcNow;

            foreach (var hook in _hooks)
            {
                await hook.OnDisposedAsync(plugin);
            }

            _states.TryRemove(plugin.Id, out _);
        }

        public async Task OnErrorAsync(IPlugin plugin, Exception error)
        {
            var state = GetOrCreateState(plugin.Id);
            state.Phase = LifecyclePhase.Failed;
            state.LastError = error;
            state.ErrorCount++;
            state.LastTransition = DateTime.UtcNow;

            foreach (var hook in _hooks)
            {
                await hook.OnErrorAsync(plugin, error);
            }
        }

        public PluginLifecycleState GetState(string pluginId)
        {
            return _states.TryGetValue(pluginId, out var state) ? state : null;
        }

        public ResourceTracker ResourceTracker => _resourceTracker;

        private PluginLifecycleState GetOrCreateState(string pluginId)
        {
            return _states.GetOrAdd(pluginId, _ => new PluginLifecycleState
            {
                PluginId = pluginId,
                Phase = LifecyclePhase.Created
            });
        }
    }
}
