// ⭐ 已移除 using Astra.Bootstrap.Core;（不再需要 IBootstrapTask）
using Astra.Bootstrap.Core;
using Astra.Bootstrap.UI;
using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.Dependencies;
using Astra.Core.Plugins.Discovery;
using Astra.Core.Plugins.Models;
using Astra.Core.Plugins.Validation;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace Astra.Bootstrap.Services
{
    /// <summary>
    /// 应用程序启动管理器
    /// </summary>
    public class ApplicationBootstrapper
    {
        // ⭐ 已移除任务系统，不再需要 _tasks 列表
        private readonly BootstrapContext _context = new BootstrapContext();
        private SplashScreenView _splashScreen;
        private CancellationTokenSource _cancellationTokenSource;

        public ApplicationBootstrapper()
        {
            _cancellationTokenSource = new CancellationTokenSource();
        }

        #region Configuration

        // ⭐ 已移除 AddTask 方法（任务系统已移除）

        public ApplicationBootstrapper ConfigureServices(Action<IServiceCollection> configure)
        {
            if (_context.Services == null)
            {
                _context.Services = new ServiceCollection();
            }
            configure?.Invoke(_context.Services);
            return this;
        }

        public ApplicationBootstrapper UseLogger(IBootstrapLogger logger)
        {
            _context.Logger = logger;
            return this;
        }

        public ApplicationBootstrapper WithCommandLineArgs(string[] args)
        {
            _context.CommandLineArgs = args;
            return this;
        }

        public ApplicationBootstrapper ConfigureSplashScreen(Action<SplashScreenOptions> configure)
        {
            var options = new SplashScreenOptions();
            configure?.Invoke(options);
            _context.SetData("SplashScreenOptions", options);
            return this;
        }

        #endregion

        #region Execution

        /// <summary>
        /// 运行启动流程
        /// </summary>
        public async Task<BootstrapResult> RunAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new BootstrapResult();
            var cancellationToken = _cancellationTokenSource.Token;

            try
            {
                _context.Logger?.LogInfo("=== 应用程序启动开始 ===");

                // 1. 显示启动画面
                ShowSplashScreen();

                // ⭐ 等待启动画面完全加载后再继续（确保窗口渲染完成，进度条可以正确计算宽度）
                UpdateSplashScreen(0, "正在启动...", null);
                await Task.Delay(100, cancellationToken);

                // ⭐ 检查是否已取消
                if (cancellationToken.IsCancellationRequested)
                {
                    _context.Logger?.LogWarning("启动流程已被用户取消");
                    result.IsCancelled = true;
                    result.TotalTime = stopwatch.Elapsed;
                    stopwatch.Stop();
                    return result;
                }

                // ⭐ 2. 构建服务提供者（不再需要执行任务）
                // 进度分配：5% ~ 40%（服务注册和构建占 35%）
                if (_context.Services != null)
                {
                    UpdateSplashScreen(5, "正在初始化服务...", null);
                    await Task.Delay(50, cancellationToken); // 短暂延迟，确保 UI 更新可见
                    
                    // ⭐ 检查是否已取消
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _context.Logger?.LogWarning("启动流程已被用户取消");
                        result.IsCancelled = true;
                        result.TotalTime = stopwatch.Elapsed;
                        stopwatch.Stop();
                        return result;
                    }
                    
                    UpdateSplashScreen(15, "正在注册服务...", null);
                    await Task.Delay(50, cancellationToken);
                    
                    // ⭐ 检查是否已取消
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _context.Logger?.LogWarning("启动流程已被用户取消");
                        result.IsCancelled = true;
                        result.TotalTime = stopwatch.Elapsed;
                        stopwatch.Stop();
                        return result;
                    }
                    
                    // 在构建服务提供者之前，将 BootstrapContext 注册到服务集合中
                    // 这样其他组件可以通过 DI 容器访问 BootstrapContext 和其中的数据（如 PluginHost）
                    _context.Services.AddSingleton(_context);
                    
                    UpdateSplashScreen(30, "正在构建服务容器...", null);
                    await Task.Delay(50, cancellationToken);
                   
                    // ⭐ 检查是否已取消
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _context.Logger?.LogWarning("启动流程已被用户取消");
                        result.IsCancelled = true;
                        result.TotalTime = stopwatch.Elapsed;
                        stopwatch.Stop();
                        return result;
                    }
                   
                    _context.ServiceProvider = _context.Services.BuildServiceProvider();
                    
                    UpdateSplashScreen(40, "服务构建完成", null);
                    await Task.Delay(50, cancellationToken);
                  
                    // ⭐ 检查是否已取消
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _context.Logger?.LogWarning("启动流程已被用户取消");
                        result.IsCancelled = true;
                        result.TotalTime = stopwatch.Elapsed;
                        stopwatch.Stop();
                        return result;
                    }
                  
                    // ⭐ 获取 IPluginHost（通过工厂方法创建，会自动使用主应用的 ServiceProvider）
                    // 工厂方法在第一次解析时执行，此时 ServiceProvider 已经构建完成
                    // 工厂方法会传入主应用的 ServiceProvider 来创建 IPluginHost
                    try
                    {
                        var pluginHost = _context.ServiceProvider.GetService<IPluginHost>();

                        if (pluginHost != null)
                        {
                            _context.Logger?.LogInfo("插件系统已创建并使用主程序的全局 ServiceProvider，开始加载插件");
                            
                            // ⭐ 在所有服务构建完成后，加载插件
                            await LoadPluginsAfterServiceProviderBuilt(pluginHost, cancellationToken);
                            
                            // ⭐ 检查是否已取消（插件加载过程中可能被取消）
                            if (cancellationToken.IsCancellationRequested)
                            {
                                _context.Logger?.LogWarning("启动流程已被用户取消");
                                result.IsCancelled = true;
                                result.TotalTime = stopwatch.Elapsed;
                                stopwatch.Stop();
                                return result;
                            }
                        }
                        else
                        {
                            _context.Logger?.LogWarning("IPluginHost 为 null，跳过插件加载");
                        }
                    }
                    catch (Exception ex)
                    {
                        _context.Logger?.LogError($"获取 PluginHost 或加载插件失败：{ex.Message}", ex);
                        // 非关键错误，不影响启动流程
                    }
                }

                // ⭐ 最终检查是否已取消
                if (cancellationToken.IsCancellationRequested)
                {
                    _context.Logger?.LogWarning("启动流程已被用户取消");
                    result.IsCancelled = true;
                    result.TotalTime = stopwatch.Elapsed;
                    stopwatch.Stop();
                    return result;
                }

                stopwatch.Stop();
                result.IsSuccess = true;
                result.TotalTime = stopwatch.Elapsed;

                _context.Logger?.LogInfo($"=== 应用程序启动完成，耗时：{stopwatch.ElapsedMilliseconds}ms ===");

                // 6. 显示完成状态（90% ~ 100%，占 10%）
                UpdateSplashScreen(95, "正在完成启动...", null);
                await Task.Delay(100, cancellationToken);
                
                // ⭐ 检查是否已取消
                if (cancellationToken.IsCancellationRequested)
                {
                    _context.Logger?.LogWarning("启动流程已被用户取消");
                    result.IsCancelled = true;
                    result.TotalTime = stopwatch.Elapsed;
                    return result;
                }
                
                // ⭐ 更新到100%，确保进度条完全填满
                UpdateSplashScreen(100, "启动完成", null);
                // ⭐ 等待动画完成（进度条动画300ms + 额外缓冲时间）
                await Task.Delay(350, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // ⭐ 捕获取消异常，设置取消标志
                stopwatch.Stop();
                result.IsCancelled = true;
                result.TotalTime = stopwatch.Elapsed;
                _context.Logger?.LogWarning("启动流程已被用户取消（OperationCanceledException）");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.IsSuccess = false;
                result.TotalTime = stopwatch.Elapsed;
                result.FatalException = ex;

                _context.Logger?.LogError("启动过程发生致命错误", ex);

                // 显示错误
                _splashScreen?.ShowError(ex.Message);
                try
                {
                    await Task.Delay(3000, cancellationToken); // 让用户看到错误
                }
                catch (OperationCanceledException)
                {
                    // 如果取消，直接返回
                    result.IsCancelled = true;
                }

                // ⭐ 不再需要回滚任务（因为没有任务系统）
            }
            finally
            {
                CloseSplashScreen();
            }

            return result;
        }

        // ⭐ 已移除任务执行和回滚方法（任务系统已移除）

        public void Cancel()
        {
            _cancellationTokenSource?.Cancel();
        }

        #endregion

        #region Splash Screen

        private void ShowSplashScreen()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var options = _context.GetData<SplashScreenOptions>("SplashScreenOptions")
                    ?? new SplashScreenOptions();

                _splashScreen = new SplashScreenView(options);

                // ⭐ 订阅取消事件
                _splashScreen.Cancelled += OnSplashScreenCancelled;

                _splashScreen.Show();
            });
        }

        /// <summary>
        /// 启动画面取消事件处理
        /// </summary>
        private void OnSplashScreenCancelled(object sender, EventArgs e)
        {
            _context.Logger?.LogWarning("用户取消了启动流程");

            // 触发取消令牌
            _cancellationTokenSource?.Cancel();
        }

        private void UpdateSplashScreen(double progress, string message, string details)
        {
            // ⭐ 使用 Invoke 而不是 InvokeAsync，确保更新及时执行
            Application.Current.Dispatcher.Invoke(() =>
            {
                _splashScreen?.UpdateProgress(progress, message, details);
            }, DispatcherPriority.Normal);
        }

        private void CloseSplashScreen()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_splashScreen != null)
                {
                    // 取消订阅
                    _splashScreen.Cancelled -= OnSplashScreenCancelled;

                    // 带动画关闭
                    _splashScreen.CloseWithAnimation();
                    _splashScreen = null;
                }
            });
        }

        #endregion

        public BootstrapContext GetContext()
        {
            return _context;
        }

        /// <summary>
        /// 在所有服务构建完成后加载插件
        /// ⭐ 这样可以确保插件系统使用主程序构建的全局 ServiceProvider
        /// 流程：注册服务 → 构建 ServiceProvider → 创建 IPluginHost → 加载插件
        /// </summary>
        private async Task LoadPluginsAfterServiceProviderBuilt(IPluginHost pluginHost, CancellationToken cancellationToken)
        {
            try
            {
                // ⭐ 优先从 BootstrapContext 获取插件目录（如果之前有任务设置了）
                var pluginDirectory = _context.GetData<string>("PluginDirectory");
                
                // 如果 BootstrapContext 中没有，使用默认插件目录
                if (string.IsNullOrEmpty(pluginDirectory))
                {
                    pluginDirectory = Path.Combine(
                        System.AppDomain.CurrentDomain.BaseDirectory,
                        "Plugins");
                    
                    // 确保插件目录存在
                    if (!Directory.Exists(pluginDirectory))
                    {
                        try
                        {
                            Directory.CreateDirectory(pluginDirectory);
                            _context.Logger?.LogInfo($"插件目录不存在，已创建: {pluginDirectory}");
                        }
                        catch (Exception ex)
                        {
                            _context.Logger?.LogWarning($"创建插件目录失败: {ex.Message}，跳过插件加载");
                            UpdateSplashScreen(90, "插件目录创建失败", null);
                            return;
                        }
                    }
                }

                // ⭐ 插件加载进度分配：40% ~ 90%（插件加载占 50%）
                // 40%: 开始扫描插件
                // 40-45%: 发现插件（5%）
                // 45-65%: 验证插件（20%，根据插件数量分配）
                // 65%: 分析依赖
                // 70-90%: 加载插件（20%，根据插件数量分配）
                // 90%: 插件加载完成
                
                UpdateSplashScreen(40, "正在扫描插件目录...", null);
                await Task.Delay(50, cancellationToken); // 短暂延迟，确保 UI 更新可见
                
                // ⭐ 检查是否已取消
                cancellationToken.ThrowIfCancellationRequested();
                
                _context.Logger?.LogInfo($"开始扫描插件目录: {pluginDirectory}");

                // ⭐ 手动分步执行插件发现和加载，以便报告详细进度
                // 1. 从 ServiceProvider 获取插件发现服务
                var pluginDiscovery = _context.ServiceProvider?.GetService<IPluginDiscovery>();
                var pluginValidator = _context.ServiceProvider?.GetService<IPluginValidator>();
                
                if (pluginDiscovery == null)
                {
                    // 如果没有发现服务，回退到自动加载方式
                    _context.Logger?.LogWarning("IPluginDiscovery 服务未找到，使用自动加载方式");
                    UpdateSplashScreen(45, "正在发现并加载插件...", null);
                    await pluginHost.DiscoverAndLoadPluginsAsync(pluginDirectory);
                    cancellationToken.ThrowIfCancellationRequested();
                    UpdateSplashScreen(90, "插件加载完成", null);
                }
                else
                {
                    // 2. 发现插件（40% ~ 45%，占 5%）
                    UpdateSplashScreen(42, "正在发现插件...", null);
                    await Task.Delay(50, cancellationToken);
                    
                    // ⭐ 检查是否已取消
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var descriptors = (await pluginDiscovery.DiscoverAsync(pluginDirectory)).ToList();
                    var totalCount = descriptors.Count;
                    
                    UpdateSplashScreen(45, $"已发现 {totalCount} 个插件", null);
                    await Task.Delay(50, cancellationToken);
                    
                    // ⭐ 检查是否已取消
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    _context.Logger?.LogInfo($"发现 {totalCount} 个插件");
                    
                    if (totalCount == 0)
                    {
                        UpdateSplashScreen(90, "未发现插件", null);
                    }
                    else
                    {
                        // 3. 验证插件（45% ~ 65%，占 20%，根据插件数量分配）
                        UpdateSplashScreen(45, "正在验证插件...", null);
                        await Task.Delay(50, cancellationToken);
                        
                        // ⭐ 检查是否已取消
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        var validDescriptors = new List<PluginDescriptor>();
                        var validatedCount = 0;
                        
                        if (pluginValidator != null)
                        {
                            foreach (var descriptor in descriptors)
                            {
                                // ⭐ 在循环中检查取消
                                cancellationToken.ThrowIfCancellationRequested();
                                
                                var validationResult = await pluginValidator.ValidateAsync(descriptor);
                                validatedCount++;
                                
                                // 更新验证进度：45% ~ 65%
                                var validateProgress = 45 + (validatedCount * 20.0 / totalCount);
                                UpdateSplashScreen(validateProgress, $"正在验证插件 ({validatedCount}/{totalCount})...", $"插件: {descriptor.Name ?? descriptor.Id}");
                                await Task.Delay(20, cancellationToken); // 短暂延迟，确保 UI 更新可见
                                
                                if (validationResult.IsValid)
                                {
                                    validDescriptors.Add(descriptor);
                                }
                                else
                                {
                                    _context.Logger?.LogWarning($"插件验证失败: {descriptor.Id}");
                                    foreach (var error in validationResult.Errors)
                                    {
                                        _context.Logger?.LogWarning($"  - {error}");
                                    }
                                }
                            }
                        }
                        else
                        {
                            // 如果没有验证器，直接使用所有发现的插件
                            validDescriptors = descriptors;
                        }
                        
                        // ⭐ 检查是否已取消
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        var validCount = validDescriptors.Count;
                        _context.Logger?.LogInfo($"验证完成，有效插件: {validCount}/{totalCount}");
                        
                        UpdateSplashScreen(65, $"验证完成，有效插件: {validCount}/{totalCount}", null);
                        await Task.Delay(50, cancellationToken);
                        
                        // ⭐ 检查是否已取消
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        // 4. 构建依赖图并排序（65% ~ 70%，占 5%）
                        UpdateSplashScreen(65, "正在分析插件依赖...", null);
                        await Task.Delay(50, cancellationToken);
                        
                        // ⭐ 检查是否已取消
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        var graph = new DependencyGraph();
                        foreach (var descriptor in validDescriptors)
                        {
                            graph.AddPlugin(descriptor);
                        }
                        
                        foreach (var descriptor in validDescriptors)
                        {
                            foreach (var dep in descriptor.Dependencies)
                            {
                                // 只添加在有效插件列表中存在的依赖
                                if (validDescriptors.Any(d => d.Id == dep.PluginId))
                                {
                                    graph.AddDependency(descriptor.Id, dep.PluginId);
                                }
                                else if (!dep.IsOptional)
                                {
                                    _context.Logger?.LogWarning($"插件 {descriptor.Id} 的必需依赖 {dep.PluginId} 未找到");
                                }
                            }
                        }
                        
                        // 检查循环依赖
                        if (graph.HasCycle())
                        {
                            _context.Logger?.LogError("检测到插件循环依赖");
                            UpdateSplashScreen(90, "插件依赖错误：检测到循环依赖", null);
                            throw new InvalidOperationException("插件依赖图中存在循环依赖");
                        }
                        
                        // 拓扑排序，获取按依赖顺序排列的插件列表
                        var sortedDescriptors = graph.TopologicalSort();
                        var sortedCount = sortedDescriptors.Count;
                        
                        _context.Logger?.LogInfo($"依赖分析完成，将按顺序加载 {sortedCount} 个插件");
                        
                        UpdateSplashScreen(70, $"依赖分析完成，准备加载 {sortedCount} 个插件...", null);
                        await Task.Delay(50, cancellationToken);
                        
                        // ⭐ 检查是否已取消
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        // ⚠️ 关键：将 loadedAssemblies 提升到外部作用域，以便在阶段2中使用
                        var loadedAssemblies = new List<System.Reflection.Assembly>();
                        
                        // ⭐ 阶段1：先只加载插件程序集（不初始化），用于扫描 ConfigProvider
                        // 这样可以确保所有 Provider 在插件初始化前已经注册
                        UpdateSplashScreen(70, "正在加载插件程序集...", null);
                        
                        if (sortedCount > 0)
                        {
                            // 逐个加载插件程序集（不初始化）
                            for (int i = 0; i < sortedDescriptors.Count; i++)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                
                                var descriptor = sortedDescriptors[i];
                                
                                try
                                {
                                    if (!string.IsNullOrEmpty(descriptor.AssemblyPath) && File.Exists(descriptor.AssemblyPath))
                                    {
                                        // 只加载程序集，不创建实例和初始化
                                        var assembly = System.Reflection.Assembly.LoadFrom(descriptor.AssemblyPath);
                                        loadedAssemblies.Add(assembly);
                                        _context.Logger?.LogInfo($"已加载插件程序集: {descriptor.Name ?? descriptor.Id} ({assembly.GetName().Name})");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _context.Logger?.LogWarning($"加载插件程序集失败: {descriptor.Name ?? descriptor.Id}, 错误: {ex.Message}");
                                    // 继续加载下一个，不阻止流程
                                }
                            }
                            
                            _context.Logger?.LogInfo($"已加载 {loadedAssemblies.Count}/{sortedCount} 个插件程序集");
                        }
                        
                        await Task.Delay(50, cancellationToken);
                        
                        // ⭐ 阶段2：扫描所有程序集并注册 ConfigProvider
                        UpdateSplashScreen(75, "正在扫描并注册配置提供者...", null);
                        
                        try
                        {
                            var configProviderDiscovery = _context.ServiceProvider?.GetService<Astra.Core.Configuration.ConfigProviderDiscovery>();
                            if (configProviderDiscovery != null)
                            {
                                _context.Logger?.LogInfo("开始扫描所有程序集中的 ConfigProvider...");
                                
                                // ⚠️ 关键：确保已加载的插件程序集都被扫描
                                // 先获取 AppDomain 中已有的程序集
                                var appDomainAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                                    .Where(a => !a.IsDynamic)
                                    .ToDictionary(a => a.FullName ?? a.GetName().FullName, a => a);
                                
                                // 将阶段1中加载的插件程序集也加入扫描列表（如果它们不在 AppDomain 中）
                                var assembliesToScan = new List<System.Reflection.Assembly>(appDomainAssemblies.Values);
                                
                                if (loadedAssemblies != null && loadedAssemblies.Count > 0)
                                {
                                    foreach (var loadedAssembly in loadedAssemblies)
                                    {
                                        var assemblyKey = loadedAssembly.FullName ?? loadedAssembly.GetName().FullName;
                                        // 如果程序集不在 AppDomain 中，添加到扫描列表
                                        if (!appDomainAssemblies.ContainsKey(assemblyKey))
                                        {
                                            assembliesToScan.Add(loadedAssembly);
                                            _context.Logger?.LogInfo($"添加插件程序集到扫描列表: {loadedAssembly.GetName().Name}");
                                        }
                                    }
                                }
                                
                                _context.Logger?.LogInfo($"准备扫描 {assembliesToScan.Count} 个程序集（AppDomain: {appDomainAssemblies.Count}, 插件: {loadedAssemblies?.Count ?? 0}）");
                                
                                var totalProviderCount = 0;
                                foreach (var assembly in assembliesToScan)
                                {
                                    try
                                    {
                                        var count = configProviderDiscovery.DiscoverAndRegisterProviders(assembly);
                                        if (count > 0)
                                        {
                                            _context.Logger?.LogInfo($"从程序集 {assembly.GetName().Name} 注册了 {count} 个 ConfigProvider");
                                        }
                                        totalProviderCount += count;
                                    }
                                    catch (Exception ex)
                                    {
                                        _context.Logger?.LogWarning($"扫描程序集 {assembly.FullName ?? assembly.GetName().FullName} 时出错: {ex.Message}");
                                    }
                                }
                                
                                _context.Logger?.LogInfo($"ConfigProvider 扫描完成，共注册了 {totalProviderCount} 个 Provider");
                                UpdateSplashScreen(80, $"已注册 {totalProviderCount} 个配置提供者", null);
                            }
                        }
                        catch (Exception ex)
                        {
                            _context.Logger?.LogWarning($"扫描 ConfigProvider 失败: {ex.Message}，不影响启动流程");
                        }
                        
                        await Task.Delay(50, cancellationToken);
                        
                        // ⭐ 阶段3：初始化并启用所有插件（此时 ConfigProvider 已全部注册）
                        UpdateSplashScreen(80, "正在初始化插件...", null);
                        
                        var loadedCount = 0;
                        
                        if (sortedCount > 0)
                        {
                            // 逐个初始化并启用插件
                            for (int i = 0; i < sortedDescriptors.Count; i++)
                            {
                                // ⭐ 在循环中检查取消
                                cancellationToken.ThrowIfCancellationRequested();
                                
                                var descriptor = sortedDescriptors[i];
                                var currentIndex = i + 1;
                                
                                // 计算初始化进度：80% ~ 95%
                                var initProgress = 80 + (currentIndex * 15.0 / sortedCount);
                                UpdateSplashScreen(initProgress, $"正在初始化插件 ({currentIndex}/{sortedCount})...", $"插件: {descriptor.Name ?? descriptor.Id}");
                                await Task.Delay(30, cancellationToken); // 短暂延迟，确保 UI 更新可见
                                
                                try
                                {
                                    // 尝试加载并初始化插件（使用路径）
                                    if (!string.IsNullOrEmpty(descriptor.AssemblyPath) && File.Exists(descriptor.AssemblyPath))
                                    {
                                        // ⭐ 此时程序集已经加载，LoadPluginAsync 会检测到并复用
                                        // 只执行创建实例、初始化、启用等操作
                                        await pluginHost.LoadPluginAsync(descriptor.AssemblyPath);
                                        loadedCount++;
                                        
                                        _context.Logger?.LogInfo($"成功初始化插件 ({currentIndex}/{sortedCount}): {descriptor.Name ?? descriptor.Id}");
                                    }
                                    else
                                    {
                                        _context.Logger?.LogWarning($"插件程序集路径无效或不存在: {descriptor.AssemblyPath}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _context.Logger?.LogError($"初始化插件失败 ({currentIndex}/{sortedCount}): {descriptor.Name ?? descriptor.Id}, 错误: {ex.Message}", ex);
                                    // 继续初始化下一个插件（即使某个插件失败，也要尝试初始化其他插件）
                                }
                            }
                            
                            // 获取最终加载的插件列表（可能与尝试加载的数量不同，因为加载可能失败）
                            loadedCount = pluginHost.LoadedPlugins.Count;
                        }
                        
                        _context.Logger?.LogInfo($"插件初始化完成，成功初始化: {loadedCount}/{sortedCount}");
                        
                        UpdateSplashScreen(95, $"插件初始化完成，已初始化 {loadedCount}/{sortedCount} 个插件", null);
                        await Task.Delay(50, cancellationToken);
                    }
                }

                // 获取已加载的插件列表
                var loadedPlugins = pluginHost.LoadedPlugins;
                // ⭐ 使用插件宿主中实际加载的插件数量（避免变量名冲突）
                var finalLoadedCount = loadedPlugins.Count;

                // 保存加载结果到 BootstrapContext（供其他代码使用）
                _context.SetData("PluginDirectory", pluginDirectory);
                _context.SetData("LoadedPlugins", finalLoadedCount);
                _context.SetData("PluginList", loadedPlugins.Select(p => new
                {
                    Id = p.Id,
                    Name = p.Name,
                    Version = p.Version
                }).ToList());

                if (finalLoadedCount > 0)
                {
                    var pluginNames = string.Join(", ", loadedPlugins.Select(p => p.Name));
                    _context.Logger?.LogInfo($"成功加载 {finalLoadedCount} 个插件: {pluginNames}");
                    // 进度已在上面更新到 90%
                }
                else
                {
                    _context.Logger?.LogInfo("未发现插件或未成功加载插件");
                    // 进度已在上面更新到 90%
                }
            }
            catch (Exception ex)
            {
                _context.Logger?.LogError($"加载插件时发生错误：{ex.Message}", ex);
                UpdateSplashScreen(90, $"插件加载失败: {ex.Message}", null);
                // 非关键错误，不影响启动流程
            }
        }
    }
}

