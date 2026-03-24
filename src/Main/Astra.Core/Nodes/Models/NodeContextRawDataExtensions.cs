using System;

namespace Astra.Core.Nodes.Models
{
    /// <summary>
    /// 节点上下文原始数据访问扩展方法。
    /// </summary>
    public static class NodeContextRawDataExtensions
    {
        private const string RawDataStoreMetadataKey = "RawDataStore";

        public static void SetRawDataStore(this NodeContext context, IRawDataStore rawDataStore)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.SetMetadata(RawDataStoreMetadataKey, rawDataStore);
        }

        public static IRawDataStore GetRawDataStore(this NodeContext context)
        {
            if (context == null) return null;
            return context.GetMetadata<IRawDataStore>(RawDataStoreMetadataKey, null);
        }
    }
}
