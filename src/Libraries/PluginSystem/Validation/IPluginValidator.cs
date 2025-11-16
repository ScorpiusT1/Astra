using Addins.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Addins.Validation
{
    /// <summary>
    /// 插件验证器
    /// </summary>
    public interface IPluginValidator
    {
        Task<ValidationResult> ValidateAsync(PluginDescriptor descriptor);
        void AddRule(IValidationRule rule);
    }
}
