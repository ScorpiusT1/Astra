using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Astra.Services.Monitoring
{
	public class TelemetryService : ITelemetryService
	{
		private readonly ILogger<TelemetryService> _logger;

		public TelemetryService(ILogger<TelemetryService> logger = null)
		{
			_logger = logger ?? NullLogger<TelemetryService>.Instance;
		}

		public void TrackEvent(string name, object properties = null)
		{
			_logger.LogInformation("Event: {Name} | Properties: {@Props}", name, properties);
		}

		public void TrackMetric(string name, double value, object properties = null)
		{
			_logger.LogInformation("Metric: {Name}={Value} | Properties: {@Props}", name, value, properties);
		}

		public async Task<T> TrackDurationAsync<T>(string name, Func<Task<T>> work, object properties = null)
		{
			var sw = Stopwatch.StartNew();
			try
			{
				return await work();
			}
			finally
			{
				sw.Stop();
				TrackMetric($"{name}.DurationMs", sw.Elapsed.TotalMilliseconds, properties);
			}
		}

		public async Task TrackDurationAsync(string name, Func<Task> work, object properties = null)
		{
			var sw = Stopwatch.StartNew();
			try
			{
				await work();
			}
			finally
			{
				sw.Stop();
				TrackMetric($"{name}.DurationMs", sw.Elapsed.TotalMilliseconds, properties);
			}
		}
	}
}


