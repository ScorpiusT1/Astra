using Addins.Configuration;
using Addins.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Addins.Core.Abstractions
{

    /// <summary>
    /// 插件上下文接口 - 接口隔离原则
    /// </summary>
    public interface IPluginContext
    {
        IServiceRegistry Services { get; }
        IMessageBus MessageBus { get; }
        IConfigurationStore Configuration { get; }
        IPluginHost Host { get; }
    }
}
