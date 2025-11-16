using System;
using System.Collections.Generic;

namespace NavStack.Models
{
	/// <summary>
	/// 拖拽模式
	/// </summary>
	public enum DragDropMode
	{
		Reorder = 0,
		Move = 1,
		Both = 2
	}

	/// <summary>
	/// 放置位置
	/// </summary>
	public enum DropPosition
	{
		Before = 0,
		After = 1,
		AsChild = 2
	}

	/// <summary>
	/// 导航项拖拽配置
	/// </summary>
	public sealed class DragDropConfig
	{
		/// <summary>
		/// 是否允许对该项进行重排序/拖拽
		/// </summary>
		public bool AllowReorder { get; set; }

		/// <summary>
		/// 是否允许作为目标的子节点放置
		/// </summary>
		public bool AllowDropAsChild { get; set; }

		/// <summary>
		/// 该项生效的拖拽模式
		/// </summary>
		public DragDropMode Mode { get; set; } = DragDropMode.Both;

		/// <summary>
		/// 允许重排所需角色（为空表示不限制）
		/// </summary>
		public HashSet<string> RequireRoles { get; } = new(StringComparer.OrdinalIgnoreCase);
	}
}


