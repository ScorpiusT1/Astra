using System.Collections;

namespace Astra.UI.Abstractions.Interfaces
{
    /// <summary>
    /// 数据源提供者接口
    /// 用于 ItemsSourceAttribute 中指定数据源提供者类型
    /// </summary>
    public interface IItemsSourceProvider
    {
        /// <summary>
        /// 获取数据源
        /// </summary>
        IEnumerable GetItemsSource();
    }
}

