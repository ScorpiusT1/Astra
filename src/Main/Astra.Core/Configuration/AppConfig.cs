using Astra.Core.Foundation.Common;
using Astra.Core.Devices;
using System;
using System.Collections.Generic;

namespace Astra.Core.Configuration
{
    /// <summary>
    /// 应用程序基础配置示例
    /// 演示如何使用 BaseConfig 创建基础配置类
    /// </summary>
    public class AppConfig : BaseConfig
    {
        private string _appName;
        private string _appVersion;
        private string _language = "zh-CN";
        private string _theme = "Light";
        private int _maxLogFileSize = 10; // MB
        private bool _enableAutoSave = true;

        /// <summary>
        /// 配置类型
        /// </summary>
        public override string ConfigType => "Application";

        /// <summary>
        /// 应用程序名称
        /// </summary>
        public string AppName
        {
            get => _appName;
            set => SetProperty(ref _appName, value, nameof(AppName));
        }

        /// <summary>
        /// 应用程序版本
        /// </summary>
        public string AppVersion
        {
            get => _appVersion;
            set => SetProperty(ref _appVersion, value, nameof(AppVersion));
        }

        /// <summary>
        /// 语言设置
        /// </summary>
        public string Language
        {
            get => _language;
            set => SetProperty(ref _language, value, nameof(Language));
        }

        /// <summary>
        /// 主题设置
        /// </summary>
        public string Theme
        {
            get => _theme;
            set => SetProperty(ref _theme, value, nameof(Theme));
        }

        /// <summary>
        /// 最大日志文件大小（MB）
        /// </summary>
        public int MaxLogFileSize
        {
            get => _maxLogFileSize;
            set => SetProperty(ref _maxLogFileSize, value, nameof(MaxLogFileSize));
        }

        /// <summary>
        /// 是否启用自动保存
        /// </summary>
        public bool EnableAutoSave
        {
            get => _enableAutoSave;
            set => SetProperty(ref _enableAutoSave, value, nameof(EnableAutoSave));
        }

        /// <summary>
        /// 生成配置ID
        /// </summary>
        protected override string GenerateConfigId()
        {
            return "AppConfig_Default";
        }

        /// <summary>
        /// 克隆配置
        /// </summary>
        public override IConfig Clone()
        {
            var clone = new AppConfig
            {
                ConfigId = ConfigId,
                ConfigName = ConfigName,
                IsEnabled = IsEnabled,
                Version = Version,
                CreatedAt = CreatedAt,
                ModifiedAt = ModifiedAt,
                AppName = AppName,
                AppVersion = AppVersion,
                Language = Language,
                Theme = Theme,
                MaxLogFileSize = MaxLogFileSize,
                EnableAutoSave = EnableAutoSave
            };
            return clone;
        }

        /// <summary>
        /// 验证配置
        /// </summary>
        public override OperationResult<bool> Validate()
        {
            var baseResult = base.Validate();
            if (!baseResult.Success)
                return baseResult;

            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(AppName))
            {
                errors.Add("应用程序名称不能为空");
            }

            if (MaxLogFileSize < 1 || MaxLogFileSize > 1000)
            {
                errors.Add("最大日志文件大小必须在 1-1000 MB 之间");
            }

            if (errors.Count > 0)
            {
                var errorMessage = $"配置验证失败，发现 {errors.Count} 个问题：" + Environment.NewLine + string.Join(Environment.NewLine + "  - ", errors);
                return OperationResult<bool>.Fail(errorMessage, ErrorCodes.InvalidConfig);
            }

            return OperationResult<bool>.Succeed(true, "配置验证通过");
        }
    }
}

