using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Astra.Behaviors
{
    public static class TreeViewItemBehaviors
    {
        public static readonly DependencyProperty SelectedCommandProperty =
            DependencyProperty.RegisterAttached(
                "SelectedCommand",
                typeof(ICommand),
                typeof(TreeViewItemBehaviors),
                new PropertyMetadata(null, OnSelectedCommandChanged));

        public static readonly DependencyProperty SelectedCommandParameterProperty =
            DependencyProperty.RegisterAttached(
                "SelectedCommandParameter",
                typeof(object),
                typeof(TreeViewItemBehaviors),
                new PropertyMetadata(null));

        public static readonly DependencyProperty DragDropCommandProperty =
            DependencyProperty.RegisterAttached(
                "DragDropCommand",
                typeof(ICommand),
                typeof(TreeViewItemBehaviors),
                new PropertyMetadata(null, OnDragDropCommandChanged));

        public static readonly DependencyProperty DeleteCommandProperty =
            DependencyProperty.RegisterAttached(
                "DeleteCommand",
                typeof(ICommand),
                typeof(TreeViewItemBehaviors),
                new PropertyMetadata(null, OnDeleteCommandChanged));

        public static ICommand GetSelectedCommand(DependencyObject obj) =>
            (ICommand)obj.GetValue(SelectedCommandProperty);

        public static void SetSelectedCommand(DependencyObject obj, ICommand value) =>
            obj.SetValue(SelectedCommandProperty, value);

        public static object GetSelectedCommandParameter(DependencyObject obj) =>
            obj.GetValue(SelectedCommandParameterProperty);

        public static void SetSelectedCommandParameter(DependencyObject obj, object value) =>
            obj.SetValue(SelectedCommandParameterProperty, value);

        public static ICommand GetDragDropCommand(DependencyObject obj) =>
            (ICommand)obj.GetValue(DragDropCommandProperty);

        public static void SetDragDropCommand(DependencyObject obj, ICommand value) =>
            obj.SetValue(DragDropCommandProperty, value);

        public static ICommand GetDeleteCommand(DependencyObject obj) =>
            (ICommand)obj.GetValue(DeleteCommandProperty);

        public static void SetDeleteCommand(DependencyObject obj, ICommand value) =>
            obj.SetValue(DeleteCommandProperty, value);

        private static void OnSelectedCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TreeViewItem item)
            {
                return;
            }

            if (e.OldValue != null)
            {
                item.Selected -= OnTreeViewItemSelected;
            }

            if (e.NewValue != null)
            {
                item.Selected += OnTreeViewItemSelected;
            }
        }

        private static void OnDragDropCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TreeViewItem item)
            {
                return;
            }

            if (e.OldValue != null)
            {
                item.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
                item.PreviewMouseMove -= OnPreviewMouseMove;
                item.PreviewMouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;
                item.Drop -= OnDrop;
                item.DragOver -= OnDragOver;
                item.DragLeave -= OnDragLeave;
                item.GiveFeedback -= OnGiveFeedback;
            }

            if (e.NewValue != null)
            {
                item.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
                item.PreviewMouseMove += OnPreviewMouseMove;
                item.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
                item.Drop += OnDrop;
                item.DragOver += OnDragOver;
                item.DragLeave += OnDragLeave;
                item.GiveFeedback += OnGiveFeedback;
            }
        }

        private static void OnDeleteCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // DeleteCommand 通过绑定使用，不需要事件处理
        }

        private static Point _startPoint;
        private static TreeViewItem _draggedItem;
        private static bool _isDragStarted;
        private static bool _isMouseDown;

        private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 检查是否点击在按钮上，如果是则不允许拖拽
            if (e.OriginalSource is System.Windows.Controls.Button ||
                e.OriginalSource is System.Windows.Controls.Primitives.ButtonBase)
            {
                return;
            }

            if (sender is not TreeViewItem item || item.DataContext == null)
            {
                return;
            }

            // 只允许子节点拖拽（不允许根节点拖拽）
            if (item.DataContext is ViewModels.TreeNodeViewModel node)
            {
                // 检查是否是根节点（通过检查是否有父节点来判断）
                // 这里我们通过检查是否在根集合中来判断
                // 暂时允许所有节点拖拽，在 DragDropNode 方法中会限制
                _startPoint = e.GetPosition(null);
                _draggedItem = item;
                _isMouseDown = true;
                _isDragStarted = false;
            }
        }

        private static void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isMouseDown || _draggedItem == null)
            {
                return;
            }

            if (e.LeftButton != MouseButtonState.Pressed)
            {
                _isMouseDown = false;
                _isDragStarted = false;
                _draggedItem = null;
                return;
            }

            if (_draggedItem.DataContext == null)
            {
                return;
            }

            var currentPoint = e.GetPosition(null);
            var diff = _startPoint - currentPoint;

            // 如果移动距离超过阈值，开始拖拽
            if (!_isDragStarted && 
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                 Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                _isDragStarted = true;

                // 设置拖拽状态
                if (_draggedItem.DataContext is ViewModels.TreeNodeViewModel node)
                {
                    node.IsDragging = true;
                }

                var dragData = new DataObject("TreeNodeViewModel", _draggedItem.DataContext);
                var result = DragDrop.DoDragDrop(_draggedItem, dragData, DragDropEffects.Move);
                
                // 清除拖拽状态
                if (_draggedItem.DataContext is ViewModels.TreeNodeViewModel node2)
                {
                    node2.IsDragging = false;
                }

                _isMouseDown = false;
                _isDragStarted = false;
                _draggedItem = null;
            }
        }

        private static void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isMouseDown = false;
            _isDragStarted = false;
            _draggedItem = null;
        }

        private static void OnDragOver(object sender, DragEventArgs e)
        {
            if (sender is not TreeViewItem item || item.DataContext == null)
            {
                return;
            }

            if (!e.Data.GetDataPresent("TreeNodeViewModel"))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            var draggedData = e.Data.GetData("TreeNodeViewModel");
            if (draggedData == item.DataContext)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            // 设置可放置状态
            if (item.DataContext is ViewModels.TreeNodeViewModel targetNode)
            {
                targetNode.CanDrop = true;
            }

            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private static void OnDragLeave(object sender, DragEventArgs e)
        {
            if (sender is not TreeViewItem item || item.DataContext == null)
            {
                return;
            }

            // 清除可放置状态
            if (item.DataContext is ViewModels.TreeNodeViewModel node)
            {
                node.CanDrop = false;
            }
        }

        private static void OnDrop(object sender, DragEventArgs e)
        {
            if (sender is not TreeViewItem item || item.DataContext == null)
            {
                return;
            }

            if (!e.Data.GetDataPresent("TreeNodeViewModel"))
            {
                return;
            }

            var draggedData = e.Data.GetData("TreeNodeViewModel");
            if (draggedData == item.DataContext)
            {
                return;
            }

            // 清除可放置状态
            if (item.DataContext is ViewModels.TreeNodeViewModel targetNode)
            {
                targetNode.CanDrop = false;
            }

            var command = GetDragDropCommand(item);
            if (command != null && command.CanExecute(new { Source = draggedData, Target = item.DataContext }))
            {
                command.Execute(new { Source = draggedData, Target = item.DataContext });
            }

            e.Handled = true;
        }

        private static void OnGiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            e.UseDefaultCursors = true;
            e.Handled = true;
        }

        private static void OnTreeViewItemSelected(object sender, RoutedEventArgs e)
        {
            if (sender is not TreeViewItem item)
            {
                return;
            }

            if (!ReferenceEquals(e.OriginalSource, item))
            {
                return;
            }

            var command = GetSelectedCommand(item);
            if (command == null)
            {
                return;
            }

            var parameter = GetSelectedCommandParameter(item) ?? item.DataContext;
            if (command.CanExecute(parameter))
            {
                command.Execute(parameter);
            }
        }
    }
}
