using Astra.Core.Devices;
using Astra.Core.Foundation.Common;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Astra.Core.Configuration
{
    /// <summary>
    /// 配置事务服务接口 - 提供批量操作的事务支持
    /// 符合单一职责原则，仅负责事务管理
    /// </summary>
    public interface IConfigurationTransactionService
    {
        /// <summary>
        /// 在事务中执行操作
        /// </summary>
        Task<OperationResult<T>> ExecuteInTransactionAsync<T>(
            Func<Task<OperationResult<T>>> operation,
            bool rollbackOnFailure = true);

        /// <summary>
        /// 批量操作（带事务支持）
        /// </summary>
        Task<OperationResult<BatchOperationResult>> ExecuteBatchAsync<T>(
            IEnumerable<Func<Task<OperationResult>>> operations,
            bool rollbackOnAnyFailure = false) where T : class, IConfig;

        /// <summary>
        /// 获取当前事务ID
        /// </summary>
        string GetCurrentTransactionId();

        /// <summary>
        /// 检查是否在事务中
        /// </summary>
        bool IsInTransaction();
    }

    /// <summary>
    /// 事务操作记录
    /// </summary>
    internal class TransactionOperation
    {
        public string OperationId { get; set; } = Guid.NewGuid().ToString("N");
        public string OperationType { get; set; }
        public object OriginalData { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public Func<Task> RollbackAction { get; set; }
    }

    /// <summary>
    /// 配置事务上下文
    /// </summary>
    internal class TransactionContext : IDisposable
    {
        public string TransactionId { get; } = Guid.NewGuid().ToString("N");
        public DateTime StartTime { get; } = DateTime.Now;
        public List<TransactionOperation> Operations { get; } = new List<TransactionOperation>();
        public bool IsCommitted { get; set; }
        public bool IsRolledBack { get; set; }

        public void Dispose()
        {
            // 清理资源
            Operations.Clear();
        }
    }

    /// <summary>
    /// 配置事务服务实现
    /// 提供配置操作的事务支持，确保批量操作的原子性
    /// </summary>
    public class ConfigurationTransactionService : IConfigurationTransactionService
    {
        [ThreadStatic]
        private static TransactionContext _currentTransaction;

        public string GetCurrentTransactionId()
        {
            return _currentTransaction?.TransactionId;
        }

        public bool IsInTransaction()
        {
            return _currentTransaction != null;
        }

        public async Task<OperationResult<T>> ExecuteInTransactionAsync<T>(
            Func<Task<OperationResult<T>>> operation,
            bool rollbackOnFailure = true)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            // 如果已经在事务中，直接执行
            if (IsInTransaction())
            {
                return await operation();
            }

            // 创建新事务
            using (var transaction = new TransactionContext())
            {
                _currentTransaction = transaction;

                try
                {
                    var result = await operation();

                    if (result.Success)
                    {
                        // 提交事务
                        transaction.IsCommitted = true;
                        return result;
                    }
                    else
                    {
                        // 操作失败
                        if (rollbackOnFailure)
                        {
                            await RollbackAsync(transaction);
                        }
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    // 发生异常，回滚
                    if (rollbackOnFailure)
                    {
                        await RollbackAsync(transaction);
                    }

                    return OperationResult<T>.Failure($"事务执行失败: {ex.Message}");
                }
                finally
                {
                    _currentTransaction = null;
                }
            }
        }

        public async Task<OperationResult<BatchOperationResult>> ExecuteBatchAsync<T>(
            IEnumerable<Func<Task<OperationResult>>> operations,
            bool rollbackOnAnyFailure = false) where T : class, IConfig
        {
            if (operations == null)
                throw new ArgumentNullException(nameof(operations));

            var result = new BatchOperationResult();
            var operationList = new List<Func<Task<OperationResult>>>(operations);

            if (rollbackOnAnyFailure)
            {
                // 严格模式：任何失败都回滚
                using (var transaction = new TransactionContext())
                {
                    _currentTransaction = transaction;

                    try
                    {
                        foreach (var operation in operationList)
                        {
                            var opResult = await operation();
                            if (opResult.Success)
                            {
                                result.SuccessCount++;
                            }
                            else
                            {
                                result.FailureCount++;
                                result.Failures[$"Operation_{result.SuccessCount + result.FailureCount}"] = opResult.Message;

                                // 立即回滚
                                await RollbackAsync(transaction);
                                transaction.IsRolledBack = true;

                                return OperationResult<BatchOperationResult>.Failure(
                                    $"批量操作失败，已回滚所有操作: {opResult.Message}");
                                    
                            }
                        }

                        // 全部成功，提交事务
                        transaction.IsCommitted = true;
                        return OperationResult<BatchOperationResult>.Succeed(result);
                    }
                    catch (Exception ex)
                    {
                        await RollbackAsync(transaction);
                        return OperationResult<BatchOperationResult>.Failure(
                            $"批量操作异常，已回滚: {ex.Message}");
                    }
                    finally
                    {
                        _currentTransaction = null;
                    }
                }
            }
            else
            {
                // 宽松模式：继续执行所有操作
                foreach (var operation in operationList)
                {
                    try
                    {
                        var opResult = await operation();
                        if (opResult.Success)
                        {
                            result.SuccessCount++;
                        }
                        else
                        {
                            result.FailureCount++;
                            result.Failures[$"Operation_{result.SuccessCount + result.FailureCount}"] = opResult.Message;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.FailureCount++;
                        result.Failures[$"Operation_{result.SuccessCount + result.FailureCount}"] = ex.Message;
                    }
                }

                return OperationResult<BatchOperationResult>.Succeed(result);
            }
        }

        /// <summary>
        /// 记录可回滚的操作
        /// </summary>
        internal void RecordOperation(string operationType, object originalData, Func<Task> rollbackAction)
        {
            if (_currentTransaction == null)
                return;

            _currentTransaction.Operations.Add(new TransactionOperation
            {
                OperationType = operationType,
                OriginalData = originalData,
                RollbackAction = rollbackAction
            });
        }

        /// <summary>
        /// 回滚事务
        /// </summary>
        private async Task RollbackAsync(TransactionContext transaction)
        {
            if (transaction.IsRolledBack)
                return;

            // 逆序执行回滚操作
            for (int i = transaction.Operations.Count - 1; i >= 0; i--)
            {
                var operation = transaction.Operations[i];
                try
                {
                    if (operation.RollbackAction != null)
                    {
                        await operation.RollbackAction();
                    }
                }
                catch (Exception ex)
                {
                    // 回滚失败，记录但继续
                    Console.Error.WriteLine($"回滚操作失败: {ex.Message}");
                }
            }

            transaction.IsRolledBack = true;
        }
    }
}
