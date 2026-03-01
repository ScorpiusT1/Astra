using Astra.Core.Foundation.Common;

namespace Astra.Core.Configuration
{
    /// <summary>
    /// 配置事务服务接口
    /// </summary>
    public interface IConfigurationTransactionService
    {
        Task<OperationResult> BeginTransactionAsync();
        Task<OperationResult> CommitTransactionAsync();
        Task<OperationResult> RollbackTransactionAsync();
    }
}

