using System;
using System.Threading.Tasks;

namespace Astra.Services.Monitoring
{
	public interface ITelemetryService
	{
		void TrackEvent(string name, object properties = null);
		void TrackMetric(string name, double value, object properties = null);
		Task<T> TrackDurationAsync<T>(string name, Func<Task<T>> work, object properties = null);
		Task TrackDurationAsync(string name, Func<Task> work, object properties = null);
	}
}


