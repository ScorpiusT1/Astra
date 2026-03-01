using System;
using System.Collections.Generic;

namespace Astra.Core.Plugins.Performance
{
    /// <summary>
    /// 插件性能指标快照
    /// </summary>
    public class PerformanceMetrics
    {
        public string PluginId { get; set; }
        public DateTime Timestamp { get; set; }
        public long MemoryUsage { get; set; }
        public double CpuUsage { get; set; }
        public Dictionary<string, OperationMetrics> Operations { get; set; } = new();
        public TimeSpan TotalExecutionTime { get; set; }
        public int TotalOperations { get; set; }
        public double AverageOperationTime { get; set; }
    }

    /// <summary>
    /// 单个操作的性能统计（调用次数/最小/最大/平均耗时）
    /// </summary>
    public class OperationMetrics
    {
        public string OperationName { get; set; }
        public int CallCount { get; set; }
        public TimeSpan TotalTime { get; set; }
        public TimeSpan AverageTime { get; set; }
        public TimeSpan MinTime { get; set; }
        public TimeSpan MaxTime { get; set; }
        public DateTime LastCall { get; set; }
    }

    /// <summary>
    /// 系统级性能快照（含所有插件汇总）
    /// </summary>
    public class SystemPerformanceMetrics
    {
        public DateTime Timestamp { get; set; }
        public long TotalMemory { get; set; }
        public long AvailableMemory { get; set; }
        public double CpuUsage { get; set; }
        public int PluginCount { get; set; }
        public Dictionary<string, PerformanceMetrics> PluginMetrics { get; set; } = new();
    }
}
