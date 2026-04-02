namespace Astra.Core.Nodes.Models
{
    /// <summary>
    /// 任意节点向 <see cref="ExecutionResult.OutputData"/> 写入单值（标量）时的推荐键格式；
    /// 算法、数据处理、脚本节点等均可复用，与 UI 层 <c>NodeUiOutputKeys</c> 中的常量保持一致。
    /// </summary>
    public static class NodeScalarOutputContracts
    {
        public const string KeyPrefix = "Scalar.";

        public static string FormatKey(string logicalName) => KeyPrefix + logicalName;
    }
}
