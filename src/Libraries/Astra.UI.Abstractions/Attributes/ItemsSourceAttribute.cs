using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.UI.Abstractions.Attributes
{
    /// <summary>
    /// 指定属性的可选项来源
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ItemsSourceAttribute : Attribute
    {
        /// <summary>
        /// 数据源属性名称
        /// </summary>
        public string PropertyName { get; }

        /// <summary>
        /// 数据源方法名称
        /// </summary>
        public string MethodName { get; }

        /// <summary>
        /// 静态类型（用于静态属性/方法）
        /// </summary>
        public Type StaticType { get; }

        public ItemsSourceAttribute(string propertyName)
        {
            PropertyName = propertyName;
        }

        public ItemsSourceAttribute(Type staticType, string memberName)
        {
            StaticType = staticType;
            PropertyName = memberName;
        }
    }
}
