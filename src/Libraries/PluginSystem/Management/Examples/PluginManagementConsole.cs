using Addins.Management;
using System;
using System.Threading.Tasks;

namespace Addins.Management.Examples
{
    /// <summary>
    /// 插件管理控制台应用程序示例
    /// </summary>
    public class PluginManagementConsole
    {
        private readonly PluginManagementToolManager _toolManager;
        private bool _isRunning = true;

        public PluginManagementConsole(PluginManagementToolManager toolManager)
        {
            _toolManager = toolManager;
        }

        public async Task RunAsync()
        {
            Console.WriteLine("Plugin Management Console");
            Console.WriteLine("=========================");
            Console.WriteLine("Type 'help' for available commands, 'exit' to quit");
            Console.WriteLine();

            while (_isRunning)
            {
                Console.Write("plugin> ");
                var input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                    continue;

                var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var command = parts[0].ToLower();

                switch (command)
                {
                    case "exit":
                    case "quit":
                        _isRunning = false;
                        break;
                    case "help":
                        ShowHelp();
                        break;
                    case "tools":
                        _toolManager.ShowAllTools();
                        break;
                    case "tool":
                        if (parts.Length > 1)
                        {
                            var toolName = parts[1];
                            var toolArgs = parts.Length > 2 ? parts[2..] : new string[0];
                            
                            if (toolArgs.Length > 0 && toolArgs[0].ToLower() == "help")
                            {
                                _toolManager.ShowToolHelp(toolName);
                            }
                            else
                            {
                                await _toolManager.ExecuteToolAsync(toolName, toolArgs);
                            }
                        }
                        else
                        {
                            Console.WriteLine("Usage: tool <name> [args...]");
                        }
                        break;
                    default:
                        Console.WriteLine($"Unknown command: {command}");
                        Console.WriteLine("Type 'help' for available commands");
                        break;
                }

                Console.WriteLine();
            }

            Console.WriteLine("Goodbye!");
        }

        private void ShowHelp()
        {
            Console.WriteLine("Available Commands:");
            Console.WriteLine("  help                    Show this help message");
            Console.WriteLine("  tools                   List all available tools");
            Console.WriteLine("  tool <name> [args...]   Execute a management tool");
            Console.WriteLine("  tool <name> help        Show help for specific tool");
            Console.WriteLine("  exit                    Exit the console");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  tool debug              Show plugin debug information");
            Console.WriteLine("  tool config list        List all plugin configurations");
            Console.WriteLine("  tool config get myplugin enabled  Get plugin setting");
        }
    }

    ///// <summary>
    ///// 插件管理控制台应用程序入口点
    ///// </summary>
    //public class Program
    //{
    //    public static async Task Main(string[] args)
    //    {
    //        // 这里需要从实际的插件系统获取服务
    //        // 在实际应用中，这些服务应该从依赖注入容器中获取
            
    //        Console.WriteLine("Plugin Management Console Example");
    //        Console.WriteLine("=================================");
    //        Console.WriteLine();
    //        Console.WriteLine("Note: This is a demonstration of how to use the");
    //        Console.WriteLine("Plugin Management Tools. In a real application,");
    //        Console.WriteLine("you would initialize the plugin system first.");
    //        Console.WriteLine();

    //        // 模拟创建工具管理器
    //        // var toolManager = PluginManagementToolFactory.CreateDefaultManager(host, lifecycleManager, messageBus, configStore);
            
    //        // var console = new PluginManagementConsole(toolManager);
    //        // await console.RunAsync();

    //        Console.WriteLine("Demo completed. Press any key to exit...");
    //        Console.ReadKey();
    //    }
    //}
}
