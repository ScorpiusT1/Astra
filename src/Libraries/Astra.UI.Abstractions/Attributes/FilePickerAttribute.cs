namespace Astra.UI.Abstractions.Attributes
{
    /// <summary>
    /// 文件对话框模式。
    /// </summary>
    public enum FilePickerMode
    {
        Open,
        Save
    }

    /// <summary>
    /// 标注在 string 或 List&lt;string&gt; 属性上，使属性编辑器显示文件选择对话框。
    /// 当标注在 List&lt;string&gt; 属性上时，应配合 <see cref="Multiselect"/> = true 使用。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class FilePickerAttribute : Attribute
    {
        /// <summary>对话框模式：打开或保存。</summary>
        public FilePickerMode Mode { get; }

        /// <summary>
        /// 文件过滤器，格式与 <see cref="Microsoft.Win32.FileDialog.Filter"/> 一致。
        /// 例如 <c>"TDMS 文件 (*.tdms)|*.tdms|所有文件 (*.*)|*.*"</c>。
        /// </summary>
        public string Filter { get; }

        /// <summary>对话框标题（留空则使用默认标题）。</summary>
        public string Title { get; set; }

        /// <summary>默认扩展名（仅 Save 模式生效，不含点号，如 <c>"wav"</c>）。</summary>
        public string DefaultExtension { get; set; }

        /// <summary>是否允许多选（仅 Open 模式生效）。设为 true 时属性类型应为 List&lt;string&gt;。</summary>
        public bool Multiselect { get; set; }

        public FilePickerAttribute(FilePickerMode mode = FilePickerMode.Open, string filter = "所有文件 (*.*)|*.*")
        {
            Mode = mode;
            Filter = filter ?? "所有文件 (*.*)|*.*";
        }
    }
}
