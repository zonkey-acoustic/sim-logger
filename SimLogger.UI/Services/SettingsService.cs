using System.IO;
using System.Text.Json;

namespace SimLogger.UI.Services;

public class SettingsService
{
    private readonly string _settingsPath;

    public string? DataStoragePath { get; set; }
    public string? GSProPath { get; set; }
    public List<string>? ColumnOrder { get; set; }
    public List<string>? HiddenColumns { get; set; }

    public SettingsService()
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var simLoggerPath = Path.Combine(documentsPath, "SimLogger");
        Directory.CreateDirectory(simLoggerPath);
        _settingsPath = Path.Combine(simLoggerPath, "settings.json");

        LoadSettings();
    }

    public string GetDataStoragePath()
    {
        if (!string.IsNullOrEmpty(DataStoragePath) && Directory.Exists(DataStoragePath))
            return DataStoragePath;

        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documentsPath, "SimLogger");
    }

    public string? GetGSProDatabasePath()
    {
        if (!string.IsNullOrEmpty(GSProPath))
        {
            var customDbPath = Path.Combine(GSProPath, "GSPro.db");
            if (File.Exists(customDbPath))
                return customDbPath;
        }

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low";
        var defaultPath = Path.Combine(appDataPath, "GSPro", "GSPro", "GSPro.db");
        if (File.Exists(defaultPath))
            return defaultPath;

        return null;
    }

    public static string GetDefaultGSProPath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low";
        return Path.Combine(appDataPath, "GSPro", "GSPro");
    }

    public void SaveSettings()
    {
        try
        {
            var settings = new AppSettings
            {
                DataStoragePath = DataStoragePath,
                GSProPath = GSProPath,
                ColumnOrder = ColumnOrder,
                HiddenColumns = HiddenColumns
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
        }
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);

                if (settings != null)
                {
                    DataStoragePath = settings.DataStoragePath;
                    GSProPath = settings.GSProPath;
                    ColumnOrder = settings.ColumnOrder;
                    HiddenColumns = settings.HiddenColumns;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
        }
    }
}

public class AppSettings
{
    public string? DataStoragePath { get; set; }
    public string? GSProPath { get; set; }
    public List<string>? ColumnOrder { get; set; }
    public List<string>? HiddenColumns { get; set; }
}
