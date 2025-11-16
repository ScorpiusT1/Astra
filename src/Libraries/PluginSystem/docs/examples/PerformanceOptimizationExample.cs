using System;
using System.Threading.Tasks;
using Addins.Host;
using Addins.Host.Configuration;
using Addins.Performance;
using Addins.Memory;
using Addins.Concurrency;
using Addins.Caching;
using Addins.Core.Abstractions;

namespace Addins.Examples
{
    /// <summary>
    /// 性能优化使用示例
    /// </summary>
    public class PerformanceOptimizationExample
    {
        public static async Task RunExample()
        {
            Console.WriteLine("=== PluginSystem 性能优化示例 ===");

            // 1. 创建高性能插件宿主
            var host = new HostBuilder()
                .WithPluginDirectory("./Plugins")
                .ConfigurePerformance()
                    .EnableAllOptimizations()
                    .WithConcurrencyControl(maxConcurrentLoads: 4, maxConcurrentDiscoveries: 8)
                    .WithCaching(new CacheOptions
                    {
                        Expiration = TimeSpan.FromMinutes(30),
                        Priority = CachePriority.High,
                        MaxSize = 1000,
                        EvictionPolicy = CacheEvictionPolicy.LRU
                    })
                    .And()
                .Build();

            // 2. 获取性能监控服务
            var performanceMonitor = host.GetServiceAsync<IPerformanceMonitor>().Result;
            var memoryManager = host.GetServiceAsync<IMemoryManager>().Result;
            var concurrencyManager = host.GetServiceAsync<IConcurrencyManager>().Result;
            var cacheManager = host.GetServiceAsync<ICacheManager>().Result;

            // 3. 监控性能指标
            await MonitorPerformanceAsync(performanceMonitor, memoryManager, concurrencyManager, cacheManager);

            // 4. 测试并发加载
            await TestConcurrentLoadingAsync(host, concurrencyManager);

            // 5. 测试缓存性能
            await TestCachingPerformanceAsync(host, cacheManager);

            // 6. 内存泄漏检测
            await TestMemoryLeakDetectionAsync(memoryManager);

            Console.WriteLine("性能优化示例完成！");
        }

        private static async Task MonitorPerformanceAsync(
            IPerformanceMonitor performanceMonitor,
            IMemoryManager memoryManager,
            IConcurrencyManager concurrencyManager,
            ICacheManager cacheManager)
        {
            Console.WriteLine("\n--- 性能监控 ---");

            // 获取系统性能指标
            var systemMetrics = await performanceMonitor.GetSystemMetricsAsync();
            Console.WriteLine($"系统内存使用: {systemMetrics.TotalMemory / 1024 / 1024} MB");
            Console.WriteLine($"CPU使用率: {systemMetrics.CpuUsage:F2}%");
            Console.WriteLine($"插件数量: {systemMetrics.PluginCount}");

            // 获取内存信息
            var memoryInfo = await memoryManager.GetMemoryInfoAsync();
            Console.WriteLine($"总内存: {memoryInfo.TotalMemory / 1024 / 1024} MB");
            Console.WriteLine($"可用内存: {memoryInfo.AvailableMemory / 1024 / 1024} MB");
            Console.WriteLine($"Gen0回收次数: {memoryInfo.Gen0Collections}");

            // 获取并发报告
            var concurrencyReport = await concurrencyManager.GetConcurrencyReportAsync();
            Console.WriteLine($"活跃操作数: {concurrencyReport.TotalActiveOperations}");
            Console.WriteLine($"队列操作数: {concurrencyReport.TotalQueuedOperations}");
            Console.WriteLine($"平均等待时间: {concurrencyReport.AverageWaitTime:F2} ms");

            // 获取缓存报告
            var cacheReport = await cacheManager.GetCacheReportAsync();
            Console.WriteLine($"缓存项数: {cacheReport.TotalItems}");
            Console.WriteLine($"缓存命中率: {cacheReport.HitRatio:P2}");
            Console.WriteLine($"缓存大小: {cacheReport.TotalSize / 1024} KB");
        }

        private static async Task TestConcurrentLoadingAsync(IPluginHost host, IConcurrencyManager concurrencyManager)
        {
            Console.WriteLine("\n--- 并发加载测试 ---");

            var loadTasks = new Task[5];
            var startTime = DateTime.UtcNow;

            for (int i = 0; i < 5; i++)
            {
                int index = i;
                loadTasks[i] = Task.Run(async () =>
                {
                    try
                    {
                        await concurrencyManager.ExecuteWithConcurrencyControl(
                            async () =>
                            {
                                Console.WriteLine($"开始加载插件 {index}");
                                await Task.Delay(1000); // 模拟加载时间
                                Console.WriteLine($"完成加载插件 {index}");
                                return true;
                            },
                            $"LoadPlugin_{index}",
                            new ConcurrencyConfig
                            {
                                MaxConcurrency = 2,
                                Timeout = TimeSpan.FromMinutes(1)
                            }
                        );
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"插件 {index} 加载失败: {ex.Message}");
                    }
                });
            }

