namespace NavStack.Modularity
{
    #region 模块生命周期接口

    /// <summary>
    /// 模块生命周期接口 - 提供初始化和清理钩子
    /// </summary>
    public interface INavigationModuleLifecycle
    {
        /// <summary>
        /// 模块初始化完成后调用
        /// </summary>
        void OnInitialized(IServiceProvider serviceProvider);

        /// <summary>
        /// 应用程序关闭时调用
        /// </summary>
        void OnShutdown();
    }

    #endregion
}
