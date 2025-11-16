using Astra.Core.Plugins.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Validation
{
    /// <summary>
    /// 插件验证器：对 <see cref="PluginDescriptor"/> 执行一组验证规则并返回汇总结果。
    /// </summary>
    public interface IPluginValidator
    {
        /// <summary>
        /// 执行验证并返回结果。
        /// </summary>
        Task<ValidationResult> ValidateAsync(PluginDescriptor descriptor);
        /// <summary>
        /// 添加一条验证规则（按添加顺序执行）。
        /// </summary>
        void AddRule(IValidationRule rule);
    }
}
