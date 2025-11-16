namespace Astra.Services.Navigation
{
	public interface INavigationRegistryProvider
	{
		void Register(string regionName, NavStack.Services.INavigationManager navManager);
	}
}


