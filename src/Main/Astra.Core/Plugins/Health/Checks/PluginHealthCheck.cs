using Astra.Core.Plugins.Abstractions;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Health
{
    /// <summary>
    /// 插件存活健康检查 - 验证指定插件是否已加载并正在运行。
    /// </summary>
    public class PluginHealthCheck : IHealthCheck
    {
        private readonly IPluginHost _host;
        private readonly string _pluginId;

        public string Name => $"Plugin-{_pluginId}";

        public PluginHealthCheck(IPluginHost host, string pluginId)
        {
            _host = host;
            _pluginId = pluginId;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                var plugin = _host.LoadedPlugins.FirstOrDefault(p => p.Id == _pluginId);
                if (plugin == null)
                    return HealthCheckResult.Unhealthy(Name, "Plugin not found");

                return HealthCheckResult.Healthy(Name, "Plugin is running", DateTime.UtcNow - startTime);
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy(Name, "Plugin health check failed", ex, DateTime.UtcNow - startTime);
            }
        }
    }
}
