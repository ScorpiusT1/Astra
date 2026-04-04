using Astra.Core.Nodes.Models;

namespace Astra.UI.Abstractions.Nodes
{
    /// <summary>
    /// 节点自定义属性编辑器接口。
    /// <para>
    /// 由插件中的 UserControl（自定义属性编辑视图）可选择实现。
    /// 宿主在点击「确定」后先执行针对 <c>[Display]</c> 属性的通用反射同步，
    /// 再调用 <see cref="Apply"/> 写回自定义页中特有的字段（如 <c>Parameters</c> 字典、
    /// 文件路径列表、预览缓存等在通用同步逻辑之外的数据）。
    /// </para>
    /// <para>
    /// 若自定义编辑视图的所有持久化字段均已通过 <c>[Display]</c> 属性暴露，
    /// 则无需实现本接口，宿主会自动完成同步。
    /// </para>
    /// <example>
    /// <code>
    /// public partial class DataImportNodePropertyView : UserControl, INodePropertyEditor
    /// {
    ///     public void Apply(Node target)
    ///     {
    ///         if (target is DataImportNode node)
    ///         {
    ///             node.Parameters["FilePath"]  = ViewModel.FilePath;
    ///             node.Parameters["ColumnMap"] = ViewModel.ColumnMap;
    ///         }
    ///     }
    /// }
    /// </code>
    /// </example>
    /// </summary>
    public interface INodePropertyEditor
    {
        /// <summary>
        /// 将界面中的编辑结果写回画布上的原始目标节点。
        /// 宿主在属性编辑窗口点击「确定」时调用，在通用 <c>[Display]</c> 反射同步之后执行。
        /// </summary>
        /// <param name="target">
        /// 画布上的原始节点实例（非编辑副本），直接修改此对象的属性或 Parameters。
        /// </param>
        void Apply(Node target);
    }
}
