using Python.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PythonWrapper.Utils
{
    // ═══════════════════════════════════════════════════════════════════
    // 修复点② — PythonException 统一封装工具方法（新增，避免到处重复判断）
    // 建议放在 PythonResult.cs 底部或单独的 PythonExceptionHelper.cs
    // ═══════════════════════════════════════════════════════════════════
    internal static class PythonExceptionHelper
    {
        /// <summary>
        /// 判断 PythonException 是否为 KeyboardInterrupt（取消信号）
        /// PythonNet 3.x 通过 pex.Type.ToString() 获取异常类型名
        /// </summary>
        public static bool IsKeyboardInterrupt(PythonException pex)
        {
            try
            {
                // pex.Type 是 PyObject，ToString() 返回如 "<class 'KeyboardInterrupt'>"
                return pex.Type?.ToString()?.Contains("KeyboardInterrupt") == true;
            }
            catch
            {
                // 兜底：检查 Message
                return pex.Message?.Contains("KeyboardInterrupt") == true;
            }
        }

        /// <summary>
        /// 提取 Python 异常的完整错误信息（含 Traceback）
        /// </summary>
        public static string GetFullMessage(PythonException pex)
        {
            try { return pex.Format(); }         // 含完整 Traceback
            catch { return pex.Message ?? "未知 Python 异常"; }
        }

        /// <summary>
        /// 提取 Python 异常类型名（如 "ValueError", "KeyboardInterrupt"）
        /// </summary>
        public static string GetTypeName(PythonException pex)
        {
            try
            {
                // pex.Type.ToString() → "<class 'ValueError'>"
                // 用正则提取引号内的类型名
                var raw = pex.Type?.ToString() ?? string.Empty;
                var match = System.Text.RegularExpressions.Regex
                    .Match(raw, @"'(.+?)'");
                return match.Success ? match.Groups[1].Value : raw;
            }
            catch { return "UnknownPythonException"; }
        }
    }
}
