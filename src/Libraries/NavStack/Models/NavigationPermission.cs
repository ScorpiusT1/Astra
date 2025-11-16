using System;
using System.Collections.Generic;

namespace NavStack.Models
{
	/// <summary>
	/// 导航项权限配置模型
	/// </summary>
	public sealed class NavigationPermission
	{
		/// <summary>
		/// 允许访问的角色
		/// </summary>
		public HashSet<string> Roles { get; } = new(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// 访问需要的权限标识
		/// </summary>
		public HashSet<string> Permissions { get; } = new(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// 权限策略名称（由策略引擎解析）
		/// </summary>
		public string? Policy { get; set; }

		/// <summary>
		/// 是否需要全部权限满足；false 为满足任一即可
		/// </summary>
		public bool RequireAll { get; set; }

		/// <summary>
		/// 是否继承父节点权限
		/// </summary>
		public bool InheritParentPermission { get; set; } = true;

		/// <summary>
		/// 明确拒绝的角色（黑名单）
		/// </summary>
		public HashSet<string> DenyRoles { get; } = new(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// 允许进行排序/拖拽的角色
		/// </summary>
		public HashSet<string> CanReorderRoles { get; } = new(StringComparer.OrdinalIgnoreCase);
	}
}


