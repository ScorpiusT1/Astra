using Microsoft.Extensions.DependencyInjection;
using NavStack.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NavStack.Modularity
{
    /// <summary>
    /// 模块管理器 - 管理和初始化所有模块
    /// </summary>
    public class NavigationModuleManager
    {
        private readonly List<INavigationModule> _modules = new();
        private bool _isInitialized = false;

        /// <summary>
        /// 注册模块
        /// </summary>
        public void RegisterModule(INavigationModule module)
        {
            if (_isInitialized)
            {
                throw new InvalidOperationException("Cannot register module after initialization");
            }

            if (module == null)
            {
                throw new ArgumentNullException(nameof(module));
            }

            if (_modules.Any(m => m.ModuleName == module.ModuleName))
            {
                throw new InvalidOperationException($"Module '{module.ModuleName}' already registered");
            }

            _modules.Add(module);
        }

        /// <summary>
        /// 初始化所有模块
        /// </summary>
        public void InitializeModules(INavigationConfiguration configuration, IServiceCollection services)
        {
            if (_isInitialized)
            {
                throw new InvalidOperationException("Modules already initialized");
            }

            foreach (var module in _modules)
            {
                try
                {
                    module.RegisterTypes(configuration, services);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to initialize module '{module.ModuleName}'", ex);
                }
            }

            _isInitialized = true;
        }

        /// <summary>
        /// 获取所有已注册的模块
        /// </summary>
        public IReadOnlyList<INavigationModule> GetModules() => _modules.AsReadOnly();
    }
}
