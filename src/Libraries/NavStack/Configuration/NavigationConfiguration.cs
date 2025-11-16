using System.Windows;

namespace NavStack.Configuration
{
    /// <summary>
    /// 导航配置实现
    /// </summary>
    public class NavigationConfiguration : INavigationConfiguration
    {
        private readonly Dictionary<string, PageRegistration> _registrations = new();

        public void RegisterPage<TView>(string key, bool singleton = false) where TView : FrameworkElement
        {
            _registrations[key] = new PageRegistration
            {
                Key = key,
                ViewType = typeof(TView),
                IsSingleton = singleton
            };
        }

        public void RegisterPage<TView, TViewModel>(string key, bool singleton = false)
            where TView : FrameworkElement
        {
            _registrations[key] = new PageRegistration
            {
                Key = key,
                ViewType = typeof(TView),
                ViewModelType = typeof(TViewModel),
                IsSingleton = singleton
            };
        }

		public void RegisterPage(string key, System.Type viewType, System.Type viewModelType = null, bool singleton = false)
		{
			_registrations[key] = new PageRegistration
			{
				Key = key,
				ViewType = viewType,
				ViewModelType = viewModelType,
				IsSingleton = singleton
			};
		}

        public PageRegistration GetPageRegistration(string key)
        {
            return _registrations.TryGetValue(key, out var registration) ? registration : null;
        }

        public IEnumerable<PageRegistration> GetAllRegistrations()
        {
            return _registrations.Values;
        }
    }
}
