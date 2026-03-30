using System.Collections.Generic;

namespace Astra.UI.Abstractions.Nodes;

/// <summary>
/// 从 <c>ExecutionResult.OutputData</c> 提取 UI 相关子集，供节点完成事件与主页绑定。
/// </summary>
public static class NodeUiPayloadFactory
{
    public static IReadOnlyDictionary<string, object>? FromOutputData(IDictionary<string, object>? outputData)
    {
        if (outputData == null || outputData.Count == 0)
        {
            return null;
        }

        var dict = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var key in NodeUiOutputKeys.All)
        {
            if (outputData.TryGetValue(key, out var value) && value != null)
            {
                dict[key] = value;
            }
        }

        return dict.Count == 0 ? null : dict;
    }
}
