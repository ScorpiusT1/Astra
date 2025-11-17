using Astra.Bootstrap.Core;
using Astra.Bootstrap.Tasks;
using Astra.Bootstrap.UI;
using Astra.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Astra.Bootstrap.Services
{
    /// <summary>
    /// 应用程序启动管理器
    /// </summary>
    public class ApplicationBootstrapper
    {
        private readonly List<IBootstrapTask> _tasks = new List<IBootstrapTask>();
        private readonly BootstrapContext _context = new BootstrapContext();
        private SplashScreenView _splashScreen;
        private CancellationTokenSource _cancellationTokenSource;

        public ApplicationBootstrapper()
        {
            _cancellationTokenSource = new CancellationTokenSource();
        }

        #region Configuration

        public ApplicationBootstrapper AddTask(IBootstrapTask task)
        {
            _tasks.Add(task);
            return this;
        }

        public ApplicationBootstrapper AddTask<T>() where T : IBootstrapTask, new()
        {
            return AddTask(new T());
        }

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

            try
            {
                _context.Logger?.LogInfo("=== 应用程序启动开始 ===");

                // 1. 显示启动画面
                ShowSplashScreen();

                // 2. 按优先级排序任务
                var sortedTasks = _tasks.OrderBy(t => t.Priority).ToList();

                // 3. 计算总权重
                var totalWeight = sortedTasks.Sum(t => t.Weight);
                var completedWeight = 0.0;

                // 4. 执行所有任务
                foreach (var task in sortedTasks)
                {
                    try
                    {
                        _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                        await ExecuteTaskAsync(task, completedWeight, totalWeight);

                        completedWeight += task.Weight;
                        result.AddCompletedTask(task);
                    }
                    catch (OperationCanceledException)
                    {
                        _context.Logger?.LogWarning($"启动已取消");
                        result.IsCancelled = true;
                        throw;
                    }
                    catch (BootstrapException ex)
                    {
                        _context.Logger?.LogError($"任务 '{task.Name}' 失败", ex);
                        result.AddFailedTask(task, ex);

                        if (task.IsCritical)
                        {
                            _context.Logger?.LogError($"关键任务失败，中止启动");
                            result.IsSuccess = false;
                            throw;
                        }
                        else
                        {
                            _context.Logger?.LogWarning($"非关键任务失败，继续启动");
                        }
                    }
                }

                // ⭐ 5. 构建服务提供者（在任务执行完成后）
                if (_context.Services != null)
                {
                    UpdateSplashScreen(95, "构建服务容器...", null);
                    
                    // 在构建服务提供者之前，将 BootstrapContext 注册到服务集合中
                    // 这样其他组件可以通过 DI 容器访问 BootstrapContext 和其中的数据（如 PluginHost）
                    _context.Services.AddSingleton(_context);
                    
                    _context.ServiceProvider = _context.Services.BuildServiceProvider();
                }

                stopwatch.Stop();
                result.IsSuccess = true;
                result.TotalTime = stopwatch.Elapsed;

                _context.Logger?.LogInfo($"=== 应用程序启动完成，耗时：{stopwatch.ElapsedMilliseconds}ms ===");

                // 6. 显示完成状态
                UpdateSplashScreen(100, "启动完成", null);
                await Task.Delay(500);
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
                await Task.Delay(3000); // 让用户看到错误

                await RollbackAsync(result.CompletedTasks.ToList());
            }
            finally
            {
                CloseSplashScreen();
            }

            return result;
        }

        private async Task ExecuteTaskAsync(IBootstrapTask task, double completedWeight, double totalWeight)
        {
            var progress = new Progress<BootstrapProgress>(p =>
            {
                var taskProgress = (p.Percentage / 100.0) * task.Weight;
                var globalProgress = ((completedWeight + taskProgress) / totalWeight) * 95; // 留 5% 给后续步骤
                UpdateSplashScreen(globalProgress, p.Message, p.Details);
            });

            await task.ExecuteAsync(_context, progress, _cancellationTokenSource.Token);
        }

        private async Task RollbackAsync(List<IBootstrapTask> completedTasks)
        {
            _context.Logger?.LogWarning("开始回滚操作");

            foreach (var task in completedTasks.AsEnumerable().Reverse())
            {
                try
                {
                    _context.Logger?.LogInfo($"回滚任务：{task.Name}");
                    await task.RollbackAsync(_context);
                }
                catch (Exception ex)
                {
                    _context.Logger?.LogError($"回滚任务 '{task.Name}' 失败", ex);
                }
            }

            _context.Logger?.LogWarning("回滚操作完成");
        }

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
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _splashScreen?.UpdateProgress(progress, message, details);
            });
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
    }
}

