using Astra.Core.Foundation.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Access.Configuration
{
    /// <summary>
    /// Access配置选项
    /// </summary>
    public class AccessGuardOptions
    {
        /// <summary>
        /// 数据库路径（如果为null则使用默认路径：根目录/Config/UserManagement.db）
        /// </summary>
        public string DatabasePath { get; set; }

        /// <summary>
        /// 数据库文件名（默认：UserManagement.db）
        /// </summary>
        public string DatabaseFileName { get; set; } = "UserManagement.db";

        /// <summary>
        /// 是否使用系统用户目录（默认false，使用根目录Config文件夹）
        /// </summary>
        public bool UseUserDataDirectory { get; set; } = false;

        /// <summary>
        /// 应用程序名称（当UseUserDataDirectory=true时使用）
        /// </summary>
        public string ApplicationName { get; set; } = "Access";

        /// <summary>
        /// 是否自动创建目录
        /// </summary>
        public bool AutoCreateDirectory { get; set; } = true;

        /// <summary>
        /// 获取最终的数据库路径
        /// </summary>
        public string GetEffectiveDatabasePath()
        {
            // 1. 优先使用明确指定的路径
            if (!string.IsNullOrEmpty(DatabasePath))
            {
                string effectivePath;

                // 如果是相对路径，转换为相对于应用程序根目录的路径
                if (!Path.IsPathRooted(DatabasePath))
                {
                    effectivePath = PathHelper.GetFullPath(DatabasePath);
                }
                else
                {
                    effectivePath = DatabasePath;
                }

                if (AutoCreateDirectory)
                {
                    PathHelper.EnsureDirectoryExists(effectivePath);
                }

                return effectivePath;
            }

            // 2. 使用系统用户目录（可选）
            if (UseUserDataDirectory)
            {
                string userDataDir = PathHelper.GetUserDataDirectory(ApplicationName);
                if (AutoCreateDirectory && !Directory.Exists(userDataDir))
                {
                    Directory.CreateDirectory(userDataDir);
                }
                return Path.Combine(userDataDir, DatabaseFileName);
            }

            // 3. 默认：使用根目录的Config文件夹
            return PathHelper.GetDefaultDatabasePath(DatabaseFileName);
        }
    }
}
