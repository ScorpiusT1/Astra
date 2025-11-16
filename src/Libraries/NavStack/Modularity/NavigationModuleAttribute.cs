namespace NavStack.Modularity
{
    #region 自动发现功能

    /// <summary>
    /// 导航模块特性 - 用于标记模块类
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class NavigationModuleAttribute : Attribute
    {
        public string ModuleName { get; set; }
        public int Priority { get; set; } = 0;
        public string Description { get; set; }
    }

    #endregion

}
