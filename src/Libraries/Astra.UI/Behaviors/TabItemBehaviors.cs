using Astra.UI.Models;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Astra.UI.Behaviors
{
    /// <summary>
    /// TabItem 拖动行为（参考 TreeViewItemBehaviors 实现）
    /// </summary>
    public static class TabItemBehaviors
    {
        public static readonly DependencyProperty DragDropCommandProperty =
            DependencyProperty.RegisterAttached(
                "DragDropCommand",
                typeof(ICommand),
                typeof(TabItemBehaviors),
                new PropertyMetadata(null, OnDragDropCommandChanged));

        public static ICommand GetDragDropCommand(DependencyObject obj) =>
            (ICommand)obj.GetValue(DragDropCommandProperty);

        public static void SetDragDropCommand(DependencyObject obj, ICommand value) =>
            obj.SetValue(DragDropCommandProperty, value);

        private static void OnDragDropCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TabItem item)
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

        private static Point _startPoint;
        private static TabItem _draggedItem;
        private static bool _isDragStarted;
        private static bool _isMouseDown;

        private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 检查是否点击在按钮上或 TextBox 上，如果是则不允许拖拽
            if (e.OriginalSource is System.Windows.Controls.Button ||
                e.OriginalSource is System.Windows.Controls.Primitives.ButtonBase ||
                e.OriginalSource is TextBox)
            {
                return;
            }

            if (sender is not TabItem item || item.DataContext == null)
            {
                return;
            }

            _startPoint = e.GetPosition(null);
            _draggedItem = item;
            _isMouseDown = true;
            _isDragStarted = false;
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

            var currentPoint = e.GetPosition(null);
            var diff = _startPoint - currentPoint;

            // 如果移动距离超过阈值，开始拖拽
            if (!_isDragStarted &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                 Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                _isDragStarted = true;

                var dragData = new DataObject("WorkflowTab", _draggedItem.DataContext);
                var result = DragDrop.DoDragDrop(_draggedItem, dragData, DragDropEffects.Move);

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
            if (sender is not TabItem item || item.DataContext == null)
            {
                return;
            }

            if (!e.Data.GetDataPresent("WorkflowTab"))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            var draggedData = e.Data.GetData("WorkflowTab");

            if (draggedData == item.DataContext)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private static void OnDragLeave(object sender, DragEventArgs e)
        {
            // 可以在这里添加视觉反馈
        }

        private static void OnDrop(object sender, DragEventArgs e)
        {
            if (sender is not TabItem item || item.DataContext == null)
            {
                return;
            }

            if (!e.Data.GetDataPresent("WorkflowTab"))
            {
                return;
            }

            var sourceTab = e.Data.GetData("WorkflowTab") as WorkflowTab;
            var targetTab = item.DataContext as WorkflowTab;

            if (sourceTab == null || targetTab == null || sourceTab == targetTab)
            {
                return;
            }

            var command = GetDragDropCommand(item);
            if (command != null && command.CanExecute(new { Source = sourceTab, Target = targetTab }))
            {
                command.Execute(new { Source = sourceTab, Target = targetTab });
            }

            e.Handled = true;
        }

        private static void OnGiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            e.UseDefaultCursors = true;
            e.Handled = true;
        }
    }
}

