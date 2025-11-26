﻿using Astra.Core.Foundation.Common;

namespace Astra.Core.Configuration
{
    /// <summary>
    /// 配置提供者非泛型基接口 - 用于消除反射调用
    /// 提供类型无关的统一访问接口
    /// </summary>
    public interface IConfigProvider
    {
        /// <summary>
        /// 获取所有配置（非泛型版本，返回 IConfig 集合）
        /// </summary>
        Task<OperationResult<IEnumerable<IConfig>>> GetAllConfigsAsync();

        /// <summary>
        /// 保存配置（非泛型版本）
        /// </summary>
        Task<OperationResult> SaveConfigAsync(IConfig config);

        /// <summary>
        /// 检查配置是否存在（非泛型版本）
        /// </summary>
        Task<bool> ExistsAsync(string configId);

        /// <summary>
        /// 删除配置（非泛型版本）
        /// </summary>
        Task<OperationResult> DeleteAsync(string configId);
    }

    /// <summary>
    /// 配置提供者接口 - 负责具体的加载/保存逻辑
    /// </summary>
    public interface IConfigProvider<T> : IConfigProvider where T : class, IConfig  // ← 添加 IConfig 约束
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
        /// 获取所有配置（泛型版本，返回强类型集合）
        /// </summary>
        Task<OperationResult<IEnumerable<T>>> GetAllAsync();

        /// <summary>
        /// 重建索引
        /// </summary>
        Task RebuildIndexAsync();
    }
}
