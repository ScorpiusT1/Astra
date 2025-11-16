using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Lifecycle
{
    /// <summary>
    /// 资源追踪器 - 生命周期绑定资源管理
    /// </summary>
    public class ResourceTracker
    {
        private readonly ConcurrentDictionary<string, List<IDisposableResource>> _resources = new();
        private readonly ConcurrentDictionary<string, List<CancellationTokenSource>> _cancellationTokens = new();
        private readonly ConcurrentDictionary<string, List<Task>> _backgroundTasks = new();

        /// <summary>
        /// 注册可释放资源
        /// </summary>
        public void RegisterResource(string pluginId, IDisposable resource, string description = null)
        {
            var resources = _resources.GetOrAdd(pluginId, _ => new List<IDisposableResource>());
            lock (resources)
            {
                resources.Add(new DisposableResourceWrapper
                {
                    Resource = resource,
                    Description = description ?? resource.GetType().Name,
                    RegisteredAt = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// 注册文件资源
        /// </summary>
        public void RegisterFile(string pluginId, string filePath)
        {
            RegisterResource(pluginId, new FileResource(filePath), $"File: {filePath}");
        }

        /// <summary>
        /// 注册取消令牌
        /// </summary>
        public CancellationTokenSource RegisterCancellationToken(string pluginId)
        {
            var cts = new CancellationTokenSource();
            var tokens = _cancellationTokens.GetOrAdd(pluginId, _ => new List<CancellationTokenSource>());
            lock (tokens)
            {
                tokens.Add(cts);
            }
            return cts;
        }

        /// <summary>
        /// 注册后台任务
        /// </summary>
        public void RegisterBackgroundTask(string pluginId, Task task)
        {
            var tasks = _backgroundTasks.GetOrAdd(pluginId, _ => new List<Task>());
            lock (tasks)
            {
                tasks.Add(task);
            }
        }

        /// <summary>
        /// 释放插件的所有资源
        /// </summary>
        public async Task ReleasePluginResourcesAsync(string pluginId)
        {
            // 1. 取消所有后台任务
            if (_cancellationTokens.TryRemove(pluginId, out var tokens))
            {
                foreach (var cts in tokens)
                {
                    try
                    {
                        cts.Cancel();
                        cts.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error cancelling token: {ex.Message}");
                    }
                }
            }

            // 2. 等待后台任务完成
            if (_backgroundTasks.TryRemove(pluginId, out var tasks))
            {
                try
                {
                    await Task.WhenAll(tasks.Where(t => !t.IsCompleted)).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error waiting for background tasks: {ex.Message}");
                }
            }

            // 3. 释放资源
            if (_resources.TryRemove(pluginId, out var resources))
            {
                foreach (var resource in resources.AsEnumerable().Reverse())
                {
                    try
                    {
                        resource.Dispose();
                        Console.WriteLine($"Released resource: {resource.Description}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error disposing resource {resource.Description}: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 获取插件资源统计
        /// </summary>
        public ResourceStatistics GetStatistics(string pluginId)
        {
            return new ResourceStatistics
            {
                PluginId = pluginId,
                ResourceCount = _resources.TryGetValue(pluginId, out var res) ? res.Count : 0,
                CancellationTokenCount = _cancellationTokens.TryGetValue(pluginId, out var tokens) ? tokens.Count : 0,
                BackgroundTaskCount = _backgroundTasks.TryGetValue(pluginId, out var tasks) ? tasks.Count : 0
            };
        }
    }

    public interface IDisposableResource : IDisposable
    {
        string Description { get; }
        DateTime RegisteredAt { get; }
    }

    internal class DisposableResourceWrapper : IDisposableResource
    {
        public IDisposable Resource { get; set; }
        public string Description { get; set; }
        public DateTime RegisteredAt { get; set; }

        public void Dispose()
        {
            Resource?.Dispose();
        }
    }

    internal class FileResource : IDisposable
    {
        private readonly string _filePath;

        public FileResource(string filePath)
        {
            _filePath = filePath;
        }

        public void Dispose()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    File.Delete(_filePath);
                }
            }
            catch { }
        }
    }

    public class ResourceStatistics
    {
        public string PluginId { get; set; }
        public int ResourceCount { get; set; }
        public int CancellationTokenCount { get; set; }
        public int BackgroundTaskCount { get; set; }
    }
}
