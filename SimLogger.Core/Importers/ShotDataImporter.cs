using System.IO;
using SimLogger.Core.Models;

namespace SimLogger.Core.Importers;

public static class ShotDataImporter
{
    private static readonly string[] ExpectedHeaders =
    {
        "Carry", "TotalDistance", "BallSpeed", "BackSpin", "SideSpin",
        "HLA", "VLA", "Decent", "DistanceToPin", "PeakHeight",
        "Offline", "rawSpinAxis", "rawCarryGame", "rawCarryLM",
        "Club", "ClubSpeed", "Path", "AoA", "FaceToTarget",
        "FaceToPath", "Lie", "Loft", "DynamicLoft", "CR",
        "HI", "VI", "SmashFactor"
    };

    public static ImportResult ImportFromGSProCsv(string filePath)
    {
        var result = new ImportResult();
        var importTimestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        var now = DateTime.Now;

        string[] lines;
        try
        {
            lines = File.ReadAllLines(filePath);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Failed to read file: {ex.Message}");
            return result;
        }

        if (lines.Length < 2)
        {
            result.Errors.Add("CSV file contains no data rows.");
            return result;
        }

        // Validate header
        var headerColumns = lines[0].Split(',');
        if (headerColumns.Length < 27
            || headerColumns[0].Trim() != "Carry"
            || headerColumns[14].Trim() != "Club")
        {
            result.Errors.Add("File does not appear to be a GSPro CSV export.");
            return result;
        }

        // Check if Tags column exists (column 27, index 27)
        var hasTagsColumn = headerColumns.Length > 27 && headerColumns[27].Trim() == "Tags";

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            var columns = line.Split(',');
            if (columns.Length < 27)
            {
                result.SkippedRows++;
                result.Errors.Add($"Row {i}: expected 27 columns, got {columns.Length}.");
                continue;
            }

            try
            {
                var shot = ParseRow(columns, importTimestamp, i, now);

                // Parse Tags column if present
                if (hasTagsColumn && columns.Length > 27 && !string.IsNullOrWhiteSpace(columns[27]))
                {
                    shot.Tags = columns[27].Trim().Trim('"')
                        .Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim())
                        .Where(t => !string.IsNullOrEmpty(t))
                        .ToList();
                }

                result.Shots.Add(shot);
            }
            catch (Exception ex)
            {
                result.SkippedRows++;
                result.Errors.Add($"Row {i}: {ex.Message}");
            }
        }

        return result;
    }

    private static ShotData ParseRow(string[] cols, string importTimestamp, int rowIndex, DateTime now)
    {
        var carry = ParseDouble(cols[0]);
        var totalDistance = ParseDouble(cols[1]);
        var ballSpeed = ParseDouble(cols[2]);
        var backSpin = ParseDouble(cols[3]);
        var sideSpin = ParseDouble(cols[4]);
        var hla = ParseDouble(cols[5]);
        var vla = ParseDouble(cols[6]);
        var descent = ParseDouble(cols[7]);
        var distanceToPin = ParseDouble(cols[8]);
        var peakHeight = ParseDouble(cols[9]);
        var offline = ParseDouble(cols[10]);
        var rawSpinAxis = ParseDouble(cols[11]);
        // cols[12] rawCarryGame - skip (same as carry)
        // cols[13] rawCarryLM - skip (not used)
        var club = cols[14].Trim();
        var clubSpeed = ParseDouble(cols[15]);
        var path = ParseDouble(cols[16]);
        var aoa = ParseDouble(cols[17]);
        var faceToTarget = ParseDouble(cols[18]);
        var faceToPath = ParseDouble(cols[19]);
        var lie = ParseDouble(cols[20]);
        var loft = ParseDouble(cols[21]);
        var dynamicLoft = ParseDouble(cols[22]);
        // cols[23] CR - skip (not mapped)
        var hi = ParseDouble(cols[24]);
        var vi = ParseDouble(cols[25]);
        var smashFactor = ParseDouble(cols[26]);

        var totalSpin = Math.Sqrt(backSpin * backSpin + sideSpin * sideSpin);

        var shot = new ShotData
        {
            DirectoryName = $"csv-import-{importTimestamp}-{rowIndex}",
            DirectoryTimestamp = now,
            DateTime = now,
            IsRealShot = true,
            ClubData = new ClubData
            {
                ClubName = club,
                Speed = FormatMph(clubSpeed),
                SwingPath = FormatDegrees(path),
                AttackAngle = FormatDegrees(aoa),
                FaceAngle = FormatDegrees(faceToTarget),
                FaceToPath = FormatDegrees(faceToPath),
                Lie = FormatDegrees(lie),
                Loft = FormatDegrees(loft),
                SpinLoft = FormatDegrees(dynamicLoft),
                ImpactPointX = FormatDegrees(hi),
                ImpactPointY = FormatDegrees(vi)
            },
            BallData = new BallData
            {
                Speed = FormatMph(ballSpeed),
                LaunchDirection = FormatDegrees(hla),
                LaunchAngle = FormatDegrees(vla),
                BackSpin = FormatRpm(backSpin),
                SideSpin = FormatRpm(sideSpin),
                TotalSpin = FormatRpm(totalSpin),
                SpinAxis = FormatDegrees(rawSpinAxis)
            },
            FlightData = new FlightData
            {
                Carry = FormatYards(carry),
                TotalDistance = FormatYards(totalDistance),
                Apex = FormatFeet(peakHeight),
                OffLine = FormatYards(offline),
                DescentAngle = FormatDegrees(descent)
            }
        };

        if (smashFactor > 0)
        {
            shot.SmashFactor = $"{smashFactor:F2}";
        }
        else if (clubSpeed > 0 && ballSpeed > 0)
        {
            shot.SmashFactor = $"{ballSpeed / clubSpeed:F2}";
        }

        if (distanceToPin > 0)
        {
            shot.DistanceToTarget = distanceToPin;
        }

        return shot;
    }

    private static double ParseDouble(string value)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return 0;
        return double.TryParse(trimmed, out var result) ? result : 0;
    }

    private static string FormatMph(double value) => value != 0 ? $"{value:F1} mph" : "";
    private static string FormatYards(double value) => value != 0 ? $"{value:F1} yds" : "";
    private static string FormatFeet(double value) => value != 0 ? $"{value:F1} ft" : "";
    private static string FormatDegrees(double value) => value != 0 ? $"{value:F1}°" : "";
    private static string FormatRpm(double value) => value != 0 ? $"{value:F0} rpm" : "";
}

public class ImportResult
{
    public List<ShotData> Shots { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public int SkippedRows { get; set; }
}
