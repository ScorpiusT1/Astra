using Astra.Core.Logs.Extensions;
using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.Health;
using Astra.Plugins.Algorithms.APIs;
using Microsoft.VisualBasic.Logging;

namespace Astra.Plugins.Algorithms
{
    public sealed class AlgorithmPlugin : IPlugin
    {
        public string Id => "Astra.Plugins.Algorithms";

        public string Name => "NVH 算法插件";

        public Version Version => new(1, 0, 0);

        public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                if (!Nvh.LoadLicense("license.lic", out string msg))
                {
                    context.Logger.LogError($"加载license失败: {msg}");
                }
            });
        }

        public Task OnEnableAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task OnDisableAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<HealthCheckResult> CheckHealthAsync()
            => Task.FromResult(HealthCheckResult.Healthy(Name, "OK"));

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
