using Astra.Core.Plugins.Models;

namespace Astra.Core.Plugins.Security
{
    /// <summary>
    /// AppDomain 沙箱实现
    /// 注意：.NET Core/.NET 5+ 不支持 AppDomain 安全性
    /// 这里提供接口定义和基本实现框架
    /// </summary>
    public interface ISandbox
    {
        void Execute(Action action, PluginPermissions permissions);
        T Execute<T>(Func<T> func, PluginPermissions permissions);
    }
}
