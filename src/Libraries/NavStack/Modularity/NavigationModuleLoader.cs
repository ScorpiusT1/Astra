using System.Reflection;

namespace NavStack.Modularity
{
    #region 自动发现功能

    /// <summary>
    /// 模块加载器 - 自动发现和加载模块
    /// </summary>
    public class NavigationModuleLoader
    {
        /// <summary>
        /// 从指定程序集加载模块
        /// </summary>
        public static IEnumerable<INavigationModule> LoadFromAssemblies(params Assembly[] assemblies)
        {
            var modules = new List<(INavigationModule Module, int Priority)>();

            foreach (var assembly in assemblies)
            {
                try
                {
                    var moduleTypes = assembly.GetTypes()
                        .Where(t => typeof(INavigationModule).IsAssignableFrom(t)
                            && !t.IsInterface
                            && !t.IsAbstract
                            && t.GetConstructor(Type.EmptyTypes) != null);

                    foreach (var type in moduleTypes)
                    {
                        var attr = type.GetCustomAttribute<NavigationModuleAttribute>();
                        var module = (INavigationModule)Activator.CreateInstance(type);
                        var priority = attr?.Priority ?? 0;

                        modules.Add((module, priority));
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // 记录加载失败的类型
                    foreach (var loaderEx in ex.LoaderExceptions)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to load type: {loaderEx?.Message}");
                    }
                }
            }

            // 按优先级排序返回
            return modules.OrderByDescending(m => m.Priority).Select(m => m.Module);
        }

        /// <summary>
        /// 从目录加载所有DLL中的模块
        /// </summary>
        public static IEnumerable<INavigationModule> LoadFromDirectory(string path, string searchPattern = "*.dll")
        {
            if (!System.IO.Directory.Exists(path))
            {
                throw new System.IO.DirectoryNotFoundException($"Directory not found: {path}");
            }

            var assemblies = System.IO.Directory.GetFiles(path, searchPattern)
                .Select(file =>
                {
                    try
                    {
                        return Assembly.LoadFrom(file);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to load assembly {file}: {ex.Message}");
                        return null;
                    }
                })
                .Where(a => a != null);

            return LoadFromAssemblies(assemblies.ToArray());
        }

        /// <summary>
        /// 从当前应用程序域加载所有模块
        /// </summary>
        public static IEnumerable<INavigationModule> LoadFromCurrentDomain()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            return LoadFromAssemblies(assemblies);
        }
    }

    #endregion

}
