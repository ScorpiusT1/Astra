using System.Threading.Tasks;
using NavStack.Core;

namespace NavStack.Services
{
	/// <summary>
	/// 导航管理器：负责按导航项 Id 进行区域导航与历史管理
	/// </summary>
	public interface INavigationManager
	{
		/// <summary>
		/// 通过导航项标识执行导航（内部解析到区域与页面键）
		/// </summary>
		/// <param name="itemId">导航项标识</param>
		/// <param name="parameters">导航参数（可选）</param>
		/// <returns>导航结果</returns>
		Task<NavigationResult> NavigateAsync(string itemId, NavigationParameters parameters = null);

		/// <summary>
		/// 判断是否可以导航到指定导航项
		/// </summary>
		/// <param name="itemId">导航项标识</param>
		/// <returns>是否可导航</returns>
		Task<bool> CanNavigateAsync(string itemId);

		/// <summary>
		/// 为指定区域注册导航项的视图
		/// </summary>
		/// <param name="regionName">区域名称</param>
		/// <param name="itemId">导航项标识（作为页面键）</param>
		/// <param name="viewType">视图类型</param>
		void RegisterForRegion(string regionName, string itemId, System.Type viewType);

		/// <summary>
		/// 直接向区域发起导航请求（以页面键）
		/// </summary>
		/// <param name="regionName">区域名称</param>
		/// <param name="pageKey">页面键</param>
		/// <param name="parameters">导航参数</param>
		void RequestNavigate(string regionName, string pageKey, NavigationParameters parameters = null);

		/// <summary>
		/// 是否可以后退
		/// </summary>
		bool CanGoBack { get; }

		/// <summary>
		/// 是否可以前进
		/// </summary>
		bool CanGoForward { get; }

		/// <summary>
		/// 后退
		/// </summary>
		void GoBack();

		/// <summary>
		/// 前进
		/// </summary>
		void GoForward();
	}
}


