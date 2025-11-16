using System.Collections.ObjectModel;
using Astra.Core.Access.Models;
using NavStack.Modularity;
using NavStack.Services;

namespace Astra.Services.Navigation
{
	public interface INavigationGuard
	{
		bool CanNavigate(User user, NavigationMenuItem target);
		void ApplyVisibility(User user, ObservableCollection<NavigationMenuItem> menuItems);
		// 计算安全回退页（优先：默认页可访问 -> 第一个可见且可访问 -> 第一个可见）
		string GetSafeFallbackPage(User user, ObservableCollection<NavigationMenuItem> menuItems, string defaultPageKey);
	}
}


