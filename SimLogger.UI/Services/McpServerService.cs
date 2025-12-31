using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using SimLogger.Core.Data;
using SimLogger.Core.Mcp;
using SimLogger.Core.Mcp.Tools;

namespace SimLogger.UI.Services;

/// <summary>
/// Service for running the MCP server in standalone mode (when invoked with --mcp-mode).
/// This is used by MCP clients like Claude Desktop to query the shot database.
/// </summary>
public static class McpServerRunner
{
    /// <summary>
    /// Runs the MCP server with stdio transport. This method blocks until the server is stopped.
    /// </summary>
    /// <param name="dataStoragePath">Optional custom data storage path for the database.</param>
    public static async Task RunAsync(string? dataStoragePath = null)
    {
        // Create database context
        var dbContext = new DatabaseContext(dataStoragePath);
        await dbContext.InitializeDatabaseAsync();

        // Create data provider
        var dataProvider = new McpShotDataProvider(dbContext);

        // Build and run the MCP server
        var builder = Host.CreateApplicationBuilder();

        // Register our data provider as a singleton so tools can access it
        builder.Services.AddSingleton(dataProvider);

        // Configure MCP server with stdio transport
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly(typeof(ShotQueryTools).Assembly);

        var host = builder.Build();
        await host.RunAsync();

        // Clean up
        dbContext.Dispose();
    }

    /// <summary>
    /// Loads settings to get the data storage path, if configured.
    /// </summary>
    public static string? GetDataStoragePathFromSettings()
    {
        try
        {
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var settingsPath = Path.Combine(documentsPath, "SimLogger", "settings.json");

            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                var settings = System.Text.Json.JsonSerializer.Deserialize<McpSettings>(json);
                return settings?.DataStoragePath;
            }
        }
        catch
        {
            // Ignore settings load errors, use default path
        }

        return null;
    }

    private class McpSettings
    {
        public string? DataStoragePath { get; set; }
    }
}
