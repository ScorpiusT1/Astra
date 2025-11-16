using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using NavStack.Authorization;
using NavStack.Models;

namespace NavStack.Services
{
	/// <summary>
	/// 拖拽服务最小实现：使用内存历史栈提供撤销/重做
	/// </summary>
	public sealed class DragDropService : IDragDropService
	{
		private readonly INavigationTreeService _tree;
		private readonly INavigationAuthorizationService _auth;

		private bool _enabled = true;
		private bool _managementMode;
		private NavigationItem? _dragging;
		private readonly Stack<Action> _undo = new();
		private readonly Stack<Action> _redo = new();

		/// <summary>
		/// 构造 DragDropService
		/// </summary>
		/// <param name="tree">导航树服务</param>
		/// <param name="auth">权限服务</param>
		public DragDropService(INavigationTreeService tree, INavigationAuthorizationService auth)
		{
			_tree = tree;
			_auth = auth;
		}

		/// <inheritdoc/>
		public async Task<bool> CanStartDragAsync(NavigationItem item, IUserContext user)
		{
			if (!_enabled || item.DragDrop?.AllowReorder != true) return false;
			return await _auth.CanReorderAsync(item, user).ConfigureAwait(false);
		}

		/// <inheritdoc/>
		public async Task<bool> CanDropAsync(NavigationItem source, NavigationItem target, DropPosition position, IUserContext user)
		{
			if (!_enabled) return false;
			if (ReferenceEquals(source, target)) return false;
			if (position == DropPosition.AsChild && target.DragDrop?.AllowDropAsChild != true) return false;

			// 基本权限：源与目标都需可访问
			var canSource = await _auth.CanAccessAsync(source, user).ConfigureAwait(false);
			var canTarget = await _auth.CanAccessAsync(target, user).ConfigureAwait(false);
			return canSource && canTarget;
		}

		/// <inheritdoc/>
		public void StartDrag(NavigationItem item)
		{
			_dragging = item;
		}

		/// <inheritdoc/>
		public void UpdateDragPosition(Point position)
		{
			// 仅用于可视反馈；核心逻辑无需实现
		}

		/// <inheritdoc/>
		public Task<bool> CompleteDragAsync(NavigationItem source, NavigationItem target, DropPosition position)
		{
			if (!_enabled) return Task.FromResult(false);

			var originalParent = source.ParentId;
			var originalOrder = source.Order;

			switch (position)
			{
				case DropPosition.AsChild:
					{
						_tree.MoveItem(source.Id, target.Id, order: int.MaxValue);
						break;
					}
				case DropPosition.Before:
					{
						_tree.MoveItem(source.Id, target.ParentId, target.Order - 1);
						break;
					}
				case DropPosition.After:
					{
						_tree.MoveItem(source.Id, target.ParentId, target.Order + 1);
						break;
					}
			}

			// 记录撤销动作
			_undo.Push(() =>
			{
				_tree.MoveItem(source.Id, originalParent, originalOrder);
			});
			_redo.Clear();

			_dragging = null;
			return Task.FromResult(true);
		}

		/// <inheritdoc/>
		public void CancelDrag()
		{
			_dragging = null;
		}

		/// <inheritdoc/>
		public void Undo()
		{
			if (_undo.Count == 0) return;
			var action = _undo.Pop();
			action();
			_redo.Push(action); // 简化处理：重复 action 作为重做占位
		}

		/// <inheritdoc/>
		public void Redo()
		{
			if (_redo.Count == 0) return;
			var action = _redo.Pop();
			action();
			_undo.Push(action);
		}

		/// <inheritdoc/>
		public bool CanUndo => _undo.Count > 0;

		/// <inheritdoc/>
		public bool CanRedo => _redo.Count > 0;

		/// <inheritdoc/>
		public void SetDragDropEnabled(bool enabled)
		{
			_enabled = enabled;
		}

		/// <inheritdoc/>
		public void SetManagementMode(bool enabled)
		{
			_managementMode = enabled;
		}
	}
}


