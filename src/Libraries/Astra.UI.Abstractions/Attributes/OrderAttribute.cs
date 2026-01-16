using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.UI.Abstractions.Attributes
{
    /// <summary>
    /// 排序特性，用于控制分组排序和组内属性排序
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class OrderAttribute : Attribute
    {
        /// <summary>
        /// 分组排序顺序（用于控制分组的显示顺序）
        /// 值越小，分组越靠前
        /// </summary>
        public int? GroupOrder { get; set; }

        /// <summary>
        /// 属性排序顺序（用于控制同一分组内属性的显示顺序）
        /// 值越小，属性越靠前
        /// </summary>
        public int? PropertyOrder { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="propertyOrder">属性排序顺序</param>
        public OrderAttribute(int propertyOrder)
        {
            PropertyOrder = propertyOrder;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="groupOrder">分组排序顺序</param>
        /// <param name="propertyOrder">属性排序顺序</param>
        public OrderAttribute(int groupOrder, int propertyOrder)
        {
            GroupOrder = groupOrder;
            PropertyOrder = propertyOrder;
        }
    }
}
