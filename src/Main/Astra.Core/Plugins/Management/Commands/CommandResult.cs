using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Management.Commands
{

    /// <summary>
    /// 命令执行结果
    /// </summary>
    public class CommandResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public Exception Error { get; set; }

        public static CommandResult Success(string message)
        {
            return new CommandResult { IsSuccess = true, Message = message };
        }

        public static CommandResult Failure(string message, Exception error = null)
        {
            return new CommandResult { IsSuccess = false, Message = message, Error = error };
        }
    }
}
