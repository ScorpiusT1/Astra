using System;
using System.IO;
using System.Runtime.InteropServices;

// ⚠️ 迁移说明：
//   - 原位置：Astra.Core/Access/Utilities/PathHelper.cs
//   - 新位置：Astra.Core/Foundation/Common/PathHelper.cs
//   - 命名空间：Astra.Core.Foundation.Common（已更新为与文件夹匹配）

namespace Astra.Core.Foundation.Common
{
    /// <summary>
    /// 路径辅助类
    /// 
    /// ✅ 迁移说明：
    ///   - 文件已移动到 Foundation/Common/
    ///   - 命名空间已更新为：Astra.Core.Foundation.Common
    ///   - 建议新代码使用：using Astra.Core.Foundation.Common;
    /// </summary>
    public static class PathHelper
    {
        /// <summary>
        /// 获取应用程序根目录（exe所在目录）
        /// </summary>
        public static string GetApplicationRootDirectory()
        {
            // 方式1: 推荐使用 AppContext.BaseDirectory
            return AppContext.BaseDirectory;

            // 方式2: 备选方案
            // return AppDomain.CurrentDomain.BaseDirectory;
        }

        /// <summary>
        /// 获取应用程序根目录下的Config文件夹路径
        /// </summary>
        public static string GetConfigDirectory()
        {
            return Path.Combine(GetApplicationRootDirectory(), "Configs");
        }

        /// <summary>
        /// 获取数据库目录路径（Configs/DataBase）
        /// </summary>
        public static string GetDatabaseDirectory()
        {
            string dbDir = Path.Combine(GetConfigDirectory(), "DataBase");

            // 确保数据库目录存在
            if (!Directory.Exists(dbDir))
            {
                Directory.CreateDirectory(dbDir);
            }

            return dbDir;
        }

        /// <summary>
        /// 获取默认数据库路径（根目录/Configs/DataBase/文件名）
        /// </summary>
        public static string GetDefaultDatabasePath(string dbFileName = "UserManagement.db")
        {
            string dbDir = GetDatabaseDirectory();
            return Path.Combine(dbDir, dbFileName);
        }

        /// <summary>
        /// 确保目录存在
        /// </summary>
        public static void EnsureDirectoryExists(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        /// <summary>
        /// 获取相对于应用程序根目录的完整路径
        /// </summary>
        public static string GetFullPath(string relativePath)
        {
            if (Path.IsPathRooted(relativePath))
            {
                return relativePath; // 已经是完整路径
            }
            return Path.Combine(GetApplicationRootDirectory(), relativePath);
        }

        /// <summary>
        /// 获取系统用户数据目录（可选，用于多用户场景）
        /// </summary>
        public static string GetUserDataDirectory(string appName = "Access")
        {
            string basePath;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(basePath))
                {
                    basePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".local", "share");
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            }
            else
            {
                basePath = GetApplicationRootDirectory();
            }

            return Path.Combine(basePath, appName);
        }
    }
}

