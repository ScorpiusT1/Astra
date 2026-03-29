using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Astra.UI.Behaviors
{
    public sealed class DataGridRowReorderInfo
    {
        public object? SourceItem { get; init; }
        public object? TargetItem { get; init; }
    }

    public static class DataGridRowReorderBehavior
    {
        public static readonly DependencyProperty EnableRowReorderProperty =
            DependencyProperty.RegisterAttached(
                "EnableRowReorder",
                typeof(bool),
                typeof(DataGridRowReorderBehavior),
                new PropertyMetadata(false, OnEnableRowReorderChanged));

        public static readonly DependencyProperty ReorderCommandProperty =
            DependencyProperty.RegisterAttached(
                "ReorderCommand",
                typeof(ICommand),
                typeof(DataGridRowReorderBehavior),
                new PropertyMetadata(null));

        private static readonly DependencyProperty DragStartPointProperty =
            DependencyProperty.RegisterAttached(
                "DragStartPoint",
                typeof(Point),
                typeof(DataGridRowReorderBehavior),
                new PropertyMetadata(default(Point)));

        private static readonly DependencyProperty DragSourceItemProperty =
            DependencyProperty.RegisterAttached(
                "DragSourceItem",
                typeof(object),
                typeof(DataGridRowReorderBehavior),
                new PropertyMetadata(null));

        public static void SetEnableRowReorder(DependencyObject element, bool value) => element.SetValue(EnableRowReorderProperty, value);
        public static bool GetEnableRowReorder(DependencyObject element) => (bool)element.GetValue(EnableRowReorderProperty);

        public static void SetReorderCommand(DependencyObject element, ICommand? value) => element.SetValue(ReorderCommandProperty, value);
        public static ICommand? GetReorderCommand(DependencyObject element) => (ICommand?)element.GetValue(ReorderCommandProperty);

        private static void SetDragStartPoint(DependencyObject element, Point value) => element.SetValue(DragStartPointProperty, value);
        private static Point GetDragStartPoint(DependencyObject element) => (Point)element.GetValue(DragStartPointProperty);

        private static void SetDragSourceItem(DependencyObject element, object? value) => element.SetValue(DragSourceItemProperty, value);
        private static object? GetDragSourceItem(DependencyObject element) => element.GetValue(DragSourceItemProperty);

        private static void OnEnableRowReorderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DataGrid grid)
            {
                return;
            }

            if ((bool)e.NewValue)
            {
                grid.AllowDrop = true;
                grid.PreviewMouseLeftButtonDown += GridOnPreviewMouseLeftButtonDown;
                grid.PreviewMouseMove += GridOnPreviewMouseMove;
                grid.DragOver += GridOnDragOver;
                grid.Drop += GridOnDrop;
            }
            else
            {
                grid.PreviewMouseLeftButtonDown -= GridOnPreviewMouseLeftButtonDown;
                grid.PreviewMouseMove -= GridOnPreviewMouseMove;
                grid.DragOver -= GridOnDragOver;
                grid.Drop -= GridOnDrop;
            }
        }

        private static void GridOnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGrid grid || !GetEnableRowReorder(grid))
            {
                return;
            }

            // 在文本框、复选框等控件上拖选时不得启动行拖拽，否则 DoDragDrop 会抢占鼠标并表现为“禁用”光标。
            if (ShouldIgnoreRowDragMouseDown(e.OriginalSource as DependencyObject))
            {
                SetDragSourceItem(grid, null);
                return;
            }

            SetDragStartPoint(grid, e.GetPosition(grid));
            SetDragSourceItem(grid, GetRowItemFromEvent(e));
        }

        /// <summary>
        /// 鼠标按下是否发生在应保留给控件自身交互（拖选文字、点选复选框等）的区域。
        /// </summary>
        private static bool ShouldIgnoreRowDragMouseDown(DependencyObject? source)
        {
            while (source != null && source is not DataGridRow)
            {
                switch (source)
                {
                    case TextBox:
                    case RichTextBox:
                    case PasswordBox:
                    case ComboBox:
                    case CheckBox:
                    case ButtonBase:
                    case Slider:
                    case ScrollBar:
                    case Thumb:
                        return true;
                }

                if (source is ComboBoxItem)
                {
                    return true;
                }

                source = VisualTreeHelper.GetParent(source);
            }

            return false;
        }

        private static void GridOnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (sender is not DataGrid grid || !GetEnableRowReorder(grid))
            {
                return;
            }

            if (e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var source = GetDragSourceItem(grid);
            if (source == null)
            {
                return;
            }

            var start = GetDragStartPoint(grid);
            var current = e.GetPosition(grid);
            if (Math.Abs(current.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(current.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            DragDrop.DoDragDrop(grid, new DataObject(typeof(object), source), DragDropEffects.Move);
        }

        private static void GridOnDragOver(object sender, DragEventArgs e)
        {
            if (sender is not DataGrid grid || !GetEnableRowReorder(grid))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            e.Effects = e.Data.GetDataPresent(typeof(object)) ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }

        private static void GridOnDrop(object sender, DragEventArgs e)
        {
            if (sender is not DataGrid grid || !GetEnableRowReorder(grid))
            {
                return;
            }

            if (!e.Data.GetDataPresent(typeof(object)))
            {
                return;
            }

            var source = e.Data.GetData(typeof(object));
            var target = GetRowItemFromEvent(e);
            var command = GetReorderCommand(grid);
            var info = new DataGridRowReorderInfo { SourceItem = source, TargetItem = target };

            if (command?.CanExecute(info) == true)
            {
                command.Execute(info);
            }

            SetDragSourceItem(grid, null);
        }

        private static object? GetRowItemFromEvent(RoutedEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject dep)
            {
                return null;
            }

            while (dep != null && dep is not DataGridRow)
            {
                dep = VisualTreeHelper.GetParent(dep);
            }

            return (dep as DataGridRow)?.Item;
        }
    }
}
