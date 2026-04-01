using Astra.Core.Nodes.Models;
using System;
using System.Windows;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using HandyControl.Controls;

namespace Astra.UI.Windows
{
    /// <summary>
    /// 节点属性编辑窗口，内部嵌入 PropertyEditorControl，用于编辑 Node 的公共属性
    /// </summary>
    public partial class NodePropertyEditorWindow : HandyControl.Controls.Window
    {
        public Node TargetNode { get; }
        private readonly Node _editableNode;

        public NodePropertyEditorWindow(Node node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));

            TargetNode = node;
            // 使用新的节点实例作为编辑目标，并从原节点复制可编辑属性，避免直接修改数据源和 Id 等关键字段
            _editableNode = CreateEditableNode(node);

            InitializeComponent();

            // 将克隆节点交给属性编辑器编辑
            PropertyEditor.SelectedObject = _editableNode;
        }

        /// <summary>
        /// 创建用于编辑的节点副本：
        /// 1. 使用无参构造函数创建同类型新实例
        /// 2. 仅复制带 DisplayAttribute 的属性（跳过 Id/Position/Size 等关键字段）
        /// </summary>
        private static Node CreateEditableNode(Node source)
        {
            var sourceType = source.GetType();
            if (Activator.CreateInstance(sourceType) is not Node editable)
            {
                // 回退：如果无法创建新实例，直接返回原对象（等价于“无保护编辑”）
                return source;
            }

            // 保留原节点 ID，使克隆副本能通过静态注册表获取上游数据源信息
            editable.Id = source.Id;

            var props = sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props)
            {
                if (!prop.CanRead || !prop.CanWrite)
                    continue;

                var displayAttr = prop.GetCustomAttribute<DisplayAttribute>();
                if (displayAttr == null)
                    continue;

                if (prop.Name is nameof(Node.Id) or nameof(Node.Position) or nameof(Node.Size))
                    continue;

                try
                {
                    var value = prop.GetValue(source);
                    prop.SetValue(editable, value);
                }
                catch
                {
                }
            }

            return editable;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // 仅当点击“确定”时，将带有 DisplayAttribute 的属性从编辑副本同步回原始节点
            var targetType = TargetNode.GetType();
            var editableType = _editableNode.GetType();

            if (editableType != targetType && !editableType.IsSubclassOf(targetType))
            {
                // 类型不匹配时，不进行同步
                DialogResult = false;
                Close();
                return;
            }

            var props = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props)
            {
                if (!prop.CanRead || !prop.CanWrite)
                    continue;

                // 只同步带 Display 特性的属性
                var displayAttr = prop.GetCustomAttribute<DisplayAttribute>();
                if (displayAttr == null)
                    continue;

                // 跳过一些不希望在属性编辑器中改动的关键字段
                if (prop.Name is nameof(Node.Id) or nameof(Node.Position) or nameof(Node.Size))
                    continue;

                try
                {
                    var newValue = prop.GetValue(_editableNode);
                    prop.SetValue(TargetNode, newValue);
                }
                catch
                {
                    // 单个属性同步失败忽略，避免阻断其他属性
                }
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // 不同步任何修改，直接关闭
            DialogResult = false;
            Close();
        }

    }
}

