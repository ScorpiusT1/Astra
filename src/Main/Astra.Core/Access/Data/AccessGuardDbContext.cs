using Astra.Core.Access;
using Astra.Core.Access.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Access.Data
{
    /// <summary>
    /// Access数据库上下文
    /// </summary>
    public class AccessGuardDbContext : DbContext
    {
        private readonly string _dbPath;
        private const string DEFAULT_DB_PATH = "Bin/Config";
        private const string DB_FILENAME = "UserManagement.db";

        public DbSet<User> Users { get; set; }

        /// <summary>
        /// 默认构造函数（使用默认路径）
        /// </summary>
        public AccessGuardDbContext() : this("")
        {
        }

        /// <summary>
        /// 指定数据库路径的构造函数
        /// </summary>
        public AccessGuardDbContext(string dbPath)
        {
            _dbPath = Path.Combine(dbPath ?? DEFAULT_DB_PATH, DB_FILENAME);

            // 确保目录存在
            string directory = Path.GetDirectoryName(_dbPath);

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        /// <summary>
        /// 用于依赖注入的构造函数
        /// </summary>
        public AccessGuardDbContext(DbContextOptions<AccessGuardDbContext> options)
            : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured && !string.IsNullOrEmpty(_dbPath))
            {
                optionsBuilder.UseSqlite($"Data Source={_dbPath}");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 配置User实体
            modelBuilder.Entity<User>(entity =>
            {
                // 设置表名
                entity.ToTable("Users");

                // 设置主键
                entity.HasKey(e => e.Id);

                // 配置Username唯一索引
                entity.HasIndex(e => e.Username)
                    .IsUnique()
                    .HasDatabaseName("IX_Users_Username");

                // 配置字段
                entity.Property(e => e.Username)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.PasswordHash)
                    .IsRequired()
                    .HasMaxLength(256);

                entity.Property(e => e.Role)
                    .IsRequired()
                    .HasConversion<int>();

                entity.Property(e => e.CreateTime)
                    .IsRequired();

                entity.Property(e => e.LastModifyTime)
                    .IsRequired();

                entity.Property(e => e.LastLoginTime)
                    .IsRequired(false);
            });
        }
    }
}
