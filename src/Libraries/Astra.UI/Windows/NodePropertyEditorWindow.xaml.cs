using Astra.Core.Nodes.Models;
using Astra.UI.Abstractions.Nodes;
using Astra.UI.Controls;
using System;
using System.Collections.Generic;
using System.Windows;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using HandyControl.Controls;

namespace Astra.UI.Windows
{
    /// <summary>
    /// 节点属性编辑窗口。
    /// <para>
    /// 若节点类型标注了 <see cref="NodePropertyEditorAttribute"/>，则在中间区域显示插件提供的
    /// 自定义视图（UserControl）；否则回退到通用属性网格（PropertyEditorControl）。
    /// </para>
    /// <para>点击「确定」时：
    /// <list type="number">
    ///   <item>始终执行通用 <c>[Display]</c> 反射同步（处理 Node 基类公共属性）；</item>
    ///   <item>若自定义视图实现了 <see cref="INodePropertyEditor"/>，再调用 <c>Apply</c> 写回特殊字段。</item>
    /// </list>
    /// </para>
    /// </summary>
    public partial class NodePropertyEditorWindow : HandyControl.Controls.Window
    {
        public Node TargetNode { get; }
        private readonly Node _editableNode;

        /// <summary>当前是否使用插件提供的自定义编辑器。</summary>
        private readonly bool _hasCustomEditor;

        /// <summary>自定义编辑器视图（仅 <see cref="_hasCustomEditor"/> 为 true 时有效）。</summary>
        private readonly FrameworkElement? _customEditorView;

        public NodePropertyEditorWindow(Node node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));

            TargetNode = node;
            // 创建可编辑副本，避免直接修改画布上的节点实例
            _editableNode = CreateEditableNode(node);

            InitializeComponent();

            // 检查节点类型上是否标注了自定义属性编辑器特性
            var editorAttr = node.GetType().GetCustomAttribute<NodePropertyEditorAttribute>();
            if (editorAttr?.ViewType != null)
            {
                _customEditorView = TryCreateCustomEditorView(editorAttr.ViewType, _editableNode);
                _hasCustomEditor = _customEditorView != null;
            }

            if (_hasCustomEditor && _customEditorView != null)
            {
                // 使用插件提供的自定义编辑视图。
                // 垂直方向若用 Stretch，ScrollViewer 会把子级高度钳在视口内，总高度永远等于可视区，
                // 外层纵向滚动条永不出现；Top 让高度按内容 DesiredSize 计算，超出才可滚动。
                //EditorContentHost.VerticalAlignment = VerticalAlignment.Top;
                //EditorContentHost.VerticalContentAlignment = VerticalAlignment.Top;
                EditorContentHost.Content = _customEditorView;
            }
            else
            {
                // 回退：使用默认通用属性网格（内部带 * 行 + ScrollViewer，需 Stretch 才能获得视口高度）
                EditorContentHost.VerticalAlignment = VerticalAlignment.Stretch;
                EditorContentHost.VerticalContentAlignment = VerticalAlignment.Stretch;
                var propertyEditor = new PropertyEditorControl
                {
                    ShowSearchBox = false,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                propertyEditor.SelectedObject = _editableNode;
                EditorContentHost.Content = propertyEditor;
            }
        }

        /// <summary>
        /// 通过反射实例化插件提供的自定义编辑视图，并将可编辑副本节点设置为其 DataContext。
        /// 若实例化失败（如缺少无参构造函数、类型非 FrameworkElement）则返回 null，触发默认回退。
        /// </summary>
        private static FrameworkElement? TryCreateCustomEditorView(Type viewType, Node editableNode)
        {
            try
            {
                if (Activator.CreateInstance(viewType) is not FrameworkElement view)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[NodePropertyEditorWindow] 自定义编辑器类型 {viewType.FullName} 不是 FrameworkElement 的派生类，已回退到默认编辑器。");
                    return null;
                }

                view.DataContext = editableNode;
                return view;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[NodePropertyEditorWindow] 创建自定义编辑器 {viewType.FullName} 失败：{ex.Message}，已回退到默认编辑器。");
                return null;
            }
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
                // 回退：如果无法创建新实例，直接返回原对象（等价于"无保护编辑"）
                return source;
            }

            // 保留原节点 ID，使克隆副本能通过静态注册表获取上游数据源信息
            editable.Id = source.Id;
            editable.ContainingWorkflow = source.ContainingWorkflow;

            CopyDisplayMarkedProperties(source, editable);

            return editable;
        }

        /// <summary>
        /// 沿继承链（派生类 → 基类直至 <see cref="Node"/>）按 <see cref="BindingFlags.DeclaredOnly"/> 收集带
        /// <see cref="DisplayAttribute"/> 的可读写属性；派生声明同名属性时优先于基类。
        /// 避免仅用 <see cref="Type.GetProperties()"/> 扁平枚举时漏掉基类「纳入测试报告」等字段，导致编辑副本退化为默认值 true。
        /// </summary>
        private static IEnumerable<PropertyInfo> EnumerateDisplayMarkedProperties(Type concreteNodeType)
        {
            if (!typeof(Node).IsAssignableFrom(concreteNodeType))
                yield break;

            var types = new List<Type>();
            for (var t = concreteNodeType; t != null && typeof(Node).IsAssignableFrom(t); t = t.BaseType)
                types.Add(t);

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var t in types)
            {
                foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (!prop.CanRead || !prop.CanWrite)
                        continue;
                    if (prop.GetCustomAttribute<DisplayAttribute>() == null)
                        continue;
                    if (prop.Name is nameof(Node.Id) or nameof(Node.Position) or nameof(Node.Size))
                        continue;
                    // 派生类已用 [Display] 声明同名属性时优先，避免仅占名无特性的属性挡住基类字段
                    if (!seen.Add(prop.Name))
                        continue;
                    yield return prop;
                }
            }
        }

        private static void CopyDisplayMarkedProperties(Node from, Node to)
        {
            foreach (var prop in EnumerateDisplayMarkedProperties(from.GetType()))
            {
                try
                {
                    prop.SetValue(to, prop.GetValue(from));
                }
                catch
                {
                }
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. 始终执行通用 [Display] 反射同步（处理 IsEnabled / ContinueOnFailure 等 Node 基类属性）
            SyncDisplayPropertiesToTarget();

            // 2. 若自定义编辑视图实现了 INodePropertyEditor，再调用 Apply 写回特殊字段
            //    （如 Parameters 字典、文件路径列表等未通过 [Display] 暴露的持久化数据）
            if (_hasCustomEditor && _customEditorView is INodePropertyEditor customEditor)
            {
                try
                {
                    customEditor.Apply(TargetNode);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[NodePropertyEditorWindow] INodePropertyEditor.Apply 执行失败：{ex.Message}");
                }
            }

            DialogResult = true;
            Close();
        }

        /// <summary>
        /// 将带有 <see cref="DisplayAttribute"/> 的属性从可编辑副本同步回原始目标节点。
        /// </summary>
        private void SyncDisplayPropertiesToTarget()
        {
            var targetType = TargetNode.GetType();
            var editableType = _editableNode.GetType();

            if (editableType != targetType && !editableType.IsSubclassOf(targetType))
                return;

            foreach (var prop in EnumerateDisplayMarkedProperties(targetType))
            {
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
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
