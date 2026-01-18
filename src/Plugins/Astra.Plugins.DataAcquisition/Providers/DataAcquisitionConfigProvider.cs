using Astra.Core.Configuration;
using Astra.Plugins.DataAcquisition.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Plugins.DataAcquisition.Providers
{
    public class DataAcquisitionConfigProvider : JsonConfigProvider<DataAcquisitionConfig>
    {
        public DataAcquisitionConfigProvider(string configDirectory, ConfigProviderOptions<DataAcquisitionConfig>? options) : base(configDirectory, options)
        {
        }
    }
}
