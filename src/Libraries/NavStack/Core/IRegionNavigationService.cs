namespace NavStack.Core
{
    /// <summary>
    /// 区域导航服务
    /// </summary>
    public interface IRegionNavigationService : INavigationService
    {
        string RegionName { get; }
    }

}
