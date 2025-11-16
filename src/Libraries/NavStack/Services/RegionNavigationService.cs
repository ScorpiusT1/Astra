using NavStack.Configuration;
using NavStack.Core;
using System.Windows;
using System.Windows.Controls;

namespace NavStack.Services
{
    /// <summary>
    /// 区域导航服务实现
    /// </summary>
    public class RegionNavigationService : IRegionNavigationService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly INavigationConfiguration _configuration;
        private readonly ContentControl _regionControl;
        private readonly Stack<NavigationHistoryEntry> _history = new();

        public string RegionName { get; }

        public event EventHandler<NavigationEventArgs> Navigating;
        public event EventHandler<NavigationEventArgs> Navigated;
        public event EventHandler<NavigationFailedEventArgs> NavigationFailed;

        public bool CanGoBack => _history.Count > 1;
        public bool CanGoForward => false; // 区域导航通常不支持前进

        public RegionNavigationService(
            string regionName,
            ContentControl regionControl,
            IServiceProvider serviceProvider,
            INavigationConfiguration configuration)
        {
            RegionName = regionName;
            _regionControl = regionControl;
            _serviceProvider = serviceProvider;
            _configuration = configuration;
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
                    NavigationSource = _regionControl
                };

                var registration = _configuration.GetPageRegistration(pageKey);
                if (registration == null)
                {
                    throw new InvalidOperationException($"Page '{pageKey}' not registered");
                }

                var page = CreatePage(registration);

                _regionControl.Content = page;
                _history.Push(new NavigationHistoryEntry
                {
                    PageKey = pageKey,
                    Parameters = parameters,
                    Content = page
                });

                Navigated?.Invoke(this, new NavigationEventArgs { Context = context, Content = page });

                return NavigationResult.Succeeded();
            }
            catch (Exception ex)
            {
                return NavigationResult.Failed($"Region navigation failed: {ex.Message}", ex);
            }
        }

        public async Task<NavigationResult> GoBackAsync(NavigationParameters parameters = null)
        {
            if (!CanGoBack)
            {
                return NavigationResult.Failed("Cannot go back");
            }

            _history.Pop(); // 移除当前
            var entry = _history.Peek();
            _regionControl.Content = entry.Content;

            return NavigationResult.Succeeded();
        }

        public Task<NavigationResult> GoForwardAsync(NavigationParameters parameters = null)
        {
            return Task.FromResult(NavigationResult.Failed("Region navigation does not support forward"));
        }

        public void ClearHistory()
        {
            _history.Clear();
        }

        public IEnumerable<string> GetNavigationHistory()
        {
            return _history.Select(e => e.PageKey).Reverse();
        }

        private object CreatePage(PageRegistration registration)
        {
            var page = _serviceProvider.GetService(registration.ViewType)
                       ?? Activator.CreateInstance(registration.ViewType);

            if (registration.ViewModelType != null)
            {
                var viewModel = _serviceProvider.GetService(registration.ViewModelType)
                                ?? Activator.CreateInstance(registration.ViewModelType);

                if (page is FrameworkElement element)
                {
                    element.DataContext = viewModel;
                }
            }

            return page;
        }

        private class NavigationHistoryEntry
        {
            public string PageKey { get; set; }
            public NavigationParameters Parameters { get; set; }
            public object Content { get; set; }
        }
    }
}
