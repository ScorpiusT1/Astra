namespace NavStack.Core
{
    /// <summary>
    /// 窗口导航服务
    /// </summary>
    public interface IWindowNavigationService
    {
        Task<bool> ShowDialogAsync(string windowKey, NavigationParameters parameters = null);
        Task ShowWindowAsync(string windowKey, NavigationParameters parameters = null);
        void CloseWindow(string windowKey);
    }

}
