using Astra.Bootstrap.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Bootstrap.Tasks
{
    /// <summary>
    /// 数据库初始化任务
    /// </summary>
    public class DatabaseInitializationTask : BootstrapTaskBase
    {
        private readonly string _connectionString;

        public DatabaseInitializationTask(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public override string Name => "数据库初始化";
        public override string Description => "正在初始化数据库连接...";
        public override double Weight => 2.0;
        public override int Priority => 30;
        public override bool IsCritical => true;

        protected override async Task ExecuteCoreAsync(
            BootstrapContext context,
            IProgress<BootstrapProgress> progress,
            CancellationToken cancellationToken)
        {
            ReportProgress(progress, 10, "连接数据库...", _connectionString);

            // 模拟数据库连接
            await Task.Delay(500, cancellationToken);

            ReportProgress(progress, 40, "检查数据库版本...");
            await Task.Delay(300, cancellationToken);

            ReportProgress(progress, 70, "执行数据库迁移...");
            await Task.Delay(400, cancellationToken);

            ReportProgress(progress, 90, "验证数据库状态...");
            await Task.Delay(200, cancellationToken);

            // 保存连接信息
            context.SetData("DatabaseConnected", true);

            ReportProgress(progress, 100, "数据库初始化完成");
        }

        public override async Task RollbackAsync(BootstrapContext context)
        {
            // 关闭数据库连接
            context.SetData("DatabaseConnected", false);
            await Task.CompletedTask;
        }
    }
}
