using Astra.Utilities;
using Astra.ViewModels;
using Astra.Views;
using System;
using System.Configuration;

namespace Astra.Services.Navigation
{
	public class DefaultNavigationRegistryProvider : INavigationRegistryProvider
	{
		public void Register(string regionName, NavStack.Services.INavigationManager navManager)
		{
			navManager.RegisterForRegion(regionName, NavigationKeys.Home, typeof(Astra.Views.HomeView));
			navManager.RegisterForRegion(regionName, NavigationKeys.Config, typeof(Astra.Views.ConfigView));
			navManager.RegisterForRegion(regionName, NavigationKeys.Debug, typeof(Astra.Views.DebugView));
			navManager.RegisterForRegion(regionName, NavigationKeys.Sequence, typeof(Astra.Views.SequenceView));
            navManager.RegisterForRegion(regionName, NavigationKeys.Permission, typeof(Astra.Views.PermissionView));
          
		}
	}
}


