using System;

namespace Astra.Core.Nodes.Models
{
    /// <summary>
    /// 节点上下文原始数据访问扩展方法。
    /// </summary>
    public static class NodeContextRawDataExtensions
    {
        public static void SetRawDataStore(this NodeContext context, IRawDataStore rawDataStore)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.SetMetadata(ExecutionContextMetadataKeys.RawDataStore, rawDataStore);
        }

        public static IRawDataStore GetRawDataStore(this NodeContext context)
        {
            if (context == null) return null;
            return context.GetMetadata<IRawDataStore>(ExecutionContextMetadataKeys.RawDataStore, null);
        }
    }
}
