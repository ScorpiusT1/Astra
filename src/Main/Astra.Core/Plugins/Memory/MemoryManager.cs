using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Memory
{
    /// <summary>
    /// 内存管理接口
    /// </summary>
    public interface IMemoryManager
    {
        Task<MemoryInfo> GetMemoryInfoAsync();
        Task CleanupAsync();
        Task<long> GetPluginMemoryUsageAsync(string pluginId);
        void RegisterPlugin(string pluginId, WeakReference pluginRef);
        void UnregisterPlugin(string pluginId);
        Task ForceGarbageCollectionAsync();
        Task<MemoryLeakReport> DetectMemoryLeaksAsync();
    }

    /// <summary>
    /// 内存信息
    /// </summary>
    public class MemoryInfo
    {
        public DateTime Timestamp { get; set; }
        public long TotalMemory { get; set; }
        public long AvailableMemory { get; set; }
        public long UsedMemory { get; set; }
        public long Gen0Collections { get; set; }
        public long Gen1Collections { get; set; }
        public long Gen2Collections { get; set; }
        public Dictionary<string, long> PluginMemoryUsage { get; set; } = new();
        public List<MemoryLeakInfo> PotentialLeaks { get; set; } = new();
    }

    /// <summary>
    /// 内存泄漏信息
    /// </summary>
    public class MemoryLeakInfo
    {
        public string PluginId { get; set; }
        public string ObjectType { get; set; }
        public long Size { get; set; }
        public DateTime FirstDetected { get; set; }
        public int ReferenceCount { get; set; }
    }

    /// <summary>
    /// 内存泄漏报告
    /// </summary>
    public class MemoryLeakReport
    {
        public DateTime GeneratedAt { get; set; }
        public List<MemoryLeakInfo> Leaks { get; set; } = new();
        public long TotalLeakedMemory { get; set; }
        public int LeakCount { get; set; }
    }

    /// <summary>
    /// 内存管理器实现
    /// </summary>
    public class MemoryManager : IMemoryManager, IDisposable
    {
        private readonly ConcurrentDictionary<string, WeakReference> _pluginReferences = new();
        private readonly ConcurrentDictionary<string, long> _pluginMemoryUsage = new();
        private readonly ConcurrentDictionary<string, DateTime> _pluginRegistrationTime = new();
        private readonly Timer _cleanupTimer;
        private readonly object _lock = new object();
        private bool _disposed = false;

        public MemoryManager()
        {
            // 每5分钟执行一次内存清理
            _cleanupTimer = new Timer(PerformScheduledCleanup, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        public async Task<MemoryInfo> GetMemoryInfoAsync()
        {
            return await Task.Run(() =>
            {
                var memoryInfo = new MemoryInfo
                {
                    Timestamp = DateTime.UtcNow,
                    TotalMemory = GC.GetTotalMemory(false),
                    Gen0Collections = GC.CollectionCount(0),
                    Gen1Collections = GC.CollectionCount(1),
                    Gen2Collections = GC.CollectionCount(2)
                };

                // 计算可用内存
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                memoryInfo.AvailableMemory = GC.GetTotalMemory(true);
                memoryInfo.UsedMemory = memoryInfo.TotalMemory - memoryInfo.AvailableMemory;

                // 获取插件内存使用情况
                foreach (var kvp in _pluginMemoryUsage)
                {
                    memoryInfo.PluginMemoryUsage[kvp.Key] = kvp.Value;
                }

                return memoryInfo;
            });
        }

        public async Task CleanupAsync()
        {
            await Task.Run(() =>
            {
                lock (_lock)
                {
                    // 清理无效的弱引用
                    var keysToRemove = new List<string>();
                    foreach (var kvp in _pluginReferences)
                    {
                        if (!kvp.Value.IsAlive)
                        {
                            keysToRemove.Add(kvp.Key);
                        }
                    }

                    foreach (var key in keysToRemove)
                    {
                        _pluginReferences.TryRemove(key, out _);
                        _pluginMemoryUsage.TryRemove(key, out _);
                        _pluginRegistrationTime.TryRemove(key, out _);
                    }

                    // 强制垃圾回收
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }
            });
        }

        public async Task<long> GetPluginMemoryUsageAsync(string pluginId)
        {
            return await Task.FromResult(_pluginMemoryUsage.GetValueOrDefault(pluginId, 0));
        }

        public void RegisterPlugin(string pluginId, WeakReference pluginRef)
        {
            _pluginReferences[pluginId] = pluginRef;
            _pluginRegistrationTime[pluginId] = DateTime.UtcNow;
            
            // 估算内存使用量
            var estimatedSize = EstimateObjectSize(pluginRef.Target);
            _pluginMemoryUsage[pluginId] = estimatedSize;
        }

        public void UnregisterPlugin(string pluginId)
        {
            _pluginReferences.TryRemove(pluginId, out _);
            _pluginMemoryUsage.TryRemove(pluginId, out _);
            _pluginRegistrationTime.TryRemove(pluginId, out _);
        }

        public async Task ForceGarbageCollectionAsync()
        {
            await Task.Run(() =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            });
        }

        public async Task<MemoryLeakReport> DetectMemoryLeaksAsync()
        {
            return await Task.Run(() =>
            {
                var report = new MemoryLeakReport
                {
                    GeneratedAt = DateTime.UtcNow
                };

                var currentTime = DateTime.UtcNow;
                var leakThreshold = TimeSpan.FromMinutes(30); // 30分钟未清理视为潜在泄漏

                foreach (var kvp in _pluginReferences)
                {
                    var pluginId = kvp.Key;
                    var reference = kvp.Value;
                    
                    if (_pluginRegistrationTime.TryGetValue(pluginId, out var registrationTime))
                    {
                        var age = currentTime - registrationTime;
                        
                        if (age > leakThreshold && reference.IsAlive)
                        {
                            var leakInfo = new MemoryLeakInfo
                            {
                                PluginId = pluginId,
                                ObjectType = reference.Target?.GetType().Name ?? "Unknown",
                                Size = _pluginMemoryUsage.GetValueOrDefault(pluginId, 0),
                                FirstDetected = registrationTime,
                                ReferenceCount = GetReferenceCount(reference.Target)
                            };

                            report.Leaks.Add(leakInfo);
                            report.TotalLeakedMemory += leakInfo.Size;
                        }
                    }
                }

                report.LeakCount = report.Leaks.Count;
                return report;
            });
        }

        private void PerformScheduledCleanup(object state)
        {
            if (!_disposed)
            {
                _ = Task.Run(async () => await CleanupAsync());
            }
        }

        private long EstimateObjectSize(object obj)
        {
            if (obj == null) return 0;

            try
            {
                // 使用反射估算对象大小
                var type = obj.GetType();
                var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                long size = IntPtr.Size; // 对象头
                
                foreach (var field in fields)
                {
                    var fieldType = field.FieldType;
                    if (fieldType.IsPrimitive)
                    {
                        size += GetPrimitiveSize(fieldType);
                    }
                    else if (fieldType == typeof(string))
                    {
                        var stringValue = field.GetValue(obj) as string;
                        size += stringValue?.Length * 2 + IntPtr.Size ?? IntPtr.Size; // Unicode字符
                    }
                    else
                    {
                        size += IntPtr.Size; // 引用
                    }
                }

                return Math.Max(size, 64); // 最小64字节
            }
            catch
            {
                return 1024; // 默认估算值
            }
        }

        private long GetPrimitiveSize(Type type)
        {
            if (type == typeof(bool)) return 1;
            if (type == typeof(byte)) return 1;
            if (type == typeof(sbyte)) return 1;
            if (type == typeof(short)) return 2;
            if (type == typeof(ushort)) return 2;
            if (type == typeof(int)) return 4;
            if (type == typeof(uint)) return 4;
            if (type == typeof(long)) return 8;
            if (type == typeof(ulong)) return 8;
            if (type == typeof(float)) return 4;
            if (type == typeof(double)) return 8;
            if (type == typeof(decimal)) return 16;
            if (type == typeof(char)) return 2;
            return IntPtr.Size;
        }

        private int GetReferenceCount(object obj)
        {
            // 这是一个简化的实现，实际应用中可能需要更复杂的引用计数
            return obj != null ? 1 : 0;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _cleanupTimer?.Dispose();
                
                // 清理所有引用
                _pluginReferences.Clear();
                _pluginMemoryUsage.Clear();
                _pluginRegistrationTime.Clear();
            }
        }
    }

    /// <summary>
    /// 内存优化配置
    /// </summary>
    public class MemoryOptimizationConfig
    {
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);
        public long MemoryThreshold { get; set; } = 100 * 1024 * 1024; // 100MB
        public TimeSpan LeakDetectionThreshold { get; set; } = TimeSpan.FromMinutes(30);
        public bool EnableAutomaticCleanup { get; set; } = true;
        public bool EnableLeakDetection { get; set; } = true;
    }
}
