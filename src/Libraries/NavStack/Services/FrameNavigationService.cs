using NavStack.Configuration;
using NavStack.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace NavStack.Services
{
    /// <summary>
    /// Frame导航服务实现
    /// </summary>
    public class FrameNavigationService : IFrameNavigationService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly INavigationConfiguration _configuration;
        private readonly Stack<NavigationHistoryEntry> _backStack = new();
        private readonly Stack<NavigationHistoryEntry> _forwardStack = new();
        private NavigationHistoryEntry _currentEntry;

        private Frame _frame;
        public Frame Frame 
        { 
            get => _frame;
            set 
            {
                _frame = value;
                System.Diagnostics.Debug.WriteLine($"[FrameNavigationService] Frame已设置: {value != null}");
            }
        }

        public event EventHandler<NavigationEventArgs> Navigating;
        public event EventHandler<NavigationEventArgs> Navigated;
        public event EventHandler<NavigationFailedEventArgs> NavigationFailed;

        public bool CanGoBack => _backStack.Count > 0;
        public bool CanGoForward => _forwardStack.Count > 0;

        public FrameNavigationService(IServiceProvider serviceProvider, INavigationConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            //Frame = new Frame();
        }

        public async Task<NavigationResult> NavigateAsync(string pageKey, NavigationParameters parameters = null)
        {
            try
            {
                var context = new NavigationContext
                {
                    NavigationUri = pageKey,
                    Parameters = parameters ?? new NavigationParameters(),
                    NavigationMode = NavigationMode.New,
                    NavigationSource = Frame
                };

                // 触发Navigating事件
                var navigatingArgs = new NavigationEventArgs { Context = context };
                Navigating?.Invoke(this, navigatingArgs);
                if (navigatingArgs.Cancel)
                {
                    return NavigationResult.Failed("Navigation cancelled by event handler");
                }

                // 确认当前页面可以导航
                if (_currentEntry != null)
                {
                    var canNavigate = await ConfirmNavigationAsync(_currentEntry.Content, context);
                    if (!canNavigate)
                    {
                        return NavigationResult.Failed("Navigation cancelled by current page");
                    }

                    // 通知当前页面导航离开
                    await NotifyNavigatingFromAsync(_currentEntry.Content, context);
                }

                // 创建新页面
                var registration = _configuration.GetPageRegistration(pageKey);
                if (registration == null)
                {
                    throw new InvalidOperationException($"Page '{pageKey}' not registered");
                }

                var page = CreatePage(registration);

                // 通知新页面即将导航到
                var canNavigateTo = await NotifyNavigatingToAsync(page, context);

                if (!canNavigateTo)
                {
                    return NavigationResult.Failed("Navigation cancelled by target page");
                }

                // 执行导航
                if (Frame == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[FrameNavigationService] 错误：Frame为null，无法导航到 {pageKey}");
                    throw new InvalidOperationException($"Frame is not set. Please ensure Frame is properly initialized before navigation to '{pageKey}'.");
                }
                
                System.Diagnostics.Debug.WriteLine($"[FrameNavigationService] 导航到 {pageKey}，Frame: {Frame != null}");
                Frame.Content = page;

                // 更新导航历史
                if (_currentEntry != null)
                {
                    _backStack.Push(_currentEntry);
                }
                _currentEntry = new NavigationHistoryEntry
                {
                    PageKey = pageKey,
                    Parameters = parameters,
                    Content = page
                };
                _forwardStack.Clear();

                // 通知页面导航完成
                if (_backStack.Count > 0)
                {
                    await NotifyNavigatedFromAsync(_backStack.Peek().Content, context);
                }
                await NotifyNavigatedToAsync(page, context);

                // 触发Navigated事件
                Navigated?.Invoke(this, new NavigationEventArgs { Context = context, Content = page });

                return NavigationResult.Succeeded();
            }
            catch (Exception ex)
            {
                var context = new NavigationContext
                {
                    NavigationUri = pageKey,
                    Parameters = parameters,
                    NavigationMode = NavigationMode.New
                };
                NavigationFailed?.Invoke(this, new NavigationFailedEventArgs { Context = context, Exception = ex });
                return NavigationResult.Failed($"Navigation failed: {ex.Message}", ex);
            }
        }

        public async Task<NavigationResult> GoBackAsync(NavigationParameters parameters = null)
        {
            if (!CanGoBack)
            {
                return NavigationResult.Failed("Cannot go back");
            }

            try
            {
                var entry = _backStack.Pop();
                var context = new NavigationContext
                {
                    NavigationUri = entry.PageKey,
                    Parameters = parameters ?? entry.Parameters,
                    NavigationMode = NavigationMode.Back,
                    NavigationSource = Frame
                };

                // 确认导航
                if (_currentEntry != null)
                {
                    var canNavigate = await ConfirmNavigationAsync(_currentEntry.Content, context);
                    if (!canNavigate)
                    {
                        _backStack.Push(entry); // 恢复堆栈
                        return NavigationResult.Failed("Navigation cancelled");
                    }
                }

                // 执行导航
                if (Frame == null)
                {
                    throw new InvalidOperationException("Frame is not set. Please ensure Frame is properly initialized before navigation.");
                }
                Frame.Content = entry.Content;
                _forwardStack.Push(_currentEntry);
                _currentEntry = entry;

                // 通知生命周期
                await NotifyNavigatedToAsync(entry.Content, context);

                return NavigationResult.Succeeded();
            }
            catch (Exception ex)
            {
                return NavigationResult.Failed($"Go back failed: {ex.Message}", ex);
            }
        }

        public async Task<NavigationResult> GoForwardAsync(NavigationParameters parameters = null)
        {
            if (!CanGoForward)
            {
                return NavigationResult.Failed("Cannot go forward");
            }

            try
            {
                var entry = _forwardStack.Pop();
                var context = new NavigationContext
                {
                    NavigationUri = entry.PageKey,
                    Parameters = parameters ?? entry.Parameters,
                    NavigationMode = NavigationMode.Forward,
                    NavigationSource = Frame
                };

                if (Frame == null)
                {
                    throw new InvalidOperationException("Frame is not set. Please ensure Frame is properly initialized before navigation.");
                }
                Frame.Content = entry.Content;
                _backStack.Push(_currentEntry);
                _currentEntry = entry;

                await NotifyNavigatedToAsync(entry.Content, context);

                return NavigationResult.Succeeded();
            }
            catch (Exception ex)
            {
                return NavigationResult.Failed($"Go forward failed: {ex.Message}", ex);
            }
        }

        public void ClearHistory()
        {
            _backStack.Clear();
            _forwardStack.Clear();
        }

        public IEnumerable<string> GetNavigationHistory()
        {
            var history = new List<string>();
            history.AddRange(_backStack.Select(e => e.PageKey).Reverse());
            if (_currentEntry != null)
            {
                history.Add(_currentEntry.PageKey);
            }
            return history;
        }

        private object CreatePage(PageRegistration registration)
        {
            System.Diagnostics.Debug.WriteLine($"[FrameNavigationService] 创建页面: {registration.ViewType.Name}");
            
            var page = _serviceProvider.GetService(registration.ViewType)
                       ?? Activator.CreateInstance(registration.ViewType);

            if (registration.ViewModelType != null)
            {
                System.Diagnostics.Debug.WriteLine($"[FrameNavigationService] 创建ViewModel: {registration.ViewModelType.Name}");
                
                var viewModel = _serviceProvider.GetService(registration.ViewModelType);
                if (viewModel == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[FrameNavigationService] 服务提供者中未找到ViewModel，使用Activator创建");
                    try
                    {
                        viewModel = Activator.CreateInstance(registration.ViewModelType);
                        System.Diagnostics.Debug.WriteLine($"[FrameNavigationService] ViewModel创建成功");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[FrameNavigationService] ViewModel创建失败: {ex.Message}");
                        throw;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[FrameNavigationService] 从服务提供者获取ViewModel成功");
                }

                if (page is FrameworkElement element)
                {
                    element.DataContext = viewModel;
                    System.Diagnostics.Debug.WriteLine($"[FrameNavigationService] 设置DataContext成功");
                }
            }

            return page;
        }

        private async Task<bool> ConfirmNavigationAsync(object page, NavigationContext context)
        {
            var confirmNavigation = GetNavigationAware(page) as IConfirmNavigation;
            if (confirmNavigation != null)
            {
                return await confirmNavigation.CanNavigateAsync(context);
            }
            return true;
        }

        private async Task<bool> NotifyNavigatingToAsync(object page, NavigationContext context)
        {
            var navigationAware = GetNavigationAware(page);
            if (navigationAware != null)
            {
                return await navigationAware.OnNavigatingToAsync(context);
            }
            return true;
        }

        private async Task NotifyNavigatedToAsync(object page, NavigationContext context)
        {
            var navigationAware = GetNavigationAware(page);
            if (navigationAware != null)
            {
                await navigationAware.OnNavigatedToAsync(context);
            }
        }

        private async Task NotifyNavigatingFromAsync(object page, NavigationContext context)
        {
            var navigationAware = GetNavigationAware(page);
            if (navigationAware != null)
            {
                await navigationAware.OnNavigatingFromAsync(context);
            }
        }

        private async Task NotifyNavigatedFromAsync(object page, NavigationContext context)
        {
            var navigationAware = GetNavigationAware(page);
            if (navigationAware != null)
            {
                await navigationAware.OnNavigatedFromAsync(context);
            }
        }

        private INavigationAware GetNavigationAware(object page)
        {
            if (page is INavigationAware navigationAware)
            {
                return navigationAware;
            }

            if (page is FrameworkElement element && element.DataContext is INavigationAware viewModelAware)
            {
                return viewModelAware;
            }

            return null;
        }

        private class NavigationHistoryEntry
        {
            public string PageKey { get; set; }
            public NavigationParameters Parameters { get; set; }
            public object Content { get; set; }
        }
    }
}
