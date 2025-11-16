using NavStack.Core;
using System.Windows.Controls;

namespace NavStack.Regions
{
    /// <summary>
    /// 区域管理器
    /// </summary>
    public interface IRegionManager
    {
        void RegisterRegion(string regionName, ContentControl control);
        IRegionNavigationService GetNavigationService(string regionName);
        void UnregisterRegion(string regionName);
    }
}
