using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.Health;

namespace Astra.Plugins.Logic
{
    public sealed class LogicPlugin : IPlugin
    {
        public string Id => "Astra.Plugins.Logic";

        public string Name => "流程逻辑插件";

        public Version Version => new(1, 0, 0);

        public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

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
