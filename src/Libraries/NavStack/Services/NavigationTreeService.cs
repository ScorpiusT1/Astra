using System;
using System.Collections.Generic;
using System.Linq;
using NavStack.Models;

namespace NavStack.Services
{
	/// <summary>
	/// 内存导航树实现，提供高性能查找（字典索引）与层级操作
	/// </summary>
	public sealed class NavigationTreeService : INavigationTreeService
	{
		private readonly Dictionary<string, NavigationItem> _items = new(StringComparer.Ordinal);
		private readonly NavigationItem _root = new NavigationItem
		{
			Id = "__root__",
			Name = "root",
			IsVisible = true,
			IsEnabled = true
		};

		/// <summary>
		/// 将导航项加入树结构并自动挂载到父节点
		/// </summary>
		/// <param name="item">导航项</param>
		/// <exception cref="ArgumentNullException">item 为 null</exception>
		/// <exception cref="ArgumentException">Id 为空</exception>
		/// <exception cref="InvalidOperationException">Id 已存在</exception>
		public void AddItem(NavigationItem item)
		{
			if (item == null) throw new ArgumentNullException(nameof(item));
			if (string.IsNullOrWhiteSpace(item.Id)) throw new ArgumentException("Id is required", nameof(item));
			if (_items.ContainsKey(item.Id)) throw new InvalidOperationException($"Item '{item.Id}' already exists");

			_items[item.Id] = item;
			AttachToParent(item.ParentId, item);
		}

		/// <summary>
		/// 删除指定导航项及其所有子孙
		/// </summary>
		/// <param name="id">导航项标识</param>
		public void RemoveItem(string id)
		{
			if (!_items.TryGetValue(id, out var item)) return;
			// 先移除子孙
			foreach (var desc in GetAllDescendants(id).ToList())
			{
				_items.Remove(desc.Id);
			}
			// 从父节点移除
			if (item.ParentId == null)
			{
				_root.RemoveChild(item.Id);
			}
			else if (_items.TryGetValue(item.ParentId, out var parent))
			{
				parent.RemoveChild(item.Id);
			}
			_items.Remove(id);
		}

		/// <summary>
		/// 获取指定标识的导航项
		/// </summary>
		/// <param name="id">导航项标识</param>
		/// <returns>导航项或 null</returns>
		public NavigationItem? GetItem(string id)
		{
			_items.TryGetValue(id, out var item);
			return item;
		}

		/// <summary>
		/// 更新指定导航项（按 Id 替换）
		/// </summary>
		/// <param name="item">导航项</param>
		/// <exception cref="ArgumentNullException">item 为 null</exception>
		/// <exception cref="KeyNotFoundException">项不存在</exception>
		public void UpdateItem(NavigationItem item)
		{
			if (item == null) throw new ArgumentNullException(nameof(item));
			if (!_items.ContainsKey(item.Id)) throw new KeyNotFoundException(item.Id);
			_items[item.Id] = item;
		}

		/// <summary>
		/// 重设指定项的排序序号
		/// </summary>
		/// <param name="itemId">导航项标识</param>
		/// <param name="newOrder">新序号</param>
		public void ReorderItem(string itemId, int newOrder)
		{
			if (!_items.TryGetValue(itemId, out var item)) return;
			item.Order = newOrder;
			if (item.ParentId == null)
			{
				// 根层
				var list = _root.Children.ToList();
				list.Sort(static (a, b) => a.Order.CompareTo(b.Order));
			}
			else if (_items.TryGetValue(item.ParentId, out var parent))
			{
				var list = parent.Children.ToList();
				list.Sort(static (a, b) => a.Order.CompareTo(b.Order));
			}
		}

		/// <summary>
		/// 将指定项移动到新的父节点并设定顺序
		/// </summary>
		/// <param name="itemId">导航项标识</param>
		/// <param name="newParentId">新父标识，null 为根</param>
		/// <param name="order">排序序号</param>
		public void MoveItem(string itemId, string? newParentId, int order)
		{
			if (!_items.TryGetValue(itemId, out var item)) return;

			// 从老父节点移除
			if (item.ParentId == null)
			{
				_root.RemoveChild(item.Id);
			}
			else if (_items.TryGetValue(item.ParentId, out var oldParent))
			{
				oldParent.RemoveChild(item.Id);
			}

			// 绑定新父
			item.ParentId = newParentId;
			item.Order = order;
			AttachToParent(newParentId, item);
		}

		/// <summary>
		/// 获取指定父节点的直接子项
		/// </summary>
		/// <param name="parentId">父标识，null 为根</param>
		/// <returns>子项集合</returns>
		public IEnumerable<NavigationItem> GetChildren(string? parentId)
		{
			if (parentId == null) return _root.Children;
			return _items.TryGetValue(parentId, out var parent) ? parent.Children : Enumerable.Empty<NavigationItem>();
		}

		/// <summary>
		/// 获取指定父节点的所有后代（深度优先）
		/// </summary>
		/// <param name="parentId">父标识，null 为根</param>
		/// <returns>后代集合</returns>
		public IEnumerable<NavigationItem> GetAllDescendants(string? parentId)
		{
			foreach (var child in GetChildren(parentId))
			{
				yield return child;
				foreach (var d in GetAllDescendants(child.Id)) yield return d;
			}
		}

		/// <summary>
		/// 获取指定项的所有祖先
		/// </summary>
		/// <param name="itemId">导航项标识</param>
		/// <returns>从父到根的祖先序列</returns>
		public IEnumerable<NavigationItem> GetAncestors(string itemId)
		{
			var current = GetItem(itemId);
			while (current != null && current.ParentId != null)
			{
				var parent = GetItem(current.ParentId);
				if (parent == null) yield break;
				yield return parent;
				current = parent;
			}
		}

		/// <summary>
		/// 获取面包屑路径（根到当前）
		/// </summary>
		/// <param name="itemId">导航项标识</param>
		/// <returns>breadcrumb 序列</returns>
		public IEnumerable<NavigationItem> GetBreadcrumb(string itemId)
		{
			return GetAncestors(itemId).Reverse().Concat(new[] { GetItem(itemId)! });
		}

		/// <summary>
		/// 获取所有项
		/// </summary>
		/// <returns>全部项集合</returns>
		public IEnumerable<NavigationItem> GetAllItems()
		{
			return _items.Values;
		}

		/// <summary>
		/// 获取可见项（IsVisible 为 true）
		/// </summary>
		/// <returns>可见项集合</returns>
		public IEnumerable<NavigationItem> GetVisibleItems()
		{
			return _items.Values.Where(x => x.IsVisible);
		}

		/// <summary>
		/// 获取虚拟根节点
		/// </summary>
		/// <returns>虚拟根</returns>
		public NavigationItem GetRoot() => _root;

		/// <summary>
		/// 将项挂载到父节点（父不存在则临时挂到根）
		/// </summary>
		/// <param name="parentId">父节点标识</param>
		/// <param name="item">导航项</param>
		private void AttachToParent(string? parentId, NavigationItem item)
		{
			if (parentId == null)
			{
				_root.AddChild(item);
			}
			else if (_items.TryGetValue(parentId, out var parent))
			{
				parent.AddChild(item);
			}
			else
			{
				// 父未添加，先挂到根；后续父加入时再 Move 也可以
				_root.AddChild(item);
			}
		}
	}
}


