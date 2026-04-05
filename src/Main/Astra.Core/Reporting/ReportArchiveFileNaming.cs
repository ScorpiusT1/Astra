using System.IO;

namespace Astra.Core.Reporting
{
    /// <summary>
    /// 归档与合并报告文件名片段：统一 SN、工站、线体、OK/NG、时间戳的拼接与非法字符处理（不含工况名）。
    /// </summary>
    public static class ReportArchiveFileNaming
    {
        /// <summary>
        /// 将字符串整理为可安全用于文件名的片段；空或仅空白时返回 <c>NA</c>。
        /// </summary>
        public static string SanitizeFileSegment(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "NA";

            var invalid = Path.GetInvalidFileNameChars();
            var chars = value.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (invalid.Contains(chars[i]) || chars[i] == ':' || chars[i] == '\\' || chars[i] == '/')
                    chars[i] = '_';
            }

            var s = new string(chars).Trim();
            if (s.Length == 0)
                return "NA";
            return s.Length > 96 ? s[..96] : s;
        }

        /// <summary>
        /// 构建归档主文件名前缀：SN、工站、线体、OK/NG、时间戳（顺序固定，便于检索）。
        /// </summary>
        public static string BuildFilePrefix(
            string sn,
            string? stationName,
            string? lineName,
            string okNg,
            string fileStamp)
        {
            return $"{SanitizeFileSegment(sn)}_{SanitizeFileSegment(stationName)}_{SanitizeFileSegment(lineName)}_{SanitizeFileSegment(okNg)}_{SanitizeFileSegment(fileStamp)}";
        }
    }
}
