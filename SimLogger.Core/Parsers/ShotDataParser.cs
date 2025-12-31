using System.Globalization;
using System.Text.Json;
using SimLogger.Core.Models;

namespace SimLogger.Core.Parsers;

public class ShotDataParser
{
    public static List<ShotData> ParseShotsDirectory(string shotsDirectory, bool silent = false)
    {
        var shots = new List<ShotData>();

        if (!Directory.Exists(shotsDirectory))
        {
            if (!silent) Console.WriteLine($"Shots directory not found: {shotsDirectory}");
            return shots;
        }

        var shotDirectories = Directory.GetDirectories(shotsDirectory)
            .OrderBy(d => d)
            .ToList();

        if (!silent) Console.WriteLine($"Found {shotDirectories.Count} shot director{(shotDirectories.Count == 1 ? "y" : "ies")}");

        foreach (var shotDir in shotDirectories)
        {
            var shotDataPath = Path.Combine(shotDir, "ShotData.json");

            if (!File.Exists(shotDataPath))
            {
                if (!silent) Console.WriteLine($"Warning: ShotData.json not found in {Path.GetFileName(shotDir)}");
                continue;
            }

            try
            {
                var shotData = ParseShotDataFile(shotDataPath);
                if (shotData != null)
                {
                    shotData.DirectoryName = Path.GetFileName(shotDir);
                    shotData.DirectoryTimestamp = ParseDirectoryTimestamp(shotData.DirectoryName);
                    shots.Add(shotData);
                }
            }
            catch (Exception ex)
            {
                if (!silent) Console.WriteLine($"Error parsing {Path.GetFileName(shotDir)}: {ex.Message}");
            }
        }

        // Assign shot numbers
        for (int i = 0; i < shots.Count; i++)
        {
            shots[i].ShotNumber = i + 1;
        }

        return shots;
    }

    private static ShotData? ParseShotDataFile(string filePath)
    {
        var json = File.ReadAllText(filePath);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        return JsonSerializer.Deserialize<ShotData>(json, options);
    }

    public static DateTime ParseDirectoryTimestamp(string directoryName)
    {
        // Format: 2025-12-03-215441552
        // Parse: YYYY-MM-DD-HHMMSSmmm
        try
        {
            if (directoryName.Length >= 19)
            {
                var datePart = directoryName.Substring(0, 10); // 2025-12-03
                var timePart = directoryName.Substring(11); // 215441552

                if (timePart.Length >= 9)
                {
                    var hour = timePart.Substring(0, 2);
                    var minute = timePart.Substring(2, 2);
                    var second = timePart.Substring(4, 2);
                    var millisecond = timePart.Substring(6, 3);

                    var dateTimeString = $"{datePart} {hour}:{minute}:{second}.{millisecond}";

                    if (DateTime.TryParseExact(
                        dateTimeString,
                        "yyyy-MM-dd HH:mm:ss.fff",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var result))
                    {
                        return result;
                    }
                }
            }
        }
        catch
        {
            // Fall through to return DateTime.MinValue
        }

        return DateTime.MinValue;
    }
}
