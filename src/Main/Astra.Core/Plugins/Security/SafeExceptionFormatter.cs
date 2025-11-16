using System;
using System.Text.RegularExpressions;

namespace Astra.Core.Plugins.Security
{
	/// <summary>
	/// 异常信息脱敏工具：移除文件路径与可能的敏感片段。
	/// </summary>
	public static class SafeExceptionFormatter
	{
		private static readonly Regex PathRegex = new Regex(@"([A-Za-z]:\\|/)[^\s:]+", RegexOptions.Compiled);

		public static string Sanitize(string message)
		{
			if (string.IsNullOrEmpty(message)) return message;
			return PathRegex.Replace(message, "[path]");
		}

		public static Exception Sanitize(Exception ex)
		{
			if (ex == null) return null;
			var msg = Sanitize(ex.Message);
			return new Exception(msg);
		}
	}
}

