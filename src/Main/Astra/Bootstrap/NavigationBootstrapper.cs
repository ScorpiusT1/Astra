using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Astra.Utilities;
using Astra.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NavStack.Regions;
using NavStack.Services;
using Astra.Services.Navigation;

namespace Astra.Bootstrap
{
	public static class NavigationBootstrapper
	{
		public static async Task InitializeAsync(Window mainWindow, IServiceProvider serviceProvider, Dispatcher dispatcher, ILogger? logger = null)
		{
			var log = logger ?? (serviceProvider?.GetService<ILoggerFactory>()?.CreateLogger("NavigationBootstrapper") ?? NullLogger.Instance);
			if (mainWindow is not Astra.Views.MainView mainView || serviceProvider == null)
			{
				return;
			}

			try
			{
				// 读取默认页配置（可选）
				string defaultPageKey = NavigationKeys.Home;
				try
				{
					var appNavOptions = serviceProvider.GetService<IOptions<Astra.Configuration.AppNavOptions>>();
					if (!string.IsNullOrWhiteSpace(appNavOptions?.Value?.DefaultPage))
					{
						defaultPageKey = appNavOptions.Value.DefaultPage;
					}
				}
				catch { }

				var regionManager = serviceProvider.GetService<IRegionManager>();
				regionManager?.RegisterRegion(RegionNames.MainRegion, mainView.MainFrame);

				try
				{
					var navManager = serviceProvider.GetService<INavigationManager>();
					var vm = mainView.DataContext as MainViewModel;
					vm?.EnsureRegionSubscriptions();
					if (navManager != null)
					{
						var registry = serviceProvider.GetService<INavigationRegistryProvider>() ?? new DefaultNavigationRegistryProvider();
						registry.Register(RegionNames.MainRegion, navManager);

						try
						{
							await navManager.NavigateAsync(defaultPageKey);
							await dispatcher.InvokeAsync(() =>
							{
								var vm2 = mainView.DataContext as MainViewModel;
								vm2?.SetSelectedByKey(defaultPageKey);
							}, DispatcherPriority.Loaded);
						}
						catch (Exception navEx)
						{
							log.LogError(navEx, "[NavigationBootstrapper] 默认导航失败");
						}
					}
				}
				catch (Exception ex2)
				{
					log.LogError(ex2, "[NavigationBootstrapper] 注册视图失败");
				}
			}
			catch (Exception ex)
			{
				log.LogError(ex, "[NavigationBootstrapper] 注册区域失败");
			}
		}
	}
}


