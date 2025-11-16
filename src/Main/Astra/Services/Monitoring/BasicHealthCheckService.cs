using System.Threading.Tasks;
using Astra.UI.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NavStack.Regions;
using Astra.Services.Navigation;

namespace Astra.Services.Monitoring
{
	public class BasicHealthCheckService : IHealthCheckService
	{
		private readonly System.IServiceProvider _services;
		private readonly ILogger<BasicHealthCheckService> _logger;

		public BasicHealthCheckService(System.IServiceProvider services, ILogger<BasicHealthCheckService> logger = null)
		{
			_services = services;
			_logger = logger ?? NullLogger<BasicHealthCheckService>.Instance;
		}

		public Task<HealthCheckResult> CheckAsync()
		{
			var result = new HealthCheckResult { IsHealthy = true, Message = "OK" };

			// 检查 RegionManager
			var regionMgr = _services.GetService<IRegionManager>();
			if (regionMgr == null)
			{
				result.IsHealthy = false;
				result.Message = "RegionManager 未解析";
				_logger.LogError("[Health] RegionManager 未解析");
				return Task.FromResult(result);
			}

			// 检查权限服务
			var perm = _services.GetService<INavigationPermissionService>();
			if (perm == null)
			{
				_logger.LogWarning("[Health] INavigationPermissionService 未解析");
			}

			return Task.FromResult(result);
		}
	}
}


