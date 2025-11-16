namespace NavStack.Core
{
    /// <summary>
    /// 核心导航服务接口
    /// </summary>
    public interface INavigationService
    {
        /// <summary>
        /// 导航到指定页面
        /// </summary>
        Task<NavigationResult> NavigateAsync(string pageKey, NavigationParameters parameters = null);

        /// <summary>
        /// 后退
        /// </summary>
        Task<NavigationResult> GoBackAsync(NavigationParameters parameters = null);

        /// <summary>
        /// 前进
        /// </summary>
        Task<NavigationResult> GoForwardAsync(NavigationParameters parameters = null);

        /// <summary>
        /// 能否后退
        /// </summary>
        bool CanGoBack { get; }

        /// <summary>
        /// 能否前进
        /// </summary>
        bool CanGoForward { get; }

        /// <summary>
        /// 清空导航历史
        /// </summary>
        void ClearHistory();

        /// <summary>
        /// 获取导航历史
        /// </summary>
        IEnumerable<string> GetNavigationHistory();

        /// <summary>
        /// 导航事件
        /// </summary>
        event EventHandler<NavigationEventArgs> Navigating;
        event EventHandler<NavigationEventArgs> Navigated;
        event EventHandler<NavigationFailedEventArgs> NavigationFailed;
    }
}
