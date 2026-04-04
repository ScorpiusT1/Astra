using System;

namespace Astra.UI.Abstractions.Nodes
{
    /// <summary>
    /// 标记节点类使用自定义属性编辑界面。
    /// <para>
    /// 当宿主双击节点打开属性编辑窗口时，若节点类型（或其基类）上存在此特性，
    /// 则用 <see cref="ViewType"/> 指定的 UserControl 替代默认的通用属性网格。
    /// </para>
    /// <para>
    /// <see cref="ViewType"/> 必须满足：
    /// <list type="number">
    ///   <item>具有无参构造函数；</item>
    ///   <item>是 <c>System.Windows.FrameworkElement</c> 的派生类（如 UserControl）；</item>
    ///   <item>建议实现 <see cref="INodePropertyEditor"/> 接口，以便在「确定」时将特殊字段写回节点。</item>
    /// </list>
    /// 宿主会在创建视图后将「可编辑副本节点」赋给视图的 <c>DataContext</c>，
    /// 插件界面可直接通过 <c>DataContext</c> 获取节点实例进行绑定与预览。
    /// </para>
    /// <example>
    /// <code>
    /// [NodePropertyEditor(typeof(DataImportNodePropertyView))]
    /// public class DataImportNode : Node { ... }
    /// </code>
    /// </example>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class NodePropertyEditorAttribute : Attribute
    {
        /// <summary>
        /// 自定义属性编辑器的视图类型（UserControl 或其他 FrameworkElement 派生类）。
        /// </summary>
        public Type ViewType { get; }

        /// <param name="viewType">
        /// 自定义编辑器的视图类型，须有无参构造函数且为 FrameworkElement 派生类。
        /// </param>
        public NodePropertyEditorAttribute(Type viewType)
        {
            ViewType = viewType ?? throw new ArgumentNullException(nameof(viewType));
        }
    }
}
