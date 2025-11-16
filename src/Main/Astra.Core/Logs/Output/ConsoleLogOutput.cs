using System;

namespace Astra.Core.Logs.Output
{
    /// <summary>
    /// 控制台日志输出实现
    /// 将日志输出到控制台，支持颜色显示
    /// </summary>
    public class ConsoleLogOutput : ILogOutput
    {
        private readonly bool _useColor;
        private readonly object _lockObject = new object();

        public ConsoleLogOutput(bool useColor = true)
        {
            _useColor = useColor;
        }

        public void Write(LogEntry entry)
        {
            lock (_lockObject)
            {
                if (_useColor)
                {
                    var originalColor = Console.ForegroundColor;
                    Console.ForegroundColor = GetColorForLevel(entry.Level);
                    // 直接输出格式化后的消息（消息已经在 Logger 中格式化）
                    Console.WriteLine(entry.Message);
                    Console.ForegroundColor = originalColor;
                }
                else
                {
                    Console.WriteLine(entry.Message);
                }
            }
        }

        public void Flush()
        {
            // 控制台输出无需刷新
        }

        public void Dispose()
        {
            // 控制台输出无需释放资源
        }

        /// <summary>
        /// 获取日志级别对应的控制台颜色
        /// </summary>
        private ConsoleColor GetColorForLevel(string level)
        {
            return level switch
            {
                "DEBUG" => ConsoleColor.Gray,
                "INFO" => ConsoleColor.White,
                "WARNING" => ConsoleColor.Yellow,
                "ERROR" => ConsoleColor.Red,
                "CRITICAL" => ConsoleColor.DarkRed,
                _ => ConsoleColor.White
            };
        }
    }
}

