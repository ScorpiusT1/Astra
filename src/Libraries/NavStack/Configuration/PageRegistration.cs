using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NavStack.Configuration
{
    /// <summary>
    /// 页面注册信息
    /// </summary>
    public class PageRegistration
    {
        public string Key { get; set; }
        public Type ViewType { get; set; }
        public Type ViewModelType { get; set; }
        public bool IsSingleton { get; set; }
    }
}
