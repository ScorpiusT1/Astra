using Microsoft.Extensions.DependencyInjection;

namespace Astra.Core.Abstractions
{
    /// <summary>
    /// 模块注册器接口 - 用于模块化服务注册
    /// </summary>
    public interface IModuleRegistrar
    {
        /// <summary>
        /// 模块名称
        /// </summary>
        string ModuleName { get; }

        /// <summary>
        /// 注册模块服务
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <returns>服务集合，支持链式调用</returns>
        IServiceCollection RegisterServices(IServiceCollection services);
    }
}

