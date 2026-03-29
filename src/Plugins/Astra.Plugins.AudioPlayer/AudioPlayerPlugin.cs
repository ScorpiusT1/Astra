using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.Health;
using NVHDataBridge.IO.WAV;

namespace Astra.Plugins.AudioPlayer
{
    public class AudioPlayerPlugin : IPlugin
    {
        public string Id => "Astra.Plugins.AudioPlayer";

        public string Name => "音频播放器";

        public Version Version => new Version(1, 0, 0);

        public Task<HealthCheckResult> CheckHealthAsync()
        {
            return Task.FromResult(HealthCheckResult.Healthy(Name, "音频播放器插件已加载"));
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        {
            RealtimeAudioPlayer.PreloadWasapiRenderDevices();
            return Task.CompletedTask;
        }

        public Task OnDisableAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task OnEnableAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
