namespace NavStack.Core
{
    #region 导航服务接口

    #endregion

    #region 导航事件参数

    public class NavigationEventArgs : EventArgs
    {
        public NavigationContext Context { get; set; }
        public object Content { get; set; }
        public bool Cancel { get; set; }
    }

    #endregion
}
