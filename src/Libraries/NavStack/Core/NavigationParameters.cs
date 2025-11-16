using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NavStack.Core
{
    #region 导航参数和结果

    /// <summary>
    /// 导航参数容器
    /// </summary>
    public class NavigationParameters : Dictionary<string, object>
    {
        public T GetValue<T>(string key, T defaultValue = default)
        {
            if (TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }

        public void Add(string key, object value)
        {
            this[key] = value;
        }
    }

    #endregion
}
