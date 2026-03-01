using Astra.Core.Access.Configuration;
using Astra.Core.Access.Repositories;
using Astra.Core.Access.Security;
using Astra.Core.Access.Services;
using Astra.Infrastructure.Access.Data;
using Astra.Infrastructure.Access.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Astra.Infrastructure.Access.Extensions
{
    /// <summary>
    /// Access 模块 DI 注册扩展方法（基础设施层）
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 注册 Access 服务（使用默认路径）
        /// </summary>
        public static IServiceCollection AddAccessGuard(this IServiceCollection services)
            => services.AddAccessGuard(_ => { });

        /// <summary>
        /// 注册 Access 服务（指定数据库路径）
        /// </summary>
        public static IServiceCollection AddAccessGuard(this IServiceCollection services, string databasePath)
            => services.AddAccessGuard(o => o.DatabasePath = databasePath);

        /// <summary>
        /// 注册 Access 服务（委托配置）
        /// </summary>
        public static IServiceCollection AddAccessGuard(
            this IServiceCollection services,
            Action<AccessGuardOptions> configureOptions)
        {
            var options = new AccessGuardOptions();
            configureOptions?.Invoke(options);

            string dbPath = options.GetEffectiveDatabasePath();

            services.AddDbContext<AccessGuardDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IPasswordService, PasswordService>();
            services.AddScoped<IPermissionValidator, PermissionValidator>();
            services.AddScoped<IAuthenticationService, AuthenticationService>();
            services.AddScoped<IUserManagementService, UserManagementService>();

            // 在容器构建前初始化数据库（确保超级管理员存在）
            using var scope = services.BuildServiceProvider().CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AccessGuardDbContext>();
            DatabaseInitializer.Initialize(context);

            return services;
        }
    }
}
