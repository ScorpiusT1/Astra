namespace Astra.Core.Plugins.Abstractions
{
    /// <summary>
    /// 依赖解析器接口
    /// </summary>
    public interface IDependencyResolver
    {
        /// <summary>
        /// 解析服务实例
        /// </summary>
        object Resolve(Type serviceType);

        /// <summary>
        /// 解析服务实例（泛型）
        /// </summary>
        T Resolve<T>() where T : class;

        /// <summary>
        /// 尝试解析服务
        /// </summary>
        bool TryResolve<T>(out T service) where T : class;

        /// <summary>
        /// 解析所有服务实例
        /// </summary>
        IEnumerable<object> ResolveAll(Type serviceType);

        /// <summary>
        /// 检查服务是否已注册
        /// </summary>
        bool IsRegistered(Type serviceType);
    }
}
