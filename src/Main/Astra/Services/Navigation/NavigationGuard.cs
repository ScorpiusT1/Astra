using System.Collections.ObjectModel;
using Astra.Core.Access.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NavStack.Modularity;
using NavStack.Services;

namespace Astra.Services.Navigation
{
	public class NavigationGuard : INavigationGuard
	{
		private readonly INavigationPermissionService _permissionService;
		private readonly ILogger<NavigationGuard> _logger;

		public NavigationGuard(INavigationPermissionService permissionService, ILogger<NavigationGuard> logger = null)
		{
			_permissionService = permissionService;
			_logger = logger ?? NullLogger<NavigationGuard>.Instance;
		}

		public bool CanNavigate(User user, NavigationMenuItem target)
		{
			var result = _permissionService.HasPermission(user, target);
			if (!result)
			{
				_logger.LogWarning("[NavigationGuard] 权限不足: 用户={User}, 目标={Target}", user?.Username ?? "未登录", target?.NavigationKey);
			}
			return result;
		}

		public void ApplyVisibility(User user, ObservableCollection<NavigationMenuItem> menuItems)
		{
			_permissionService.UpdateMenuItemsVisibility(user, menuItems);
		}

		public string GetSafeFallbackPage(User user, ObservableCollection<NavigationMenuItem> menuItems, string defaultPageKey)
		{
			if (!string.IsNullOrWhiteSpace(defaultPageKey))
			{
				var def = TryFind(menuItems, defaultPageKey);
				if (def != null && def.IsVisible && CanNavigate(user, def))
				{
					return defaultPageKey;
				}
			}

			foreach (var item in menuItems)
			{
				if (item.IsVisible && CanNavigate(user, item))
				{
					return item.NavigationKey;
				}
			}

			// 兜底：返回第一个可见项；若无可见项则返回默认页（可能仍不可访问，但至少不为空）
			var firstVisible = menuItems.FirstOrDefault(m => m.IsVisible);
			return firstVisible?.NavigationKey ?? defaultPageKey;
		}

		private static NavigationMenuItem TryFind(ObservableCollection<NavigationMenuItem> menuItems, string key)
		{
			foreach (var i in menuItems)
			{
				if (i.NavigationKey == key) return i;
			}
			return null;
		}
	}
}


