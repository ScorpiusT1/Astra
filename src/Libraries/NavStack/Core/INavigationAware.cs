namespace NavStack.Core
{
    /// <summary>
    /// 导航感知接口 - 页面/ViewModel实现此接口以参与导航生命周期
    /// </summary>
    public interface INavigationAware
    {
        /// <summary>
        /// 导航到此页面之前调用，可以取消导航
        /// </summary>
        Task<bool> OnNavigatingToAsync(NavigationContext context);

        /// <summary>
        /// 导航到此页面后调用
        /// </summary>
        Task OnNavigatedToAsync(NavigationContext context);

        /// <summary>
        /// 离开此页面之前调用，可以取消导航
        /// </summary>
        Task<bool> OnNavigatingFromAsync(NavigationContext context);

        /// <summary>
        /// 离开此页面后调用
        /// </summary>
        Task OnNavigatedFromAsync(NavigationContext context);
    }

}
