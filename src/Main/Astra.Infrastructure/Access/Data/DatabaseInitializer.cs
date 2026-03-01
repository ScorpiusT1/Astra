using Astra.Core.Access.Models;
using Astra.Core.Foundation.Common;

namespace Astra.Infrastructure.Access.Data
{
    /// <summary>
    /// 数据库初始化器：创建表结构并预置超级管理员和默认管理员账户
    /// </summary>
    public static class DatabaseInitializer
    {
        private const string DEFAULT_ADMIN_USERNAME = "Admin";
        private const string DEFAULT_ADMIN_PASSWORD = "Admin123";
        private const string SUPER_ADMIN_USERNAME = "SupperAdmin";
        private const string SUPER_ADMIN_PASSWORD = "Admin.123";

        public static void Initialize(AccessGuardDbContext context)
        {
            context.Database.EnsureCreated();
            EnsureSuperAdministrator(context);

            if (context.Users.Any(u => u.Role != UserRole.SuperAdministrator))
                return;

            context.Users.Add(new User
            {
                Username = DEFAULT_ADMIN_USERNAME,
                PasswordHash = PasswordHelper.HashPassword(DEFAULT_ADMIN_PASSWORD),
                Role = UserRole.Administrator,
                CreateTime = DateTime.Now,
                LastModifyTime = DateTime.Now
            });
            context.SaveChanges();
        }

        private static void EnsureSuperAdministrator(AccessGuardDbContext context)
        {
            var superAdmin = context.Users.FirstOrDefault(
                u => u.Username == SUPER_ADMIN_USERNAME || u.Role == UserRole.SuperAdministrator);

            if (superAdmin == null)
            {
                context.Users.Add(new User
                {
                    Username = SUPER_ADMIN_USERNAME,
                    PasswordHash = PasswordHelper.HashPassword(SUPER_ADMIN_PASSWORD),
                    Role = UserRole.SuperAdministrator,
                    CreateTime = DateTime.Now,
                    LastModifyTime = DateTime.Now
                });
                context.SaveChanges();
            }
            else if (superAdmin.Role != UserRole.SuperAdministrator)
            {
                superAdmin.Role = UserRole.SuperAdministrator;
                superAdmin.LastModifyTime = DateTime.Now;
                context.SaveChanges();
            }
            else if (superAdmin.Username != SUPER_ADMIN_USERNAME)
            {
                superAdmin.Username = SUPER_ADMIN_USERNAME;
                superAdmin.PasswordHash = PasswordHelper.HashPassword(SUPER_ADMIN_PASSWORD);
                superAdmin.LastModifyTime = DateTime.Now;
                context.SaveChanges();
            }
        }
    }
}
