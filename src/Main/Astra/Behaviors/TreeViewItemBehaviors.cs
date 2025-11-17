using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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

        public static ICommand GetSelectedCommand(DependencyObject obj) =>
            (ICommand)obj.GetValue(SelectedCommandProperty);

        public static void SetSelectedCommand(DependencyObject obj, ICommand value) =>
            obj.SetValue(SelectedCommandProperty, value);

        public static object GetSelectedCommandParameter(DependencyObject obj) =>
            obj.GetValue(SelectedCommandParameterProperty);

        public static void SetSelectedCommandParameter(DependencyObject obj, object value) =>
            obj.SetValue(SelectedCommandParameterProperty, value);

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
