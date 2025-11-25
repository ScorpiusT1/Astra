using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Astra.Core.Configuration
{
    public static class ConfigPathString
    {
        public static string BaseConfigDirectory => Path.Combine(AppContext.BaseDirectory, "Configs");

        public static string DeviceConfigDirectory => Path.Combine(BaseConfigDirectory, "Devices");
    }
}
