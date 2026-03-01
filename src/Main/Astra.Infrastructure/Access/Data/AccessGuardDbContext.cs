using Astra.Core.Access.Models;
using Microsoft.EntityFrameworkCore;

namespace Astra.Infrastructure.Access.Data
{
    /// <summary>
    /// Access 数据库上下文（基础设施层，仅限 Astra.Infrastructure 项目使用）
    /// </summary>
    public class AccessGuardDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }

        /// <summary>
        /// 用于依赖注入的构造函数（推荐）
        /// </summary>
        public AccessGuardDbContext(DbContextOptions<AccessGuardDbContext> options)
            : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(e => e.Id);

                entity.HasIndex(e => e.Username)
                    .IsUnique()
                    .HasDatabaseName("IX_Users_Username");

                entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
                entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(256);
                entity.Property(e => e.Role).IsRequired().HasConversion<int>();
                entity.Property(e => e.CreateTime).IsRequired();
                entity.Property(e => e.LastModifyTime).IsRequired();
                entity.Property(e => e.LastLoginTime).IsRequired(false);
            });
        }
    }
}
