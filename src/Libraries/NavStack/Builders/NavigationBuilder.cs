using System;
using System.Collections.Generic;
using NavStack.Models;

namespace NavStack.Builders
{
	/// <summary>
	/// 流式导航构建器
	/// </summary>
	public sealed class NavigationBuilder
	{
		private readonly List<NavigationItem> _roots = new();
		private readonly Stack<NavigationItem> _stack = new();

		private NavigationBuilder() { }

		/// <summary>
		/// 创建新的导航构建器实例
		/// </summary>
		/// <returns>构建器实例</returns>
		public static NavigationBuilder Create() => new();

		/// <summary>
		/// 生成当前已构建的顶层导航项集合（只读）
		/// </summary>
		/// <returns>只读的顶层导航项列表</returns>
		public IReadOnlyList<NavigationItem> Build() => _roots.AsReadOnly();

		/// <summary>
		/// 添加一个导航项；若当前存在父级上下文，则作为其子项；否则作为根项
		/// </summary>
		/// <param name="id">唯一标识</param>
		/// <param name="name">显示名称</param>
		/// <returns>构建器自身</returns>
		public NavigationBuilder AddItem(string id, string name)
		{
			var item = new NavigationItem { Id = id, Name = name };
			if (_stack.Count == 0)
			{
				_roots.Add(item);
			}
			else
			{
				_stack.Peek().AddChild(item);
			}
			_stack.Push(item);
			return this;
		}

		/// <summary>
		/// 为当前导航项设置图标
		/// </summary>
		/// <param name="icon">图标数据（路径/Geometry）</param>
		/// <returns>构建器自身</returns>
		public NavigationBuilder WithIcon(string icon)
		{
			if (_stack.Count == 0) throw new InvalidOperationException("No current item.");
			_stack.Peek().Icon = icon;
			return this;
		}

		/// <summary>
		/// 为当前导航项设置关联视图类型
		/// </summary>
		/// <typeparam name="TView">视图类型</typeparam>
		/// <returns>构建器自身</returns>
		public NavigationBuilder WithView<TView>()
		{
			if (_stack.Count == 0) throw new InvalidOperationException("No current item.");
			_stack.Peek().ViewType = typeof(TView);
			return this;
		}

		/// <summary>
		/// 为当前导航项设置区域名称（用于区域导航）
		/// </summary>
		/// <param name="regionName">区域名称</param>
		/// <returns>构建器自身</returns>
		public NavigationBuilder ForRegion(string regionName)
		{
			if (_stack.Count == 0) throw new InvalidOperationException("No current item.");
			_stack.Peek().RegionName = regionName;
			return this;
		}

		/// <summary>
		/// 为当前导航项设置排序序号
		/// </summary>
		/// <param name="order">排序序号</param>
		/// <returns>构建器自身</returns>
		public NavigationBuilder WithOrder(int order)
		{
			if (_stack.Count == 0) throw new InvalidOperationException("No current item.");
			_stack.Peek().Order = order;
			return this;
		}

		/// <summary>
		/// 为当前导航项追加权限列表（Permissions）
		/// </summary>
		/// <param name="permissions">权限标识集合</param>
		/// <returns>构建器自身</returns>
		public NavigationBuilder WithPermissions(params string[] permissions)
		{
			if (_stack.Count == 0) throw new InvalidOperationException("No current item.");
			_stack.Peek().Permission ??= new NavigationPermission();
			foreach (var p in permissions) _stack.Peek().Permission.Permissions.Add(p);
			return this;
		}

		/// <summary>
		/// 为当前导航项追加角色列表（Roles）
		/// </summary>
		/// <param name="roles">角色集合</param>
		/// <returns>构建器自身</returns>
		public NavigationBuilder WithRoles(params string[] roles)
		{
			if (_stack.Count == 0) throw new InvalidOperationException("No current item.");
			_stack.Peek().Permission ??= new NavigationPermission();
			foreach (var r in roles) _stack.Peek().Permission.Roles.Add(r);
			return this;
		}

		/// <summary>
		/// 为当前导航项设置策略名称（Policy）
		/// </summary>
		/// <param name="policyName">策略名称</param>
		/// <returns>构建器自身</returns>
		public NavigationBuilder WithPolicy(string policyName)
		{
			if (_stack.Count == 0) throw new InvalidOperationException("No current item.");
			_stack.Peek().Permission ??= new NavigationPermission();
			_stack.Peek().Permission.Policy = policyName;
			return this;
		}

		/// <summary>
		/// 为当前导航项配置拒绝访问的角色（黑名单）
		/// </summary>
		/// <param name="roles">被拒绝的角色列表</param>
		/// <returns>构建器自身</returns>
		public NavigationBuilder DenyRoles(params string[] roles)
		{
			if (_stack.Count == 0) throw new InvalidOperationException("No current item.");
			_stack.Peek().Permission ??= new NavigationPermission();
			foreach (var r in roles) _stack.Peek().Permission.DenyRoles.Add(r);
			return this;
		}

		/// <summary>
		/// 配置当前项拖拽排序能力
		/// </summary>
		/// <param name="allow">是否允许重排</param>
		/// <param name="requireRoles">需要具备的角色（可选）</param>
		/// <param name="allowDropAsChild">是否允许作为目标的子节点放置</param>
		/// <returns>构建器自身</returns>
		public NavigationBuilder AllowReorder(bool allow, string[]? requireRoles = null, bool allowDropAsChild = false)
		{
			if (_stack.Count == 0) throw new InvalidOperationException("No current item.");
			_stack.Peek().DragDrop ??= new DragDropConfig();
			_stack.Peek().DragDrop.AllowReorder = allow;
			_stack.Peek().DragDrop.AllowDropAsChild = allowDropAsChild;
			if (requireRoles != null)
			{
				foreach (var r in requireRoles) _stack.Peek().DragDrop.RequireRoles.Add(r);
			}
			return this;
		}

		/// <summary>
		/// 以当前项作为父，继续添加子项
		/// </summary>
		/// <param name="id">子项标识</param>
		/// <param name="name">子项名称</param>
		/// <returns>构建器自身</returns>
		public NavigationBuilder AddChild(string id, string name)
		{
			return AddItem(id, name);
		}

		/// <summary>
		/// 结束当前项的配置，返回其父级上下文
		/// </summary>
		/// <returns>构建器自身</returns>
		public NavigationBuilder Parent()
		{
			if (_stack.Count > 0) _stack.Pop();
			return this;
		}
	}
}


