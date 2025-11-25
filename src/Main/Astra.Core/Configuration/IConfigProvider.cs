using Astra.Core.Foundation.Common;

namespace Astra.Core.Configuration
{
    /// <summary>
    /// 配置提供者接口 - 负责具体的加载/保存逻辑
    /// </summary>
    public interface IConfigProvider<T> where T : class, IConfig  // ← 添加 IConfig 约束
    {
        /// <summary>
        /// 加载配置
        /// </summary>
        Task<OperationResult<T>> LoadAsync(string configId);

        /// <summary>
        /// 保存配置
        /// </summary>
        Task<OperationResult> SaveAsync(T config);

        /// <summary>
        /// 删除配置
        /// </summary>
        Task<OperationResult> DeleteAsync(string configId);

        /// <summary>
        /// 检查配置是否存在
        /// </summary>
        Task<bool> ExistsAsync(string configId);

        /// <summary>
        /// 获取所有配置
        /// </summary>
        Task<OperationResult<IEnumerable<T>>> GetAllAsync();

        /// <summary>
        /// 重建索引
        /// </summary>
        Task RebuildIndexAsync();
    }
}
