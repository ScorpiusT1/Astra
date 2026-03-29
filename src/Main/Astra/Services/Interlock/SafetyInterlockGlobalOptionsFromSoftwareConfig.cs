using Astra.Configuration;
using Astra.Core.Configuration.Abstractions;
using Astra.Core.Triggers.Interlock;
using System.Linq;

namespace Astra.Services.Interlock
{
    /// <summary>
    /// 从 <see cref="SoftwareConfig"/> 读取安全联锁总开关与轮询周期。
    /// </summary>
    public sealed class SafetyInterlockGlobalOptionsFromSoftwareConfig : ISafetyInterlockGlobalOptionsSource
    {
        private readonly IConfigurationManager _configurationManager;

        public SafetyInterlockGlobalOptionsFromSoftwareConfig(IConfigurationManager configurationManager)
        {
            _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
        }

        public async ValueTask<(bool Enabled, int PollIntervalMs)> GetOptionsAsync(CancellationToken cancellationToken = default)
        {
            var result = await _configurationManager.GetAllAsync<SoftwareConfig>().ConfigureAwait(false);
            if (!result.Success || result.Data == null)
            {
                return (true, 100);
            }

            var sc = result.Data.OfType<SoftwareConfig>().FirstOrDefault();
            if (sc == null)
            {
                return (true, 100);
            }

            var poll = sc.SafetyInterlockPollIntervalMs;
            if (poll < 50)
            {
                poll = 50;
            }

            if (poll > 60_000)
            {
                poll = 60_000;
            }

            return (sc.SafetyInterlockEnabled, poll);
        }
    }
}
