using System.Threading.Tasks;

namespace Astra.Services.Monitoring
{
	public interface IHealthCheckService
	{
		Task<HealthCheckResult> CheckAsync();
	}

	public class HealthCheckResult
	{
		public bool IsHealthy { get; set; }
		public string Message { get; set; }
	}
}


