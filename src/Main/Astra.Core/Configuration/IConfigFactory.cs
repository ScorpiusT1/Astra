using Astra.Core.Foundation.Common;

namespace Astra.Core.Configuration
{
    // ==================== 配置工厂接口 ====================

    /// <summary>
    /// 配置工厂接口 - 负责创建配置实例
    /// </summary>
    public interface IConfigFactory<T> where T : class, IConfig
    {
        /// <summary>
        /// 创建默认配置
        /// </summary>
        T CreateDefault(string configId);

        /// <summary>
        /// 从模板创建配置
        /// </summary>
        T CreateFromTemplate(string configId, T template);

        /// <summary>
        /// 克隆配置
        /// </summary>
        T Clone(T source, string newConfigId);

        /// <summary>
        /// 验证配置是否可以创建
        /// </summary>
        OperationResult ValidateCreation(T config);
    }
}
