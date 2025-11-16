namespace NavStack.Modularity
{
    /// <summary>
    /// 菜单提供接口 - 模块可选实现此接口以提供菜单
    /// </summary>
    public interface INavigationMenuProvider
    {
        IEnumerable<NavigationMenuItem> GetMenuItems();
    }
}
