using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.UI.Abstractions.Interfaces
{
    /// <summary>
    /// 自定义属性提供器接口
    /// </summary>
    public interface IPropertyProvider
    {
        IEnumerable<Models.PropertyDescriptor> GetProperties(object target);
    }

}
