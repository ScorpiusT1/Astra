using Astra.Core.Access;
using Astra.Core.Access.Models;
using Astra.Core.Foundation.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Access.Data
{
    /// <summary>
    /// 数据库初始化器
    /// </summary>
    public static class DatabaseInitializer
    {
        private const string DEFAULT_ADMIN_USERNAME = "Admin";
        private const string DEFAULT_ADMIN_PASSWORD = "Admin123";
        private const string SUPER_ADMIN_USERNAME = "SupperAdmin";
        private const string SUPER_ADMIN_PASSWORD = "Admin.123";

        /// <summary>
        /// 初始化数据库（创建表结构并添加默认管理员和超级管理员）
        /// </summary>
        public static void Initialize(AccessGuardDbContext context)
        {
            // 确保数据库已创建
            context.Database.EnsureCreated();

            // 检查并创建超级管理员（如果不存在）
            EnsureSuperAdministrator(context);

            // 检查是否已有其他用户（除了超级管理员）
            if (context.Users.Any(u => u.Role != UserRole.SuperAdministrator))
            {
                return; // 数据库已初始化
            }

            // 创建默认管理员
            var admin = new User
            {
                Username = DEFAULT_ADMIN_USERNAME,
                PasswordHash = PasswordHelper.HashPassword(DEFAULT_ADMIN_PASSWORD),
                Role = UserRole.Administrator,
                CreateTime = DateTime.Now,
                LastModifyTime = DateTime.Now
            };

            context.Users.Add(admin);
            context.SaveChanges();
        }

        /// <summary>
        /// 确保超级管理员存在（如果不存在则创建）
        /// </summary>
        private static void EnsureSuperAdministrator(AccessGuardDbContext context)
        {
            // 检查是否已存在超级管理员
            var superAdmin = context.Users
                .FirstOrDefault(u => u.Username == SUPER_ADMIN_USERNAME || u.Role == UserRole.SuperAdministrator);

            if (superAdmin == null)
            {
                // 创建超级管理员
                var newSuperAdmin = new User
                {
                    Username = SUPER_ADMIN_USERNAME,
                    PasswordHash = PasswordHelper.HashPassword(SUPER_ADMIN_PASSWORD),
                    Role = UserRole.SuperAdministrator,
                    CreateTime = DateTime.Now,
                    LastModifyTime = DateTime.Now
                };

                context.Users.Add(newSuperAdmin);
                context.SaveChanges();
            }
            else if (superAdmin.Role != UserRole.SuperAdministrator)
            {
                // 如果用户名匹配但角色不对，更新角色
                superAdmin.Role = UserRole.SuperAdministrator;
                superAdmin.LastModifyTime = DateTime.Now;
                context.SaveChanges();
            }
            else if (superAdmin.Username != SUPER_ADMIN_USERNAME)
            {
                // 如果角色匹配但用户名不对，更新用户名
                superAdmin.Username = SUPER_ADMIN_USERNAME;
                superAdmin.PasswordHash = PasswordHelper.HashPassword(SUPER_ADMIN_PASSWORD);
                superAdmin.LastModifyTime = DateTime.Now;
                context.SaveChanges();
            }
        }
    }
}
