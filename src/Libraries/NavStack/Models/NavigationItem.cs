using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using NavStack.Core;

namespace NavStack.Models
{
	/// <summary>
	/// 导航项接口（最小契约）
	/// </summary>
	public interface INavigationItem
	{
		/// <summary>
		/// 唯一标识
		/// </summary>
		string Id { get; }

		/// <summary>
		/// 显示名称
		/// </summary>
		string Name { get; }

		/// <summary>
		/// 图标（路径/Geometry）
		/// </summary>
		string? Icon { get; }

		/// <summary>
		/// 父节点标识（根为 null）
		/// </summary>
		string? ParentId { get; }

		/// <summary>
		/// 排序序号
		/// </summary>
		int Order { get; }

		/// <summary>
		/// 是否启用
		/// </summary>
		bool IsEnabled { get; }

		/// <summary>
		/// 是否可见
		/// </summary>
		bool IsVisible { get; }

		/// <summary>
		/// 是否展开（用于树）
		/// </summary>
		bool IsExpanded { get; }

		/// <summary>
		/// 是否激活/选中
		/// </summary>
		bool IsActive { get; }

		/// <summary>
		/// 徽章数量
		/// </summary>
		int BadgeCount { get; }

		/// <summary>
		/// 关联视图类型
		/// </summary>
		Type? ViewType { get; }

		/// <summary>
		/// 关联区域名称
		/// </summary>
		string? RegionName { get; }

		/// <summary>
		/// 权限配置
		/// </summary>
		NavigationPermission? Permission { get; }

		/// <summary>
		/// 拖拽配置
		/// </summary>
		DragDropConfig? DragDrop { get; }

		/// <summary>
		/// 子节点集合（只读）
		/// </summary>
		IReadOnlyList<NavigationItem> Children { get; }

		/// <summary>
		/// 导航参数
		/// </summary>
		NavigationParameters? NavigationParameters { get; }
	}

	/// <summary>
	/// 导航项模型
	/// </summary>
	public class NavigationItem : INavigationItem
	{
		private readonly List<NavigationItem> _children = new();

		/// <inheritdoc/>
		public string Id { get; set; } = Guid.NewGuid().ToString("N");
		/// <inheritdoc/>
		public string Name { get; set; } = string.Empty;
		/// <inheritdoc/>
		public string? Icon { get; set; }
		/// <inheritdoc/>
		public string? ParentId { get; set; }
		/// <inheritdoc/>
		public int Order { get; set; }
		/// <inheritdoc/>
		public bool IsEnabled { get; set; } = true;
		/// <inheritdoc/>
		public bool IsVisible { get; set; } = true;
		/// <inheritdoc/>
		public bool IsExpanded { get; set; }
		/// <inheritdoc/>
		public bool IsActive { get; set; }
		/// <inheritdoc/>
		public int BadgeCount { get; set; }
		/// <inheritdoc/>
		public Type? ViewType { get; set; }
		/// <inheritdoc/>
		public string? RegionName { get; set; }
		/// <inheritdoc/>
		public NavigationPermission? Permission { get; set; }
		/// <inheritdoc/>
		public DragDropConfig? DragDrop { get; set; }
		/// <inheritdoc/>
		public NavigationParameters? NavigationParameters { get; set; }

		/// <inheritdoc/>
		public IReadOnlyList<NavigationItem> Children => _children;

		/// <summary>
		/// 添加一个子节点（会自动设置子节点的 ParentId 并保持有序）
		/// </summary>
		/// <param name="child">子节点</param>
		/// <returns>当前实例（便于链式调用）</returns>
		/// <exception cref="ArgumentNullException">child 为 null</exception>
		public NavigationItem AddChild(NavigationItem child)
		{
			if (child == null) throw new ArgumentNullException(nameof(child));
			child.ParentId = Id;
			_children.Add(child);
			_children.Sort(static (a, b) => a.Order.CompareTo(b.Order));
			return this;
		}

		/// <summary>
		/// 移除指定标识的直接子节点
		/// </summary>
		/// <param name="id">子节点标识</param>
		/// <returns>是否发生删除</returns>
		public bool RemoveChild(string id)
		{
			var index = _children.FindIndex(x => string.Equals(x.Id, id, StringComparison.Ordinal));
			if (index >= 0)
			{
				_children.RemoveAt(index);
				return true;
			}
			return false;
		}
	}
}


