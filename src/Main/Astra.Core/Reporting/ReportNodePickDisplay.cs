using Astra.Core.Nodes.Models;

namespace Astra.Core.Reporting
{
    /// <summary>
    /// 结果归档白名单下拉中的可读标签（不展示命名空间或完整 CLR 类型名）。
    /// </summary>
    public static class ReportNodePickDisplay
    {
        /// <summary>优先节点 <see cref="Node.Name"/>；空则使用类型键的最后一段（去掉命名空间）；再无则「未命名」。</summary>
        public static string FormatNodeLabel(Node? n)
        {
            if (n == null)
                return string.Empty;
            if (!string.IsNullOrWhiteSpace(n.Name))
                return n.Name.Trim();
            var shortType = ShortTypeSegment(n.NodeType);
            return string.IsNullOrEmpty(shortType) ? "未命名" : shortType;
        }

        /// <summary>取类型标识的最后一段（如 <c>A.B.MyNode</c> → <c>MyNode</c>）。</summary>
        public static string ShortTypeSegment(string? nodeType)
        {
            if (string.IsNullOrWhiteSpace(nodeType))
                return string.Empty;
            var t = nodeType.Trim();
            var i = t.LastIndexOf('.');
            return i >= 0 && i < t.Length - 1 ? t[(i + 1)..].Trim() : t;
        }
    }
}
