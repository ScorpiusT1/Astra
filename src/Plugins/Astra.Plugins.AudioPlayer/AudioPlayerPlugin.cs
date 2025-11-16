using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.Health;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Plugins.AudioPlayer
{
    public class AudioPlayerPlugin : IPlugin
    {
        public string Id => "Astra.Plugins.AudioPlayer";

        public string Name => "音频播放器";

        public Version Version => new Version(1, 0, 0);

        public Task<HealthCheckResult> CheckHealthAsync()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }

        public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task OnDisableAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task OnEnableAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
