using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using NavStack.Models;
using NavStack.Services;
using NavStack.ViewModels;

namespace NavStack.Authorization
{
	/// <summary>
	/// 导航权限验证服务（最小实现，含简单缓存）
	/// </summary>
	public sealed class NavigationAuthorizationService : INavigationAuthorizationService
	{
		private readonly INavigationTreeService _tree;

		// 缓存：userId|itemId -> 结果
		private readonly ConcurrentDictionary<string, bool> _accessCache = new();
		private readonly ConcurrentDictionary<string, bool> _reorderCache = new();

		/// <summary>
		/// 构造权限服务
		/// </summary>
		/// <param name="tree">导航树服务</param>
		public NavigationAuthorizationService(INavigationTreeService tree)
		{
			_tree = tree;
		}

		/// <inheritdoc/>
		public Task<bool> CanAccessAsync(string itemId, IUserContext user)
		{
			var item = _tree.GetItem(itemId);
			if (item == null) return Task.FromResult(false);
			return CanAccessAsync(item, user);
		}

		/// <inheritdoc/>
		public Task<bool> CanAccessAsync(NavigationItem item, IUserContext user)
		{
			var cacheKey = $"{user.UserId}|{item.Id}";
			if (_accessCache.TryGetValue(cacheKey, out var cached)) return Task.FromResult(cached);

			var allowed = EvaluateAccess(item, user);
			_accessCache[cacheKey] = allowed;
			return Task.FromResult(allowed);
		}

		/// <inheritdoc/>
		public Task<bool> CanReorderAsync(NavigationItem item, IUserContext user)
		{
			var cacheKey = $"{user.UserId}|reorder|{item.Id}";
			if (_reorderCache.TryGetValue(cacheKey, out var cached)) return Task.FromResult(cached);

			bool result = item.DragDrop?.AllowReorder == true &&
				(item.DragDrop.RequireRoles.Count == 0 ||
				 item.DragDrop.RequireRoles.Any(user.IsInRole));

			_reorderCache[cacheKey] = result;
			return Task.FromResult(result);
		}

		/// <inheritdoc/>
		public async Task<ObservableCollection<NavigationItemViewModel>> FilterByPermissionAsync(
			IEnumerable<NavigationItem> items, IUserContext user)
		{
			var roots = new ObservableCollection<NavigationItemViewModel>();
			var itemList = items.OrderBy(i => i.Order).ToList();
			var idToVm = new Dictionary<string, NavigationItemViewModel>(StringComparer.Ordinal);

			foreach (var item in itemList)
			{
				if (!await CanAccessAsync(item, user).ConfigureAwait(false)) continue;
				var vm = new NavigationItemViewModel(item);
				idToVm[item.Id] = vm;
			}

			// 组装树（只放入有权限的父/子）
			foreach (var vm in idToVm.Values)
			{
				var parentId = vm.Model.ParentId;
				if (parentId != null && idToVm.TryGetValue(parentId, out var parentVm))
				{
					parentVm.Children.Add(vm);
				}
				else
				{
					roots.Add(vm);
				}
			}

			// 移除空父节点（没有任何可访问子项且自己不可访问的已被过滤）
			PruneEmptyParents(roots);
			return roots;
		}

		/// <inheritdoc/>
		public async Task<ObservableCollection<NavigationItemViewModel>> GetNavigationTreeForUserAsync(IUserContext user)
		{
			var topLevel = _tree.GetChildren(parentId: null);
			return await FilterByPermissionAsync(topLevel, user).ConfigureAwait(false);
		}

		/// <inheritdoc/>
		public void ClearPermissionCache()
		{
			_accessCache.Clear();
			_reorderCache.Clear();
		}

		/// <inheritdoc/>
		public void ClearPermissionCache(string userId)
		{
			foreach (var key in _accessCache.Keys.Where(k => k.StartsWith(userId + "|", StringComparison.Ordinal)).ToList())
			{
				_accessCache.TryRemove(key, out _);
			}
			foreach (var key in _reorderCache.Keys.Where(k => k.StartsWith(userId + "|", StringComparison.Ordinal)).ToList())
			{
				_reorderCache.TryRemove(key, out _);
			}
		}

		/// <inheritdoc/>
		public Task RefreshPermissionsAsync()
		{
			// 占位：从外部源刷新权限时可实现
			ClearPermissionCache();
			return Task.CompletedTask;
		}

		/// <inheritdoc/>
		public void SetPermission(string itemId, NavigationPermission permission)
		{
			var item = _tree.GetItem(itemId);
			if (item == null) return;
			item.Permission = permission;
			ClearPermissionCache();
		}

		/// <inheritdoc/>
		public void AddRole(string itemId, params string[] roles)
		{
			var item = _tree.GetItem(itemId);
			if (item == null) return;
				item.Permission ??= new NavigationPermission();
			foreach (var r in roles) item.Permission.Roles.Add(r);
			ClearPermissionCache();
		}

		/// <inheritdoc/>
		public void AddPermission(string itemId, params string[] permissions)
		{
			var item = _tree.GetItem(itemId);
			if (item == null) return;
			item.Permission ??= new NavigationPermission();
			foreach (var p in permissions) item.Permission.Permissions.Add(p);
			ClearPermissionCache();
		}

		/// <inheritdoc/>
		public void SetPolicy(string itemId, string policyName)
		{
			var item = _tree.GetItem(itemId);
			if (item == null) return;
			item.Permission ??= new NavigationPermission();
			item.Permission.Policy = policyName;
			ClearPermissionCache();
		}

		/// <inheritdoc/>
		public void RemovePermission(string itemId)
		{
			var item = _tree.GetItem(itemId);
			if (item == null) return;
			item.Permission = null;
			ClearPermissionCache();
		}

		private static void PruneEmptyParents(ObservableCollection<NavigationItemViewModel> roots)
		{
			for (int i = roots.Count - 1; i >= 0; i--)
			{
				var vm = roots[i];
				PruneEmptyParents(vm.Children);
				// 如果没有子项且自身可见为 false，可按需要裁剪；当前策略保留自身
			}
		}

		private static bool EvaluateAccess(NavigationItem item, IUserContext user)
		{
			// 基础：未配置权限则默认可访问（可根据需要修改为默认拒绝）
			if (item.Permission == null) return true;

			// 黑名单优先生效
			if (item.Permission.DenyRoles.Any(user.IsInRole)) return false;

			bool roleOk = item.Permission.Roles.Count == 0 || item.Permission.Roles.Any(user.IsInRole);
			bool permOk = item.Permission.Permissions.Count == 0; // 未配置权限即视为通过

			// 组合权限判断
			if (item.Permission.Permissions.Count > 0)
			{
				if (item.Permission.RequireAll)
				{
					// 无Claims来源，最小实现仅按角色判定；扩展时可接入 Claims/Policy
					permOk = true;
				}
				else
				{
					permOk = true;
				}
			}

			// Policy 最小实现：存在即认为需要额外策略，通过留空策略引擎默认为通过（后续可扩展）
			bool policyOk = true;

			return roleOk && permOk && policyOk;
		}
	}
}


