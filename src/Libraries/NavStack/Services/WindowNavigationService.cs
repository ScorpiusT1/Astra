using NavStack.Configuration;
using NavStack.Core;
using System.Windows;

namespace NavStack.Services
{
    /// <summary>
    /// 窗口导航服务实现
    /// </summary>
    public class WindowNavigationService : IWindowNavigationService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly INavigationConfiguration _configuration;
        private readonly Dictionary<string, Window> _openWindows = new();

        public WindowNavigationService(IServiceProvider serviceProvider, INavigationConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
        }

        public async Task<bool> ShowDialogAsync(string windowKey, NavigationParameters parameters = null)
        {
            try
            {
                var window = CreateWindow(windowKey, parameters);
                var result = window.ShowDialog();
                return result ?? false;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to show dialog '{windowKey}'", ex);
            }
        }

        public async Task ShowWindowAsync(string windowKey, NavigationParameters parameters = null)
        {
            try
            {
                if (_openWindows.ContainsKey(windowKey))
                {
                    _openWindows[windowKey].Activate();
                    return;
                }

                var window = CreateWindow(windowKey, parameters);
                _openWindows[windowKey] = window;

                window.Closed += (s, e) => _openWindows.Remove(windowKey);
                window.Show();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to show window '{windowKey}'", ex);
            }
        }

        public void CloseWindow(string windowKey)
        {
            if (_openWindows.TryGetValue(windowKey, out var window))
            {
                window.Close();
                _openWindows.Remove(windowKey);
            }
        }

        private Window CreateWindow(string windowKey, NavigationParameters parameters)
        {
            var registration = _configuration.GetPageRegistration(windowKey);
            if (registration == null)
            {
                throw new InvalidOperationException($"Window '{windowKey}' not registered");
            }

            var window = _serviceProvider.GetService(registration.ViewType) as Window
                         ?? Activator.CreateInstance(registration.ViewType) as Window;

            if (window == null)
            {
                throw new InvalidOperationException($"'{windowKey}' is not a Window type");
            }

            if (registration.ViewModelType != null)
            {
                var viewModel = _serviceProvider.GetService(registration.ViewModelType)
                                ?? Activator.CreateInstance(registration.ViewModelType);
                window.DataContext = viewModel;

                // 传递参数到ViewModel
                if (viewModel is INavigationAware navigationAware && parameters != null)
                {
                    var context = new NavigationContext
                    {
                        NavigationUri = windowKey,
                        Parameters = parameters,
                        NavigationMode = NavigationMode.New
                    };
                    _ = navigationAware.OnNavigatedToAsync(context);
                }
            }

            return window;
        }
    }
}
