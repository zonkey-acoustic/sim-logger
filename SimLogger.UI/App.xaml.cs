using System.Windows;
using SimLogger.UI.Services;

namespace SimLogger.UI;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        // Check for MCP mode
        if (e.Args.Contains("--mcp") || e.Args.Contains("--mcp-mode"))
        {
            // Run as MCP server only (no WPF UI)
            // This prevents the normal WPF startup
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            try
            {
                var dataStoragePath = McpServerRunner.GetDataStoragePathFromSettings();
                await McpServerRunner.RunAsync(dataStoragePath);
            }
            finally
            {
                Shutdown();
            }
            return;
        }

        // Normal WPF startup
        base.OnStartup(e);
    }
}
