using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Management.Commands
{
    /// <summary>
    /// 插件管理接口 - 提供管理功能而非 UI
    /// </summary>
    public interface IPluginAdministrator
    {
        /// <summary>
        /// 获取所有插件信息
        /// </summary>
        Task<List<PluginInfo>> GetAllPluginsAsync();

        /// <summary>
        /// 启用插件
        /// </summary>
        Task EnablePluginAsync(string pluginId);

        /// <summary>
        /// 禁用插件
        /// </summary>
        Task DisablePluginAsync(string pluginId);

        /// <summary>
        /// 更新插件配置
        /// </summary>
        Task UpdateConfigurationAsync(string pluginId, Dictionary<string, object> config);

        /// <summary>
        /// 获取插件配置
        /// </summary>
        Task<Dictionary<string, object>> GetConfigurationAsync(string pluginId);

        /// <summary>
        /// 执行管理命令
        /// </summary>
        Task<CommandResult> ExecuteCommandAsync(AdminCommand command);
    }
}
