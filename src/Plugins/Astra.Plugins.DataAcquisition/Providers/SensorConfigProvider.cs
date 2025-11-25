using Astra.Core.Configuration;
using Astra.Plugins.DataAcquisition.Configs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Plugins.DataAcquisition.Providers
{
    internal class SensorConfigProvider : JsonConfigProvider<SensorConfig>
    {
        public SensorConfigProvider(string configDirectory, ConfigProviderOptions<SensorConfig>? options = null) : base(configDirectory, options)
        {

        }
    }
}
