namespace Astra.UI.Abstractions.Interfaces
{
    /// <summary>
    /// 属性可见性提供器接口：属性编辑器在加载属性列表后调用 <see cref="IsPropertyVisible"/>，
    /// 以动态设置各属性的是否可浏览。
    /// 典型用法：对不产生测试报告数据的节点隐藏 <c>IncludeInTestReport</c>。
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
