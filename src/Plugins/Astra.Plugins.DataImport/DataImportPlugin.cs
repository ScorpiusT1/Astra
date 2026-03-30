using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.Health;

namespace Astra.Plugins.DataImport
{
    public sealed class DataImportPlugin : IPlugin
    {
        public string Id => "Astra.Plugins.DataImport";

        public string Name => "数据导入与转换";

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
