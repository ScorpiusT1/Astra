# PluginSystem 文件结构重构建议

## 当前问题总结

### 1. **文件夹重复问题**
- `Core/Loading/` 和 `Loading/` 重复
- `Administration/` 和 `Management/` 功能重叠
- `UI/` 文件夹为空

### 2. **类职责过重**
- `HostBuilder` 类过于复杂（200+行）
- 缺少工厂类来创建复杂对象

### 3. **文档文件分散**
- 多个`.md`文件分散在根目录

## 重构方案

### 第一步：合并重复文件夹

#### 1.1 合并Loading文件夹
```bash
# 将HighPerformancePluginLoader.cs移动到Core/Loading/
mv Libraries/PluginSystem/Loading/HighPerformancePluginLoader.cs Libraries/PluginSystem/Core/Loading/
rmdir Libraries/PluginSystem/Loading/
```

#### 1.2 合并管理相关文件夹
```bash
# 将Administration内容移动到Management/Commands/
mkdir Libraries/PluginSystem/Management/Commands/
mv Libraries/PluginSystem/Administration/* Libraries/PluginSystem/Management/Commands/
rmdir Libraries/PluginSystem/Administration/
```

#### 1.3 删除空文件夹
```bash
rmdir Libraries/PluginSystem/UI/
```

### 第二步：创建文档文件夹
```bash
mkdir Libraries/PluginSystem/docs/
mkdir Libraries/PluginSystem/docs/architecture/
mkdir Libraries/PluginSystem/docs/guides/
mkdir Libraries/PluginSystem/docs/examples/

# 移动文档文件
mv Libraries/PluginSystem/*.md Libraries/PluginSystem/docs/
mv Libraries/PluginSystem/Examples/* Libraries/PluginSystem/docs/examples/
```

### 第三步：拆分复杂类

#### 3.1 拆分HostBuilder
```csharp
// 新的HostBuilder.cs
namespace PluginSystem.Host
{
    public class HostBuilder
    {
        private readonly HostConfiguration _config = new();
        
        public HostBuilder ConfigureServices(Action<ServiceConfiguration> configure)
        {
            configure(_config.Services);
            return this;
        }
        
        public HostBuilder ConfigurePerformance(Action<PerformanceConfiguration> configure)
        {
            configure(_config.Performance);
            return this;
        }
        
        public IPluginHost Build()
        {
            return PluginHostFactory.CreateHost(_config);
        }
    }
}

// 新的HostConfiguration.cs
namespace PluginSystem.Host
{
    public class HostConfiguration
    {
        public ServiceConfiguration Services { get; set; } = new();
        public PerformanceConfiguration Performance { get; set; } = new();
        public SecurityConfiguration Security { get; set; } = new();
        public string PluginDirectory { get; set; } = "./Plugins";
        public bool EnableHotReload { get; set; } = false;
        public bool RequireSignature { get; set; } = false;
    }
}

// 新的PluginHostFactory.cs
namespace PluginSystem.Host
{
    public static class PluginHostFactory
    {
        public static IPluginHost CreateHost(HostConfiguration config)
        {
            var serviceRegistry = CreateServiceRegistry(config.Services);
            var performanceServices = CreatePerformanceServices(config.Performance);
            
            var baseHost = new PluginHost(
                serviceRegistry.Resolve<IPluginDiscovery>(),
                serviceRegistry,
                serviceRegistry.Resolve<IMessageBus>(),
                serviceRegistry.Resolve<IPermissionManager>(),
                serviceRegistry.Resolve<IPluginValidator>(),
                serviceRegistry.Resolve<IExceptionHandler>(),
                serviceRegistry.Resolve<IErrorLogger>(),
                serviceRegistry.Resolve<IHealthCheckService>(),
                serviceRegistry.Resolve<ISelfHealingService>()
            );
            
            // 应用性能优化装饰器
            var optimizedHost = new ConcurrencyControlledPluginHost(baseHost, performanceServices.ConcurrencyManager);
            var cachedHost = new CachedPluginHost(optimizedHost, performanceServices.CacheManager);
            
            return cachedHost;
        }
        
        private static ServiceRegistry CreateServiceRegistry(ServiceConfiguration config)
        {
            var registry = new ServiceRegistry();
            // 注册服务...
            return registry;
        }
        
        private static PerformanceServices CreatePerformanceServices(PerformanceConfiguration config)
        {
            return new PerformanceServices
            {
                PerformanceMonitor = new PerformanceMonitor(),
                MemoryManager = new MemoryManager(),
                ConcurrencyManager = new ConcurrencyManager(),
                CacheManager = new CacheManager()
            };
        }
    }
}
```

