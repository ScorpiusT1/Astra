using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Utilities
{
    /// <summary>
    /// 程序集信息辅助类
    /// </summary>
    public static class AssemblyInfo
    {
        private static readonly Assembly _assembly = Assembly.GetExecutingAssembly();

        /// <summary>
        /// 产品名称
        /// </summary>
        public static string ProductName
        {
            get
            {
                var attr = _assembly.GetCustomAttribute<AssemblyProductAttribute>();
                return attr?.Product ?? "Astra";
            }
        }

        /// <summary>
        /// 公司名称
        /// </summary>
        public static string CompanyName
        {
            get
            {
                var attr = _assembly.GetCustomAttribute<AssemblyCompanyAttribute>();
                return attr?.Company ?? "Your Company";
            }
        }

        /// <summary>
        /// 版本号
        /// </summary>
        public static string Version
        {
            get
            {
                var version = _assembly.GetName().Version;
                return $"v{version.Major}.{version.Minor}.{version.Build}";
            }
        }

        /// <summary>
        /// 版权信息
        /// </summary>
        public static string Copyright
        {
            get
            {
                var attr = _assembly.GetCustomAttribute<AssemblyCopyrightAttribute>();
                return attr?.Copyright ?? $"© {DateTime.Now.Year} {CompanyName}. All rights reserved.";
            }
        }

        /// <summary>
        /// 生成完整版权信息
        /// </summary>
        public static string GetFullCopyright()
        {
            return $"© {DateTime.Now.Year} {CompanyName}. All rights reserved.";
        }
    }
}
