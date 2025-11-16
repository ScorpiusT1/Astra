namespace NavStack.Core
{
    #region 导航事件参数

    public class NavigationFailedEventArgs : EventArgs
    {
        public NavigationContext Context { get; set; }
        public Exception Exception { get; set; }
    }

    #endregion
}