### 第四步：统一命名空间

#### 4.1 更新命名空间
```csharp
// 所有核心类使用统一命名空间
namespace PluginSystem.Core.Abstractions
namespace PluginSystem.Core.Models
namespace PluginSystem.Core.Services
namespace PluginSystem.Core.Security
namespace PluginSystem.Core.Discovery
namespace PluginSystem.Core.Lifecycle
namespace PluginSystem.Core.Loading

// 功能模块使用功能命名空间
namespace PluginSystem.Performance
namespace PluginSystem.Memory
namespace PluginSystem.Concurrency
namespace PluginSystem.Caching
namespace PluginSystem.Security
namespace PluginSystem.Messaging
namespace PluginSystem.Validation
namespace PluginSystem.Exceptions
namespace PluginSystem.Health
namespace PluginSystem.Recovery
namespace PluginSystem.Management
namespace PluginSystem.Configuration
namespace PluginSystem.Dependencies
namespace PluginSystem.Manifest
namespace PluginSystem.Resources
namespace PluginSystem.Extensions
```

### 第五步：创建配置类

#### 5.1 创建配置类
```csharp
// ServiceConfiguration.cs
namespace PluginSystem.Host
{
    public class ServiceConfiguration
    {
        public List<Type> ManifestSerializers { get; set; } = new();
        public List<Type> ValidationRules { get; set; } = new();
        public bool EnableDefaultSerializers { get; set; } = true;
        public bool EnableDefaultValidationRules { get; set; } = true;
    }
}

// PerformanceConfiguration.cs
namespace PluginSystem.Host
{
    public class PerformanceConfiguration
    {
        public bool EnablePerformanceMonitoring { get; set; } = true;
        public bool EnableMemoryManagement { get; set; } = true;
        public bool EnableConcurrencyControl { get; set; } = true;
        public bool EnableCaching { get; set; } = true;
        public int MaxConcurrentLoads { get; set; } = 4;
        public int MaxConcurrentDiscoveries { get; set; } = 8;
        public CacheOptions CacheOptions { get; set; } = new();
    }
}

// SecurityConfiguration.cs
namespace PluginSystem.Host
{
    public class SecurityConfiguration
    {
        public bool RequireSignature { get; set; } = false;
        public bool EnableSandbox { get; set; } = true;
        public SandboxType SandboxType { get; set; } = SandboxType.AppDomain;
        public List<PluginPermissions> DefaultPermissions { get; set; } = new();
    }
}
```

## 重构后的文件结构

