using System.Windows;
using SimLogger.UI.Services;

namespace SimLogger.UI;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Check for MCP mode
        if (e.Args.Contains("--mcp") || e.Args.Contains("--mcp-mode"))
        {
            // Run as MCP server only (no WPF UI)
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var dataStoragePath = McpServerRunner.GetDataStoragePathFromSettings();

            // Run synchronously to keep the process alive for the MCP server
            McpServerRunner.RunAsync(dataStoragePath).GetAwaiter().GetResult();

            Shutdown();
            return;
        }

        // Normal WPF startup
        base.OnStartup(e);
    }
}
