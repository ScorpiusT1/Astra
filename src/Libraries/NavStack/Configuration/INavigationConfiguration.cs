using System.Windows;

namespace NavStack.Configuration
{
    /// <summary>
    /// 导航配置
    /// </summary>
    public interface INavigationConfiguration
    {
        void RegisterPage<TView>(string key, bool singleton = false) where TView : FrameworkElement;
        void RegisterPage<TView, TViewModel>(string key, bool singleton = false)
            where TView : FrameworkElement;
		void RegisterPage(string key, System.Type viewType, System.Type viewModelType = null, bool singleton = false);

        PageRegistration GetPageRegistration(string key);
        IEnumerable<PageRegistration> GetAllRegistrations();
    }
}