```
PluginSystem/
├── docs/                           # 文档文件夹
│   ├── architecture/               # 架构文档
│   │   ├── ARCHITECTURE_ANALYSIS.md
│   │   └── ARCHITECTURE_DIAGRAMS.md
│   ├── guides/                    # 使用指南
│   │   ├── PERFORMANCE_OPTIMIZATION_GUIDE.md
│   │   ├── README_ExceptionHandling.md
│   │   └── REFACTORING_UI_GUIDE.md
│   ├── examples/                  # 示例代码
│   │   ├── ExceptionHandlingExample.cs
│   │   └── PerformanceOptimizationExample.cs
│   └── fixes/                     # 修复文档
│       ├── ABSTRACT_CLASS_FIX.md
│       ├── COMPILATION_FIXES.md
│       └── OPTIMIZATION_COMPLETION_REPORT.md
├── src/                           # 源代码
│   ├── Core/                      # 核心层
│   │   ├── Abstractions/          # 抽象接口
│   │   │   ├── IPlugin.cs
│   │   │   ├── IPluginHost.cs
│   │   │   ├── IPluginContext.cs
│   │   │   ├── IServiceRegistry.cs
│   │   │   ├── IPluginMetadata.cs
│   │   │   ├── IDependencyResolver.cs
│   │   │   └── DependencyResolver.cs
│   │   ├── Models/                # 数据模型
│   │   │   ├── PluginDescriptor.cs
│   │   │   ├── PluginState.cs
│   │   │   ├── PluginPermissions.cs
│   │   │   ├── DependencyInfo.cs
│   │   │   └── VersionRange.cs
│   │   ├── Discovery/             # 发现机制
│   │   │   ├── IPluginDiscovery.cs
│   │   │   ├── FileSystemDiscovery.cs
│   │   │   └── TypeDiscovery.cs
│   │   ├── Lifecycle/             # 生命周期
│   │   │   ├── ILifecycleManager.cs
│   │   │   ├── ILifecycleHook.cs
│   │   │   ├── PluginLifecycleManager.cs
│   │   │   ├── LifecyclePhase.cs
│   │   │   ├── PluginLifecycleState.cs
│   │   │   └── ResourceTracker.cs
│   │   └── Loading/               # 加载机制（合并后）
│   │       ├── IPluginLoader.cs
│   │       ├── AssemblyPluginLoader.cs
│   │       ├── HighPerformancePluginLoader.cs
│   │       └── PluginLoadContext.cs
│   ├── Host/                      # 宿主层
│   │   ├── PluginHost.cs
│   │   ├── HostBuilder.cs
│   │   ├── HostConfiguration.cs
│   │   ├── ServiceConfiguration.cs
│   │   ├── PerformanceConfiguration.cs
│   │   ├── SecurityConfiguration.cs
│   │   └── PluginHostFactory.cs
│   ├── Services/                  # 服务层
│   │   ├── ServiceRegistry.cs
│   │   ├── ServiceDescriptor.cs
│   │   └── ServiceLifetime.cs
│   ├── Security/                  # 安全层
│   │   ├── IPermissionManager.cs
│   │   ├── ISandbox.cs
│   │   ├── AppDomainSandbox.cs
│   │   └── ProcessSandbox.cs
│   ├── Messaging/                 # 通信层
│   │   ├── IMessageBus.cs
│   │   └── MessageBus.cs
│   ├── Validation/                # 验证层
│   │   ├── IPluginValidator.cs
│   │   ├── IValidationRule.cs
│   │   ├── PluginValidator.cs
│   │   ├── ValidationResult.cs
│   │   ├── AssemblyExistsRule.cs
│   │   ├── DependencyValidRule.cs
│   │   ├── VersionValidRule.cs
│   │   └── ValidationRules/
│   │       ├── DependencyValidator.cs
│   │       ├── SecurityValidator.cs
│   │       └── SignatureValidator.cs
│   ├── Exceptions/                # 异常层
│   │   ├── PluginSystemExceptions.cs
│   │   ├── ExceptionHandler.cs
│   │   └── ErrorLogger.cs
│   ├── Health/                    # 健康层
│   │   └── HealthCheckService.cs
│   ├── Recovery/                  # 恢复层
│   │   └── SelfHealingService.cs
│   ├── Management/                # 管理层（合并后）
│   │   ├── IPluginManager.cs
│   │   ├── PluginManagementToolManager.cs
│   │   ├── PluginDebugTool.cs
│   │   ├── PluginConfigTool.cs
│   │   ├── Commands/              # 管理命令
│   │   │   ├── AdminCommand.cs
│   │   │   ├── CommandResult.cs
│   │   │   ├── CommandType.cs
│   │   │   ├── IPluginAdministrator.cs
│   │   │   ├── PluginAdministrator.cs
│   │   │   └── PluginInfo.cs
│   │   └── Console/               # 控制台
│   │       └── PluginManagementConsole.cs
│   ├── Configuration/             # 配置层
│   │   └── IConfigurationStore.cs
│   ├── Dependencies/              # 依赖层
│   │   ├── DependencyGraph.cs
│   │   ├── ConflictResolver.cs
│   │   ├── TopologicalSorter.cs
│   │   └── VersionResolver.cs
│   ├── Manifest/                  # 清单层
│   │   ├── AddinManifest.cs
│   │   ├── AddinInfo.cs
│   │   ├── AddinDependency.cs
│   │   ├── Extension.cs
│   │   ├── ExtensionPoint.cs
│   │   ├── ManifestValidator.cs
│   │   ├── PermissionsInfo.cs
│   │   ├── RuntimeInfo.cs
│   │   └── Serializers/
│   │       ├── XmlManifestSerializer.cs
│   │       ├── JsonManifestSerializer.cs
│   │       └── YamlManifestSerializer.cs
│   ├── Resources/                 # 资源层
│   │   ├── IIconProvider.cs
│   │   ├── EmbeddedIconProvider.cs
│   │   ├── FileIconProvider.cs
│   │   ├── UriIconProvider.cs
│   │   └── IconCache.cs
│   ├── Performance/              # 性能层
│   │   └── PerformanceMonitor.cs
│   ├── Memory/                    # 内存层
│   │   └── MemoryManager.cs
│   ├── Concurrency/               # 并发层
│   │   └── ConcurrencyManager.cs
│   ├── Caching/                   # 缓存层
│   │   └── CacheManager.cs
│   └── Extensions/                # 扩展层
│       ├── ServiceAttribute.cs
│       └── ServiceCollectionExtensions.cs
├── tests/                         # 测试文件夹
│   └── ServiceRegistryInterfaceTest.cs
└── PluginSystem.csproj           # 项目文件
```

