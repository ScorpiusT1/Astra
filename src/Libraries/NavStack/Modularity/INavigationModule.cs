using Microsoft.Extensions.DependencyInjection;
using NavStack.Configuration;

namespace NavStack.Modularity
{
    /// <summary>
    /// 导航模块接口 - 外部库实现此接口以注册页面
    /// </summary>
    public interface INavigationModule
    {
        /// <summary>
        /// 模块名称
        /// </summary>
        string ModuleName { get; }

        /// <summary>
        /// 注册模块的页面、ViewModel和服务
        /// </summary>
        void RegisterTypes(INavigationConfiguration configuration, IServiceCollection services);
    }

}
