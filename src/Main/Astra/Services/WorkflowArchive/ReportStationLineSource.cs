using System.Linq;
using System.Threading.Tasks;
using Astra.Configuration;
using Astra.Core.Configuration.Abstractions;
using Astra.Core.Reporting;

namespace Astra.Services.WorkflowArchive
{
    /// <summary>
    /// 从 <see cref="SoftwareConfig"/> 读取工站名、线体名。
    /// </summary>
    public sealed class ReportStationLineSource : IReportStationLineSource
    {
        private readonly IConfigurationManager _configurationManager;

        public ReportStationLineSource(IConfigurationManager configurationManager)
        {
            _configurationManager = configurationManager;
        }

        /// <inheritdoc />
        public (string StationName, string LineName) GetStationAndLine()
        {
            try
            {
                var result = _configurationManager.GetAllAsync<SoftwareConfig>().GetAwaiter().GetResult();
                if (result.Success && result.Data != null)
                {
                    var sc = result.Data.FirstOrDefault();
                    if (sc != null)
                        return (sc.StationName?.Trim() ?? string.Empty, sc.LineName?.Trim() ?? string.Empty);
                }
            }
            catch
            {
                // 忽略读取失败，返回空
            }

            return (string.Empty, string.Empty);
        }
    }
}
