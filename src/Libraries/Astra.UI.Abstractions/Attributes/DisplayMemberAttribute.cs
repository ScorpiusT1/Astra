using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.UI.Abstractions.Attributes
{
    /// <summary>
    /// 指定显示成员路径（用于复杂对象集合）
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class DisplayMemberAttribute : Attribute
    {
        /// <summary>
        /// 显示成员路径
        /// </summary>
        public string Path { get; }

        public DisplayMemberAttribute(string path)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }
    }
}
