using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Astra.UI.Controls
{
    /// <summary>
    /// 文件选择行控件：[TextBox] [浏览按钮(文件夹图标,PrimaryButton)] [清空按钮(删除图标)]，三者在同一行。
    /// 通过 <see cref="MeasureOverride"/> 和动态 MaxWidth 双重保证 TextBox 不会因内容过长而撑开布局。
    /// </summary>
    public class FilePickerBox : DockPanel
    {
        private readonly TextBox _textBox;
        private readonly Button _browseButton;
        private readonly Button _clearButton;

        public event EventHandler BrowseClick;
        public event EventHandler ClearClick;

        public TextBox TextBox => _textBox;

        public string Text
        {
            get => _textBox.Text;
            set => _textBox.Text = value;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            var w = constraint.Width;
            if (double.IsInfinity(w) || double.IsNaN(w))
                w = ActualWidth > 0 ? ActualWidth : 400;

            var capped = new Size(w, constraint.Height);

            _browseButton.Measure(capped);
            _clearButton.Measure(capped);

            var buttonsWidth = _browseButton.DesiredSize.Width + _clearButton.DesiredSize.Width;
            var textBoxWidth = Math.Max(0, w - buttonsWidth);
            _textBox.MaxWidth = textBoxWidth;

            var desired = base.MeasureOverride(capped);
            desired.Width = Math.Min(desired.Width, w);
            return desired;
        }

        public FilePickerBox()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch;
            LastChildFill = true;

            _clearButton = CreateClearButton();
            SetDock(_clearButton, Dock.Right);
            _clearButton.Click += (_, _) => ClearClick?.Invoke(this, EventArgs.Empty);
            Children.Add(_clearButton);

            _browseButton = CreateBrowseButton();
            SetDock(_browseButton, Dock.Right);
            _browseButton.Click += (_, _) => BrowseClick?.Invoke(this, EventArgs.Empty);
            Children.Add(_browseButton);

            _textBox = new TextBox
            {
                MinHeight = 36,
                FontSize = 13,
                Padding = new Thickness(10, 6, 6, 6),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
            };
            _textBox.SetResourceReference(StyleProperty, "CompactTextBoxStyle");
            Children.Add(_textBox);
        }

        private static Button CreateBrowseButton()
        {
            var icon = new System.Windows.Shapes.Path
            {
                Width = 16,
                Height = 16,
                Stretch = Stretch.Uniform,
                StrokeThickness = 1.6,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
            };
            icon.SetResourceReference(System.Windows.Shapes.Path.DataProperty, "FolderGeometry");
            icon.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "ReverseTextBrush");

            var btn = new Button
            {
                Content = icon,
                Width = 36,
                MinHeight = 36,
                Padding = new Thickness(0),
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "浏览文件...",
            };
            btn.SetResourceReference(StyleProperty, "PrimaryButtonStyle");

            return btn;
        }

        private static Button CreateClearButton()
        {
            var icon = new System.Windows.Shapes.Path
            {
                Width = 12,
                Height = 12,
                Stretch = Stretch.Uniform,
                StrokeThickness = 1.4,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
            };
            icon.SetResourceReference(System.Windows.Shapes.Path.DataProperty, "DeleteGeometry");
            icon.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "DangerBrush");

            var btn = new Button
            {
                Content = icon,
                Width = 32,
                MinHeight = 36,
                Padding = new Thickness(0),
                Margin = new Thickness(2, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "清空",
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
            };

            return btn;
        }
    }
}
