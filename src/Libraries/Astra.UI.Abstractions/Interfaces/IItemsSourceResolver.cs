using Astra.UI.Abstractions.Attributes;
using System.Collections;

namespace Astra.UI.Abstractions.Interfaces
{
    /// <summary>
    /// ItemsSource 解析器抽象：用于从特性和目标对象解析下拉数据源。
    /// </summary>
    public interface IItemsSourceResolver
    {
        bool TryResolve(ItemsSourceAttribute attribute, object targetObject, out IEnumerable itemsSource);
    }
}