            await Task.WhenAll(loadTasks);
            var endTime = DateTime.UtcNow;
            Console.WriteLine($"并发加载完成，总耗时: {(endTime - startTime).TotalSeconds:F2} 秒");
        }

        private static async Task TestCachingPerformanceAsync(IPluginHost host, ICacheManager cacheManager)
        {
            Console.WriteLine("\n--- 缓存性能测试 ---");

            var testKey = "test_data";
            var testData = "这是测试数据";

            // 测试缓存设置
            var setStartTime = DateTime.UtcNow;
            await cacheManager.SetAsync(testKey, testData, new CacheOptions
            {
                Expiration = TimeSpan.FromMinutes(5),
                Priority = CachePriority.High
            });
            var setEndTime = DateTime.UtcNow;
            Console.WriteLine($"缓存设置耗时: {(setEndTime - setStartTime).TotalMilliseconds:F2} ms");

            // 测试缓存获取
            var getStartTime = DateTime.UtcNow;
            var cachedData = await cacheManager.GetAsync<string>(testKey);
            var getEndTime = DateTime.UtcNow;
            Console.WriteLine($"缓存获取耗时: {(getEndTime - getStartTime).TotalMilliseconds:F2} ms");
            Console.WriteLine($"缓存数据: {cachedData}");

            // 测试缓存命中率
            var hitStartTime = DateTime.UtcNow;
            for (int i = 0; i < 100; i++)
            {
                await cacheManager.GetAsync<string>(testKey);
            }
            var hitEndTime = DateTime.UtcNow;
            Console.WriteLine($"100次缓存获取耗时: {(hitEndTime - hitStartTime).TotalMilliseconds:F2} ms");

            // 获取缓存报告
            var cacheReport = await cacheManager.GetCacheReportAsync();
            Console.WriteLine($"缓存命中率: {cacheReport.HitRatio:P2}");
        }

        private static async Task TestMemoryLeakDetectionAsync(IMemoryManager memoryManager)
        {
            Console.WriteLine("\n--- 内存泄漏检测测试 ---");

            // 创建一些测试对象
            var testObjects = new object[100];
            for (int i = 0; i < 100; i++)
            {
                testObjects[i] = new { Id = i, Data = new string('x', 1000) };
                memoryManager.RegisterPlugin($"test_plugin_{i}", new WeakReference(testObjects[i]));
            }

            // 获取内存信息
            var memoryInfo = await memoryManager.GetMemoryInfoAsync();
            Console.WriteLine($"注册100个对象后内存使用: {memoryInfo.TotalMemory / 1024 / 1024} MB");

            // 检测内存泄漏
            var leakReport = await memoryManager.DetectMemoryLeaksAsync();
            Console.WriteLine($"检测到潜在泄漏: {leakReport.LeakCount} 个");
            Console.WriteLine($"泄漏内存总量: {leakReport.TotalLeakedMemory / 1024} KB");

            // 清理内存
            await memoryManager.CleanupAsync();
            Console.WriteLine("执行内存清理");

            // 获取清理后的内存信息
            var cleanedMemoryInfo = await memoryManager.GetMemoryInfoAsync();
            Console.WriteLine($"清理后内存使用: {cleanedMemoryInfo.TotalMemory / 1024 / 1024} MB");
        }

        /// <summary>
        /// 性能基准测试
        /// </summary>
        public static async Task RunBenchmarkAsync()
        {
            Console.WriteLine("\n=== 性能基准测试 ===");

            var host = new HostBuilder()
                .ConfigurePerformance()
                    .EnableAllOptimizations()
                    .WithConcurrencyControl(maxConcurrentLoads: 8)
                    .WithCaching()
                    .And()
                .Build();

            var performanceMonitor = host.GetServiceAsync<IPerformanceMonitor>().Result;

            // 测试不同操作的性能
            var operations = new[]
            {
                "PluginDiscovery",
                "PluginLoading",
                "PluginInitialization",
                "ServiceResolution",
                "MessagePublishing"
            };

            foreach (var operation in operations)
            {
                var iterations = 1000;
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                for (int i = 0; i < iterations; i++)
                {
                    performanceMonitor.RecordOperation(operation, TimeSpan.FromMilliseconds(1));
                }

                stopwatch.Stop();
                var averageTime = stopwatch.Elapsed.TotalMilliseconds / iterations;
                
                Console.WriteLine($"{operation}: {iterations} 次操作，平均耗时 {averageTime:F4} ms");
            }
        }
    }
}
