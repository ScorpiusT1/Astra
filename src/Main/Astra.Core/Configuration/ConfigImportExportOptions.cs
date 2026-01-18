using Astra.Core.Foundation.Common;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Astra.Core.Configuration
{
    /// <summary>
    /// 导入选项 - 控制导入行为
    /// </summary>
    public class ImportOptions
    {
        /// <summary>
        /// 目标配置类型
        /// </summary>
        public Type TargetType { get; set; }

        /// <summary>
        /// 导入前是否验证配置
        /// </summary>
        public bool ValidateBeforeImport { get; set; } = true;

        /// <summary>
        /// 是否生成新的 ConfigId（避免冲突）
        /// </summary>
        public bool GenerateNewId { get; set; } = true;

        /// <summary>
        /// 冲突解决策略
        /// </summary>
        public ConflictResolution ConflictResolution { get; set; } = ConflictResolution.Skip;

        /// <summary>
        /// JSON 序列化选项（如果为 null，使用默认选项）
        /// </summary>
        public JsonSerializerOptions JsonOptions { get; set; }

        /// <summary>
        /// 是否保存到配置管理器（false 时仅导入到内存）
        /// </summary>
        public bool SaveToManager { get; set; } = true;
    }

    /// <summary>
    /// 导出选项 - 控制导出行为
    /// </summary>
    public class ExportOptions
    {
        /// <summary>
        /// 配置类型
        /// </summary>
        public Type ConfigType { get; set; }

        /// <summary>
        /// 导出格式
        /// </summary>
        public ExportFormat Format { get; set; } = ExportFormat.JsonArray;

        /// <summary>
        /// JSON 序列化选项（如果为 null，使用默认选项）
        /// </summary>
        public JsonSerializerOptions JsonOptions { get; set; }

        /// <summary>
        /// 是否包含元数据
        /// </summary>
        public bool IncludeMetadata { get; set; } = true;
    }

    /// <summary>
    /// 冲突解决策略
    /// </summary>
    public enum ConflictResolution
    {
        /// <summary>
        /// 跳过已存在的配置
        /// </summary>
        Skip,

        /// <summary>
        /// 覆盖已存在的配置
        /// </summary>
        Overwrite,

        /// <summary>
        /// 重命名新配置（生成新的 ConfigId）
        /// </summary>
        Rename
    }

    /// <summary>
    /// 导出格式
    /// </summary>
    public enum ExportFormat
    {
        /// <summary>
        /// JSON 数组格式：[{...}, {...}]
        /// </summary>
        JsonArray,

        /// <summary>
        /// JSON 对象格式（包含元数据）：{ "Configs": [...], "Metadata": {...} }
        /// </summary>
        JsonObject,

        /// <summary>
        /// 每个配置一个文件（批量导出时使用）
        /// </summary>
        SingleFile
    }

    /// <summary>
    /// 导入结果 - 继承 OperationResult，复用基础功能
    /// </summary>
    public class ImportResult : OperationResult
    {
        /// <summary>
        /// 成功导入的配置数量
        /// </summary>
        public int ImportedCount { get; set; }

        /// <summary>
        /// 跳过的配置数量
        /// </summary>
        public int SkippedCount { get; set; }

        /// <summary>
        /// 失败的配置数量
        /// </summary>
        public int FailedCount { get; set; }

        /// <summary>
        /// 成功导入的配置列表
        /// </summary>
        public List<IConfig> ImportedConfigs { get; set; } = new List<IConfig>();

        /// <summary>
        /// 错误列表（如果有多个错误）
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        public ImportResult()
        {
            // 初始化时将错误列表放入 ExtendedData，方便统一访问
        }

        public static ImportResult CreateSuccess(string message, int count = 1, List<IConfig> configs = null)
        {
            var result = new ImportResult
            {
                Success = true,
                ErrorCode = 0,
                Message = message ?? "导入成功",
                ImportedCount = count,
                ImportedConfigs = configs ?? new List<IConfig>()
            };
            
            // 将导入的配置数量存储到 ExtendedData
            result.ExtendedData["ImportedCount"] = count;
            if (configs != null)
            {
                result.ExtendedData["ImportedConfigs"] = configs;
            }

            return result;
        }

        public static ImportResult CreateFailure(string message, List<string> errors = null)
        {
            var result = new ImportResult
            {
                Success = false,
                ErrorCode = -1,
                ErrorMessage = message,
                Message = message,
                FailedCount = 1,
                Errors = errors ?? new List<string>()
            };

            // 将错误列表存储到 ExtendedData
            if (errors != null && errors.Count > 0)
            {
                result.ExtendedData["Errors"] = errors;
                result.ErrorMessage = string.Join("; ", errors);
            }

            return result;
        }

        public static ImportResult CreateSkipped(string message)
        {
            var result = new ImportResult
            {
                Success = true,
                ErrorCode = 0,
                Message = message,
                SkippedCount = 1
            };

            result.ExtendedData["SkippedCount"] = 1;

            return result;
        }

        /// <summary>
        /// 合并另一个导入结果（用于批量导入）
        /// </summary>
        public void Merge(ImportResult other)
        {
            if (other == null) return;

            ImportedCount += other.ImportedCount;
            SkippedCount += other.SkippedCount;
            FailedCount += other.FailedCount;
            ImportedConfigs.AddRange(other.ImportedConfigs);
            Errors.AddRange(other.Errors);

            // 如果另一个结果失败，当前结果也应该标记为失败
            if (!other.Success && Success)
            {
                Success = false;
                ErrorCode = other.ErrorCode;
                if (!string.IsNullOrEmpty(other.ErrorMessage))
                {
                    ErrorMessage = string.IsNullOrEmpty(ErrorMessage) 
                        ? other.ErrorMessage 
                        : $"{ErrorMessage}; {other.ErrorMessage}";
                }
            }

            // 更新 ExtendedData
            ExtendedData["ImportedCount"] = ImportedCount;
            ExtendedData["SkippedCount"] = SkippedCount;
            ExtendedData["FailedCount"] = FailedCount;
            if (Errors.Count > 0)
            {
                ExtendedData["Errors"] = Errors;
            }
        }
    }

    /// <summary>
    /// 导出结果 - 继承 OperationResult，复用基础功能
    /// </summary>
    public class ExportResult : OperationResult
    {
        /// <summary>
        /// 导出文件路径
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// 成功导出的配置数量
        /// </summary>
        public int ExportedCount { get; set; }

        /// <summary>
        /// 错误列表（如果有多个错误）
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        public ExportResult()
        {
            // 初始化时将错误列表放入 ExtendedData，方便统一访问
        }

        public static ExportResult CreateSuccess(string message, string filePath, int count)
        {
            var result = new ExportResult
            {
                Success = true,
                ErrorCode = 0,
                Message = message ?? "导出成功",
                FilePath = filePath,
                ExportedCount = count
            };

            // 将导出信息存储到 ExtendedData
            result.ExtendedData["FilePath"] = filePath;
            result.ExtendedData["ExportedCount"] = count;

            return result;
        }

        public static ExportResult CreateFailure(string message, List<string> errors = null)
        {
            var result = new ExportResult
            {
                Success = false,
                ErrorCode = -1,
                ErrorMessage = message,
                Message = message,
                Errors = errors ?? new List<string>()
            };

            // 将错误列表存储到 ExtendedData
            if (errors != null && errors.Count > 0)
            {
                result.ExtendedData["Errors"] = errors;
                result.ErrorMessage = string.Join("; ", errors);
            }

            return result;
        }
    }
}

