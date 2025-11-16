using System.Windows.Controls;

namespace NavStack.Core
{
    /// <summary>
    /// Frame导航服务
    /// </summary>
    public interface IFrameNavigationService : INavigationService
    {
        Frame Frame { get; set; }
    }
}
