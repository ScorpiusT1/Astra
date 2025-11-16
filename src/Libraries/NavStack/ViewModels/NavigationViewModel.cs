using System.Collections.ObjectModel;
using System.Threading.Tasks;
using NavStack.ViewModels;
using NavStack.Models;

namespace NavStack.ViewModels
{
	/// <summary>
	/// 导航容器视图模型（最小骨架）
	/// </summary>
	public class NavigationViewModel
	{
		/// <summary>
		/// 根级导航项集合
		/// </summary>
		public ObservableCollection<NavigationItemViewModel> Items { get; private set; } = new();

		/// <summary>
		/// 当前激活项
		/// </summary>
		public NavigationItemViewModel? CurrentItem { get; set; }

		/// <summary>
		/// 是否为管理模式（影响拖拽与管理操作）
		/// </summary>
		public bool IsManagementMode { get; set; }

		/// <summary>
		/// 加载导航（异步）
		/// </summary>
		/// <returns>异步任务</returns>
		public Task LoadNavigationAsync()
		{
			return Task.CompletedTask;
		}

		/// <summary>
		/// 刷新导航（异步）
		/// </summary>
		/// <returns>异步任务</returns>
		public Task RefreshAsync()
		{
			return Task.CompletedTask;
		}
	}
}


