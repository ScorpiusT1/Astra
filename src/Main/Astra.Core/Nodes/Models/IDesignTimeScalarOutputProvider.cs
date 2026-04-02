using System.Collections.Generic;

namespace Astra.Core.Nodes.Models
{
    /// <summary>
    /// 设计期：任意上游节点可枚举其将写入 <see cref="NodeScalarOutputContracts"/> 标量键，
    /// 供下游（如值卡控）在连线后刷新下拉；运行时对应 <see cref="NodeContext.InputData"/> 的限定键 <c>上游节点Id:Scalar.xxx</c>。
    /// 运行时请在节点结果中使用 <c>WithOutput(NodeScalarOutputContracts.FormatKey(逻辑名), 数值)</c>（或 UI 层的 <c>NodeUiOutputKeys.FormatScalarOutputKey</c>）。
    /// </summary>
    public interface IDesignTimeScalarOutputProvider
    {
        /// <summary>提供者节点 Id（与 InputData 中键的限定前缀一致）。</summary>
        string ProviderNodeId { get; }

        /// <summary>枚举设计期可选择的标量输入键（通常为 <c>节点Id:Scalar.xxx</c>）。</summary>
        IEnumerable<string> EnumerateDesignTimeScalarInputKeys();
    }
}
