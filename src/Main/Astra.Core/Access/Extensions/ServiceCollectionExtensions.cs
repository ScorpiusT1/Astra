using Astra.Core.Access.Configuration;
using Astra.Core.Access.Data;
using Astra.Core.Access.Repositories;
using Astra.Core.Access.Security;
using Astra.Core.Access.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Access.Extensions
{
    /// <summary>
    /// 依赖注入扩展方法
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 注册Access服务（使用默认配置：根目录/Config/UserManagement.db）
        /// </summary>
        public static IServiceCollection AddAccessGuard(this IServiceCollection services)
        {
            return services.AddAccessGuard(options => { });
        }

        /// <summary>
        /// 注册Access服务（指定数据库路径）
        /// </summary>
        /// <param name="databasePath">
        /// 数据库路径，支持：
        /// - 完整路径: "D:\Data\users.db"
        /// - 相对路径: "Config\UserManagement.db" (相对于应用程序根目录)
        /// - null: 使用默认路径（根目录/Config/UserManagement.db）
        /// </param>
        public static IServiceCollection AddAccessGuard(
            this IServiceCollection services,
            string databasePath)
        {
            return services.AddAccessGuard(options =>
            {
                options.DatabasePath = databasePath;
            });
        }

        /// <summary>
        /// 注册Access服务（使用配置委托）
        /// </summary>
        public static IServiceCollection AddAccessGuard(
            this IServiceCollection services,
            Action<AccessGuardOptions> configureOptions)
        {
            // 配置选项
            var options = new AccessGuardOptions();
            configureOptions?.Invoke(options);

            // 获取有效的数据库路径
            string dbPath = options.GetEffectiveDatabasePath();

            // 注册DbContext
            services.AddDbContext<AccessGuardDbContext>(dbOptions =>
                dbOptions.UseSqlite($"Data Source={dbPath}"));

            // 注册仓储
            services.AddScoped<IUserRepository, UserRepository>();

            // 注册安全服务
            services.AddScoped<IPasswordService, PasswordService>();
            services.AddScoped<IPermissionValidator, PermissionValidator>();

            // 注册业务服务
            services.AddScoped<IAuthenticationService, AuthenticationService>();

            // 明确注册UserManagementService，解决构造函数歧义问题
            services.AddScoped<IUserManagementService>(provider =>
            {
                var context = provider.GetRequiredService<AccessGuardDbContext>();
                return new UserManagementService(context);
            });

            // 初始化数据库
            using (var scope = services.BuildServiceProvider().CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AccessGuardDbContext>();
                DatabaseInitializer.Initialize(context);
            }

            return services;
        }
    }
}
