using System;
using System.Collections.Generic;
using Addins.Concurrency;
using Addins.Caching;
using Addins.Core.Models;

namespace Addins.Host
{
    /// <summary>
    /// 宿主配置
    /// </summary>
    public class HostConfiguration
    {
        public ServiceConfiguration Services { get; set; } = new();
        public PerformanceConfiguration Performance { get; set; } = new();
        public SecurityConfiguration Security { get; set; } = new();
        public string PluginDirectory { get; set; } = "./Plugins";
        public bool EnableHotReload { get; set; } = false;
        public bool RequireSignature { get; set; } = false;
    }

    /// <summary>
    /// 服务配置
    /// </summary>
    public class ServiceConfiguration
    {
        public List<Type> ManifestSerializers { get; set; } = new();
        public List<Type> ValidationRules { get; set; } = new();
        public bool EnableDefaultSerializers { get; set; } = true;
        public bool EnableDefaultValidationRules { get; set; } = true;
    }

    /// <summary>
    /// 性能配置
    /// </summary>
    public class PerformanceConfiguration
    {
        public bool EnablePerformanceMonitoring { get; set; } = true;
        public bool EnableMemoryManagement { get; set; } = true;
        public bool EnableConcurrencyControl { get; set; } = true;
        public bool EnableCaching { get; set; } = true;
        public int MaxConcurrentLoads { get; set; } = 4;
        public int MaxConcurrentDiscoveries { get; set; } = 8;
        public CacheOptions CacheOptions { get; set; } = new();
        public ConcurrencyConfig ConcurrencyConfig { get; set; } = new();

        /// <summary>
        /// 启用所有性能优化
        /// </summary>
        public void EnableAllOptimizations(bool enable = true)
        {
            EnablePerformanceMonitoring = enable;
            EnableMemoryManagement = enable;
            EnableConcurrencyControl = enable;
            EnableCaching = enable;
        }
    }

    /// <summary>
    /// 安全配置
    /// </summary>
    public class SecurityConfiguration
    {
        public bool RequireSignature { get; set; } = false;
        public bool EnableSandbox { get; set; } = true;
        public SandboxType SandboxType { get; set; } = SandboxType.AppDomain;
        public List<PluginPermissions> DefaultPermissions { get; set; } = new();
    }

    /// <summary>
    /// 沙箱类型
    /// </summary>
    public enum SandboxType
    {
        AppDomain,
        Process,
        None
    }
}
