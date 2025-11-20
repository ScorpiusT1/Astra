namespace Astra.Plugins.DataAcquisition.Configs
{
    #region 传感器库管理

    #endregion

    #region 验证结果

    public class ValidationResult
    {
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();

        public bool IsValid => Errors.Count == 0;
        public bool HasWarnings => Warnings.Count > 0;

        public void AddError(string message) => Errors.Add(message);
        public void AddWarning(string message) => Warnings.Add(message);

        public override string ToString()
        {
            var lines = new List<string>();
            if (Errors.Count > 0)
            {
                lines.Add("错误:");
                lines.AddRange(Errors.Select(e => $"  ✗ {e}"));
            }
            if (Warnings.Count > 0)
            {
                lines.Add("警告:");
                lines.AddRange(Warnings.Select(w => $"  ⚠ {w}"));
            }
            return lines.Count > 0 ? string.Join(Environment.NewLine, lines) : "  ✓ 配置有效";
        }
    }

    #endregion
}
