using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

        /// <summary>
        /// 数据源类型（用于枚举或 IItemsSourceProvider）
        /// </summary>
        public Type ItemsSourceType { get; set; }

        /// <summary>
        /// 路径（用于静态字段/属性）
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// 显示成员路径
        /// </summary>
        public string DisplayMemberPath { get; set; }

        public ItemsSourceAttribute(string propertyName)
        {
            PropertyName = propertyName;
        }

        public ItemsSourceAttribute(Type staticType, string memberName)
        {
            StaticType = staticType;
            // 判断是属性还是方法
            var property = staticType.GetProperty(memberName, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
            if (property != null)
            {
                PropertyName = memberName;
            }
            else
            {
                MethodName = memberName;
            }
        }

        /// <summary>
        /// 使用 ItemsSourceType 和 Path 的构造函数
        /// </summary>
        public ItemsSourceAttribute(Type itemsSourceType, string path, string displayMemberPath = null)
        {
            ItemsSourceType = itemsSourceType;
            Path = path;
            DisplayMemberPath = displayMemberPath;
        }
    }
}
