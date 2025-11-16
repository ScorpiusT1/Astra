using NavStack.Configuration;
using NavStack.Core;
using NavStack.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace NavStack.Regions
{

    public class RegionManager : IRegionManager
    {
        private readonly Dictionary<string, ContentControl> _regions = new();
        private readonly Dictionary<string, IRegionNavigationService> _navigationServices = new();
        private readonly IServiceProvider _serviceProvider;
        private readonly INavigationConfiguration _configuration;

        public RegionManager(IServiceProvider serviceProvider, INavigationConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
        }

        public void RegisterRegion(string regionName, ContentControl control)
        {
            _regions[regionName] = control;
            _navigationServices[regionName] = new RegionNavigationService(
                regionName, control, _serviceProvider, _configuration);
        }

        public IRegionNavigationService GetNavigationService(string regionName)
        {
            return _navigationServices.TryGetValue(regionName, out var service) ? service : null;
        }

        public void UnregisterRegion(string regionName)
        {
            _regions.Remove(regionName);
            _navigationServices.Remove(regionName);
        }
    }
}
