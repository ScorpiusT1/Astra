using Astra.Core.Plugins.Models;

namespace Astra.Core.Plugins.Security
{
    /// <summary>
    /// 进程级沙箱（.NET Core 推荐方案）
    /// </summary>
    public class ProcessSandbox : ISandbox
    {
        public void Execute(Action action, PluginPermissions permissions)
        {
            // 使用独立进程执行不受信任的代码
            // 可以配合 Docker 容器或 Windows Sandbox
            throw new NotImplementedException("Process-level sandbox requires separate process implementation");
        }

        public T Execute<T>(Func<T> func, PluginPermissions permissions)
        {
            throw new NotImplementedException("Process-level sandbox requires separate process implementation");
        }
    }
}