## 重构步骤

### 1. 创建新文件夹结构
```bash
mkdir -p Libraries/PluginSystem/docs/{architecture,guides,examples,fixes}
mkdir -p Libraries/PluginSystem/src
mkdir -p Libraries/PluginSystem/tests
```

### 2. 移动文件
```bash
# 移动文档文件
mv Libraries/PluginSystem/*.md Libraries/PluginSystem/docs/
mv Libraries/PluginSystem/Examples/* Libraries/PluginSystem/docs/examples/

# 移动源代码
mv Libraries/PluginSystem/Core Libraries/PluginSystem/src/
mv Libraries/PluginSystem/Host Libraries/PluginSystem/src/
mv Libraries/PluginSystem/Services Libraries/PluginSystem/src/
# ... 其他文件夹

# 移动测试文件
mv Libraries/PluginSystem/Tests/* Libraries/PluginSystem/tests/
```

### 3. 更新项目文件
```xml
<!-- PluginSystem.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="YamlDotNet" Version="13.7.1" />
    <PackageReference Include="System.Text.Json" Version="9.0.10" />
  </ItemGroup>

  <ItemGroup>
    <Compile Include="src/**/*.cs" />
    <Compile Include="tests/**/*.cs" />
  </ItemGroup>
</Project>
```

## 重构后的优势

### 1. **结构更清晰**
- 文档集中管理
- 源代码和测试分离
- 功能模块更明确

### 2. **维护性更好**
- 类职责更单一
- 配置更灵活
- 工厂模式简化创建

### 3. **扩展性更强**
- 配置驱动
- 插件化架构
- 模块化设计

### 4. **命名更规范**
- 统一的命名空间
- 一致的命名规范
- 清晰的层次结构

## 总结

通过这次重构，PluginSystem的文件结构将更加合理，类的划分更加清晰，整体架构更加优雅。重构后的结构遵循了现代软件架构的最佳实践，为后续的开发和维护奠定了良好的基础。
