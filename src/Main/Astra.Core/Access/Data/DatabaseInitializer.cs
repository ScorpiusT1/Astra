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

        /// <summary>
        /// 初始化数据库（创建表结构并添加默认管理员）
        /// </summary>
        public static void Initialize(AccessGuardDbContext context)
        {
            // 确保数据库已创建
            context.Database.EnsureCreated();

            // 检查是否已有用户
            if (context.Users.Any())
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
    }
}
