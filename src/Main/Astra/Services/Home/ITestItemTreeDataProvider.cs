using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Astra.ViewModels.HomeModules;

namespace Astra.Services.Home
{
    /// <summary>
    /// 为测试项树提供异步数据加载，便于替换为流程引擎、数据库或远程接口实现。
    /// </summary>
    public interface ITestItemTreeDataProvider
    {
        Task<IReadOnlyList<TestTreeNodeItem>> LoadRootNodesAsync(CancellationToken cancellationToken = default);
    }
}
