using System.Threading.Tasks;
using System.Windows;
using NavStack.Authorization;
using NavStack.Models;

namespace NavStack.Services
{
	/// <summary>
	/// 拖拽服务抽象：负责拖拽验证、执行与撤销/重做
	/// </summary>
	public interface IDragDropService
	{
		/// <summary>
		/// 判断是否可以开始拖拽
		/// </summary>
		/// <param name="item">源导航项</param>
		/// <param name="user">用户上下文</param>
		/// <returns>是否允许开始拖拽</returns>
		Task<bool> CanStartDragAsync(NavigationItem item, IUserContext user);

		/// <summary>
		/// 判断是否可以在目标位置放置
		/// </summary>
		/// <param name="source">源项</param>
		/// <param name="target">目标项</param>
		/// <param name="position">放置位置</param>
		/// <param name="user">用户上下文</param>
		/// <returns>是否允许放置</returns>
		Task<bool> CanDropAsync(NavigationItem source, NavigationItem target, DropPosition position, IUserContext user);

		/// <summary>
		/// 开始拖拽（记录初始状态）
		/// </summary>
		/// <param name="item">源项</param>
		void StartDrag(NavigationItem item);

		/// <summary>
		/// 更新拖拽位置（用于可视反馈）
		/// </summary>
		/// <param name="position">屏幕坐标</param>
		void UpdateDragPosition(Point position);

		/// <summary>
		/// 完成拖拽并应用变更
		/// </summary>
		/// <param name="source">源项</param>
		/// <param name="target">目标项</param>
		/// <param name="position">放置位置</param>
		/// <returns>是否成功</returns>
		Task<bool> CompleteDragAsync(NavigationItem source, NavigationItem target, DropPosition position);

		/// <summary>
		/// 取消当前拖拽
		/// </summary>
		void CancelDrag();

		/// <summary>
		/// 撤销一步
		/// </summary>
		void Undo();

		/// <summary>
		/// 重做一步
		/// </summary>
		void Redo();

		/// <summary>
		/// 是否可以撤销
		/// </summary>
		bool CanUndo { get; }

		/// <summary>
		/// 是否可以重做
		/// </summary>
		bool CanRedo { get; }

		/// <summary>
		/// 启用/禁用拖拽
		/// </summary>
		/// <param name="enabled">是否启用</param>
		void SetDragDropEnabled(bool enabled);

		/// <summary>
		/// 设置管理模式（影响是否显示拖拽手柄等）
		/// </summary>
		/// <param name="enabled">是否为管理模式</param>
		void SetManagementMode(bool enabled);
	}
}


