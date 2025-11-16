using System;
using System.Collections.Generic;
using NavStack.Models;

namespace NavStack.Services
{
	/// <summary>
	/// 导航树管理服务（节点/树操作）
	/// </summary>
	public interface INavigationTreeService
	{
		/// <summary>
		/// 添加一个导航项到树中（会根据 ParentId 自动挂载）
		/// </summary>
		/// <param name="item">导航项</param>
		void AddItem(NavigationItem item);

		/// <summary>
		/// 根据标识删除导航项（包含其所有子孙）
		/// </summary>
		/// <param name="id">导航项标识</param>
		void RemoveItem(string id);

		/// <summary>
		/// 获取指定标识的导航项
		/// </summary>
		/// <param name="id">导航项标识</param>
		/// <returns>导航项或 null</returns>
		NavigationItem? GetItem(string id);

		/// <summary>
		/// 更新一个导航项（根据相同 Id 覆盖）
		/// </summary>
		/// <param name="item">导航项</param>
		void UpdateItem(NavigationItem item);

		/// <summary>
		/// 调整指定项的排序序号
		/// </summary>
		/// <param name="itemId">导航项标识</param>
		/// <param name="newOrder">新的排序序号</param>
		void ReorderItem(string itemId, int newOrder);

		/// <summary>
		/// 将指定项移动到新的父节点并设置排序序号
		/// </summary>
		/// <param name="itemId">导航项标识</param>
		/// <param name="newParentId">新父节点标识（null 表示根）</param>
		/// <param name="order">新的排序序号</param>
		void MoveItem(string itemId, string? newParentId, int order);

		/// <summary>
		/// 获取指定父节点的直接子项集合
		/// </summary>
		/// <param name="parentId">父节点标识（null 表示根层）</param>
		/// <returns>子项集合</returns>
		IEnumerable<NavigationItem> GetChildren(string? parentId);

		/// <summary>
		/// 获取指定父节点的所有后代（深度优先）
		/// </summary>
		/// <param name="parentId">父节点标识（null 表示根层）</param>
		/// <returns>后代集合</returns>
		IEnumerable<NavigationItem> GetAllDescendants(string? parentId);

		/// <summary>
		/// 获取指定项的所有祖先（从父到根）
		/// </summary>
		/// <param name="itemId">导航项标识</param>
		/// <returns>祖先集合</returns>
		IEnumerable<NavigationItem> GetAncestors(string itemId);

		/// <summary>
		/// 获取面包屑路径（从根到指定项）
		/// </summary>
		/// <param name="itemId">导航项标识</param>
		/// <returns>breadcrumb 集合</returns>
		IEnumerable<NavigationItem> GetBreadcrumb(string itemId);

		/// <summary>
		/// 获取树中所有项
		/// </summary>
		/// <returns>全部项</returns>
		IEnumerable<NavigationItem> GetAllItems();

		/// <summary>
		/// 获取可见的所有项（IsVisible == true）
		/// </summary>
		/// <returns>可见项</returns>
		IEnumerable<NavigationItem> GetVisibleItems();

		/// <summary>
		/// 获取虚拟根节点（不参与权限/导航，仅用于树的组织）
		/// </summary>
		/// <returns>虚拟根</returns>
		NavigationItem GetRoot(); // 返回虚拟根
	}
}


