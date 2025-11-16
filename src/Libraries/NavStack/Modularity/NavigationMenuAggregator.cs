namespace NavStack.Modularity
{
    /// <summary>
    /// 菜单聚合器 - 收集所有模块的菜单项
    /// </summary>
    public class NavigationMenuAggregator
    {
        private readonly IEnumerable<INavigationModule> _modules;

        public NavigationMenuAggregator(IEnumerable<INavigationModule> modules)
        {
            _modules = modules;
        }

        public IEnumerable<NavigationMenuItem> GetAllMenuItems()
        {
            var menuProviders = _modules.OfType<INavigationMenuProvider>();
            var allItems = menuProviders.SelectMany(p => p.GetMenuItems());
            return allItems.OrderBy(m => m.Order);
        }

        public IEnumerable<IGrouping<string, NavigationMenuItem>> GetGroupedMenuItems()
        {
            return GetAllMenuItems()
                .GroupBy(m => m.Group ?? "Default")
                .OrderBy(g => g.Key);
        }
    }
}
