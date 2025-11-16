using System.Collections.ObjectModel;
using NavStack.Models;

namespace NavStack.ViewModels
{
	/// <summary>
	/// 导航项视图模型（不直接依赖 Prism，提供最小属性集）
	/// </summary>
	public class NavigationItemViewModel
	{
		/// <summary>
		/// 使用模型初始化视图模型
		/// </summary>
		/// <param name="model">导航项模型</param>
		public NavigationItemViewModel(NavigationItem model)
		{
			Model = model;
			Children = new ObservableCollection<NavigationItemViewModel>();
		}

		/// <summary>
		/// 关联的导航项模型
		/// </summary>
		public NavigationItem Model { get; }

		/// <summary>
		/// 导航项标识
		/// </summary>
		public string Id => Model.Id;
		/// <summary>
		/// 显示名称
		/// </summary>
		public string Name => Model.Name;
		/// <summary>
		/// 图标资源（路径或 Geometry）
		/// </summary>
		public string? Icon => Model.Icon;
		/// <summary>
		/// 徽章数量
		/// </summary>
		public int BadgeCount => Model.BadgeCount;

		/// <summary>
		/// 是否展开
		/// </summary>
		public bool IsExpanded { get; set; }
		/// <summary>
		/// 是否为当前激活项
		/// </summary>
		public bool IsActive { get; set; }
		/// <summary>
		/// 是否可见
		/// </summary>
		public bool IsVisible { get; set; } = true;
		/// <summary>
		/// 是否处于拖拽中
		/// </summary>
		public bool IsDragging { get; set; }
		/// <summary>
		/// 是否允许作为放置目标
		/// </summary>
		public bool CanDrop { get; set; }
		/// <summary>
		/// 是否为管理模式（决定拖拽显示等）
		/// </summary>
		public bool IsManagementMode { get; set; }

		/// <summary>
		/// 是否允许拖拽（受管理模式和配置控制）
		/// </summary>
		public bool AllowDrag => IsManagementMode && (Model.DragDrop?.AllowReorder == true);

		/// <summary>
		/// 子项集合
		/// </summary>
		public ObservableCollection<NavigationItemViewModel> Children { get; }
	}
}


