namespace NavStack.Core
{
    /// <summary>
    /// 导航上下文
    /// </summary>
    public class NavigationContext
    {
        public string NavigationUri { get; set; }
        public NavigationParameters Parameters { get; set; }
        public NavigationMode NavigationMode { get; set; }
        public object NavigationSource { get; set; }
    }

}
