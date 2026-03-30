using Astra.Core.Constants;
using Astra.Core.Nodes.Models;

namespace Astra.Core.Data
{
    /// <summary>
    /// 从 <see cref="NodeContext"/> 获取 <see cref="ITestDataBus"/> 的扩展方法。
    /// </summary>
    public static class NodeContextDataBusExtensions
    {
        public static ITestDataBus? GetDataBus(this NodeContext? context)
        {
            return context?.GetMetadata<ITestDataBus>(AstraSharedConstants.MetadataKeys.TestDataBus, null!);
        }

        public static void SetDataBus(this NodeContext context, ITestDataBus dataBus)
        {
            if (context == null) return;
            context.SetMetadata(AstraSharedConstants.MetadataKeys.TestDataBus, dataBus);
        }
    }
}
