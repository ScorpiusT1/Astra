namespace NavStack.Core
{
    /// <summary>
    /// 确认导航接口 - 可以阻止离开当前页面
    /// </summary>
    public interface IConfirmNavigation
    {
        Task<bool> CanNavigateAsync(NavigationContext context);
    }

  
}
