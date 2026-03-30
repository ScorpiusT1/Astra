using Astra.Core.Constants;
using Astra.Core.Nodes.Models;
using Astra.Plugins.DataImport.Conversion;
using Astra.Plugins.DataImport.Import;
using Astra.UI.Abstractions.Attributes;
using Astra.UI.PropertyEditors;
using NVHDataBridge.Converters;
using System.ComponentModel.DataAnnotations;

namespace Astra.Plugins.DataImport.Nodes
{
    /// <summary>将 TDMS 中 Signal 组指定通道导出为 WAV（不发布 Raw）。</summary>
    public sealed class TdmsToWavExportNode : Node
    {
        private string _channelName = string.Empty;

        public TdmsToWavExportNode()
        {
            NodeType = "DataImport.TdmsToWav";
            Name = "TDMS 转 WAV";
        }

        [Display(Name = "TDMS 路径", GroupName = "输入", Order = 0, Description = "待转换的 TDMS 文件路径")]
        [Editor(typeof(FilePickerPropertyEditor))]
        [FilePicker(FilePickerMode.Open, "TDMS 文件 (*.tdms)|*.tdms|所有文件 (*.*)|*.*",
            Title = "选择 TDMS 文件")]
        public string SourceTdmsPath { get; set; } = string.Empty;

        [Display(Name = "输出 WAV 路径", GroupName = "输出", Order = 0, Description = "导出 WAV 文件的保存路径")]
        [Editor(typeof(FilePickerPropertyEditor))]
        [FilePicker(FilePickerMode.Save, "WAV 文件 (*.wav)|*.wav|所有文件 (*.*)|*.*",
            Title = "保存 WAV 文件", DefaultExtension = "wav")]
        public string OutputWavPath { get; set; } = string.Empty;

        [Display(Name = "通道", GroupName = "输入", Order = 1, Description = "空或默认项表示 Signal 组内首通道")]
        public string ChannelName
        {
            get => string.IsNullOrEmpty(_channelName) ? AstraSharedConstants.DesignTimeLabels.UseFirstChannelInGroup : _channelName;
            set
            {
                var v = value ?? string.Empty;
                if (string.Equals(v, AstraSharedConstants.DesignTimeLabels.UseFirstChannelInGroup, StringComparison.Ordinal))
                    v = string.Empty;
                if (string.Equals(_channelName, v, StringComparison.Ordinal))
                    return;
                _channelName = v;
                OnPropertyChanged();
            }
        }

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var src = SourceTdmsPath?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(src) || !File.Exists(src))
                return Task.FromResult(ExecutionResult.Failed("请指定有效的 TDMS 文件路径。"));

            var dst = OutputWavPath?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(dst))
                return Task.FromResult(ExecutionResult.Failed("请指定输出 WAV 路径。"));

            if (!new TdmsNvhImporter().CanImport(src))
                return Task.FromResult(ExecutionResult.Failed("源文件不是有效的 TDMS。"));

            NVHDataBridge.Models.NvhMemoryFile file;
            try
            {
                file = NvhTdmsConverter.LoadFromTdms(src);
            }
            catch (Exception ex)
            {
                return Task.FromResult(ExecutionResult.Failed("读取 TDMS 失败: " + ex.Message, ex));
            }

            var chKey = _channelName?.Trim();
            if (string.IsNullOrEmpty(chKey))
                chKey = null;

            try
            {
                NvhToWavExporter.ExportSignalChannel(file, dst, chKey);
            }
            catch (Exception ex)
            {
                return Task.FromResult(ExecutionResult.Failed(ex.Message, ex));
            }

            return Task.FromResult(ExecutionResult.Successful($"已写入 {dst}"));
        }
    }
}
