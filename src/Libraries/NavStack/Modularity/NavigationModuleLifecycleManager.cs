namespace NavStack.Modularity
{
    /// <summary>
    /// 模块生命周期管理器
    /// </summary>
    public class NavigationModuleLifecycleManager
    {
        private readonly List<INavigationModuleLifecycle> _lifecycleModules = new();
        private IServiceProvider _serviceProvider;

        public void RegisterLifecycleModule(INavigationModuleLifecycle module)
        {
            _lifecycleModules.Add(module);
        }

        public void NotifyInitialized(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            foreach (var module in _lifecycleModules)
            {
                try
                {
                    module.OnInitialized(serviceProvider);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Module initialization failed: {ex.Message}");
                }
            }
        }

        public void NotifyShutdown()
        {
            foreach (var module in _lifecycleModules)
            {
                try
                {
                    module.OnShutdown();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Module shutdown failed: {ex.Message}");
                }
            }
        }
    }
}
