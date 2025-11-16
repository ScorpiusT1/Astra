using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using NavStack.Models;
using NavStack.ViewModels;

namespace NavStack.Authorization
{
	/// <summary>
	/// 导航权限验证服务
	/// </summary>
	public interface INavigationAuthorizationService
	{
		/// <summary>
		/// 判断用户是否可访问指定导航项
		/// </summary>
		/// <param name="itemId">导航项标识</param>
		/// <param name="user">用户上下文</param>
		/// <returns>是否可访问</returns>
		Task<bool> CanAccessAsync(string itemId, IUserContext user);

		/// <summary>
		/// 判断用户是否可访问指定导航项
		/// </summary>
		/// <param name="item">导航项</param>
		/// <param name="user">用户上下文</param>
		/// <returns>是否可访问</returns>
		Task<bool> CanAccessAsync(NavigationItem item, IUserContext user);

		/// <summary>
		/// 判断用户是否有权对导航项进行排序/拖拽
		/// </summary>
		/// <param name="item">导航项</param>
		/// <param name="user">用户上下文</param>
		/// <returns>是否可重排</returns>
		Task<bool> CanReorderAsync(NavigationItem item, IUserContext user);

		/// <summary>
		/// 按权限过滤导航项集合并生成对应的 ViewModel 树
		/// </summary>
		/// <param name="items">导航项序列</param>
		/// <param name="user">用户上下文</param>
		/// <returns>可访问的导航树</returns>
		Task<ObservableCollection<NavigationItemViewModel>> FilterByPermissionAsync(
			IEnumerable<NavigationItem> items, IUserContext user);

		/// <summary>
		/// 获取用户可访问的整个导航树
		/// </summary>
		/// <param name="user">用户上下文</param>
		/// <returns>可访问的导航树</returns>
		Task<ObservableCollection<NavigationItemViewModel>> GetNavigationTreeForUserAsync(IUserContext user);

		/// <summary>
		/// 清空权限缓存（全部用户）
		/// </summary>
		void ClearPermissionCache();

		/// <summary>
		/// 清空指定用户的权限缓存
		/// </summary>
		/// <param name="userId">用户标识</param>
		void ClearPermissionCache(string userId);

		/// <summary>
		/// 刷新权限数据源（如从远端拉取）
		/// </summary>
		Task RefreshPermissionsAsync();

		/// <summary>
		/// 为指定项设置权限配置对象
		/// </summary>
		/// <param name="itemId">导航项标识</param>
		/// <param name="permission">权限配置</param>
		void SetPermission(string itemId, NavigationPermission permission);

		/// <summary>
		/// 向指定项追加允许访问的角色
		/// </summary>
		/// <param name="itemId">导航项标识</param>
		/// <param name="roles">角色列表</param>
		void AddRole(string itemId, params string[] roles);

		/// <summary>
		/// 向指定项追加访问所需的权限标识
		/// </summary>
		/// <param name="itemId">导航项标识</param>
		/// <param name="permissions">权限标识</param>
		void AddPermission(string itemId, params string[] permissions);

		/// <summary>
		/// 为指定项设置权限策略名称
		/// </summary>
		/// <param name="itemId">导航项标识</param>
		/// <param name="policyName">策略名称</param>
		void SetPolicy(string itemId, string policyName);

		/// <summary>
		/// 移除指定项的权限配置
		/// </summary>
		/// <param name="itemId">导航项标识</param>
		void RemovePermission(string itemId);
	}
}


