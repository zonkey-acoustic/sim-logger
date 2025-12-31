using System.Text.RegularExpressions;
using System.Globalization;
using SimLogger.Core.Models;

namespace SimLogger.Core.Parsers;

public class LogParser
{
    private static readonly Regex LogLineRegex = new(
        @"^(\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}\.\d+)\|(\w+)\|(\d+)\|(.+)$",
        RegexOptions.Compiled
    );

    public static List<LogEntry> ParseLogFile(string filePath, bool silent = false)
    {
        var entries = new List<LogEntry>();

        if (!File.Exists(filePath))
        {
            if (!silent) Console.WriteLine($"Log file not found: {filePath}");
            return entries;
        }

        foreach (var line in File.ReadLines(filePath))
        {
            var entry = ParseLogLine(line);
            if (entry != null)
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    public static LogEntry? ParseLogLine(string line)
    {
        var match = LogLineRegex.Match(line);
        if (!match.Success)
            return null;

        if (!DateTime.TryParseExact(
            match.Groups[1].Value,
            "yyyy-MM-dd HH:mm:ss.ffff",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var timestamp))
        {
            return null;
        }

        return new LogEntry
        {
            Timestamp = timestamp,
            Level = match.Groups[2].Value,
            ThreadId = int.Parse(match.Groups[3].Value),
            Message = match.Groups[4].Value,
            RawLine = line
        };
    }

    public static List<LogEntry> ParseLogDirectory(string directoryPath, bool silent = false)
    {
        var allEntries = new List<LogEntry>();

        if (!Directory.Exists(directoryPath))
        {
            if (!silent) Console.WriteLine($"Log directory not found: {directoryPath}");
            return allEntries;
        }

        var logFiles = Directory.GetFiles(directoryPath, "LogFile-*.txt")
            .OrderBy(f => f)
            .ToList();

        if (!silent) Console.WriteLine($"Found {logFiles.Count} log file(s)");

        foreach (var logFile in logFiles)
        {
            if (!silent) Console.WriteLine($"Parsing: {Path.GetFileName(logFile)}");
            var entries = ParseLogFile(logFile, silent);
            allEntries.AddRange(entries);
        }

        return allEntries;
    }
}
