namespace Astra.UI.Abstractions.Interfaces
{
    /// <summary>
    /// 属性可见性提供器接口
    /// 允许对象动态控制属性的可见性
    /// </summary>
    public interface IPropertyVisibilityProvider
    {
        /// <summary>
        /// 检查指定属性是否可见
        /// </summary>
        /// <param name="propertyName">属性名称</param>
        /// <returns>如果属性可见返回 true，否则返回 false</returns>
        bool IsPropertyVisible(string propertyName);
    }

}
