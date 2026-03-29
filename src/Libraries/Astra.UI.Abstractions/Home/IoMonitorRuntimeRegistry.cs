using System;

namespace Astra.UI.Abstractions.Home
{
    /// <summary>
    /// 宿主不引用具体插件程序集时，由 PLC 插件在初始化/释放时挂接首页 IO 监控实现。
    /// </summary>
    public static class IoMonitorRuntimeRegistry
    {
        private static IHomeIoMonitorRuntime? _instance;

        /// <summary>
        /// 在 <see cref="Register"/> 调用后触发；用于首页模块在插件晚于界面构造完成时再执行 <see cref="IHomeIoMonitorRuntime.Attach"/>。
        /// </summary>
        public static event EventHandler? RuntimeRegistered;

        public static void Register(IHomeIoMonitorRuntime? runtime)
        {
            _instance = runtime;
            RuntimeRegistered?.Invoke(null, EventArgs.Empty);
        }

        public static IHomeIoMonitorRuntime? TryGet()
        {
            return _instance;
        }
    }
}
