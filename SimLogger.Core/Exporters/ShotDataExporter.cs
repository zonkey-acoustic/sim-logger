using System.Text;
using System.Text.Json;
using SimLogger.Core.Models;

namespace SimLogger.Core.Exporters;

public enum ExportFormat
{
    GSPro,
    ShotPattern
}

public class ShotDataExporter
{
    public static void ExportToGSProCsv(List<ShotData> shots, string outputPath, bool silent = false)
    {
        try
        {
            using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);

            // Write GSPro-compatible header (27 columns)
            writer.WriteLine(string.Join(",",
                "Carry",
                "TotalDistance",
                "BallSpeed",
                "BackSpin",
                "SideSpin",
                "HLA",
                "VLA",
                "Decent",
                "DistanceToPin",
                "PeakHeight",
                "Offline",
                "rawSpinAxis",
                "rawCarryGame",
                "rawCarryLM",
                "Club",
                "ClubSpeed",
                "Path",
                "AoA",
                "FaceToTarget",
                "FaceToPath",
                "Lie",
                "Loft",
                "DynamicLoft",
                "CR",
                "HI",
                "VI",
                "SmashFactor"
            ));

            // Write data rows with raw numeric values
            foreach (var shot in shots)
            {
                var carry = ExtractNumericValue(shot.FlightData?.Carry);
                var totalDistance = ExtractNumericValue(shot.FlightData?.TotalDistance);
                var ballSpeed = ExtractNumericValue(shot.BallData?.Speed);
                var backSpin = ExtractNumericValue(shot.BallData?.BackSpin);
                var sideSpin = ExtractNumericValue(shot.BallData?.SideSpin);
                var hla = ExtractNumericValue(shot.BallData?.LaunchDirection);
                var vla = ExtractNumericValue(shot.BallData?.LaunchAngle);
                var descent = ExtractNumericValue(shot.FlightData?.DescentAngle);
                var distanceToPin = shot.DistanceToTarget?.ToString() ?? "";
                var peakHeight = ExtractNumericValue(shot.FlightData?.Apex);
                var offline = ExtractNumericValue(shot.FlightData?.OffLine);
                var rawSpinAxis = ExtractNumericValue(shot.BallData?.SpinAxis);
                var rawCarryGame = carry; // Same as carry
                var club = shot.ClubData?.ClubName ?? "";
                var clubSpeed = ExtractNumericValue(shot.ClubData?.Speed);
                var path = ExtractNumericValue(shot.ClubData?.SwingPath);
                var aoa = ExtractNumericValue(shot.ClubData?.AttackAngle);
                var faceToTarget = ExtractNumericValue(shot.ClubData?.FaceAngle);
                var faceToPath = ExtractNumericValue(shot.ClubData?.FaceToPath);
                var lie = ExtractNumericValue(shot.ClubData?.Lie);
                var loft = ExtractNumericValue(shot.ClubData?.Loft);
                var dynamicLoft = ExtractNumericValue(shot.ClubData?.SpinLoft);
                var hi = ExtractNumericValue(shot.ClubData?.ImpactPointX);
                var vi = ExtractNumericValue(shot.ClubData?.ImpactPointY);
                var smashFactor = ExtractNumericValue(shot.SmashFactor);

                writer.WriteLine(string.Join(",",
                    FormatGSProValue(carry),
                    FormatGSProValue(totalDistance),
                    FormatGSProValue(ballSpeed),
                    FormatGSProValue(backSpin),
                    FormatGSProValue(sideSpin),
                    FormatGSProValue(hla),
                    FormatGSProValue(vla),
                    FormatGSProValue(descent),
                    distanceToPin,
                    FormatGSProValue(peakHeight),
                    FormatGSProValue(offline),
                    FormatGSProValue(rawSpinAxis),
                    FormatGSProValue(rawCarryGame),
                    "", // rawCarryLM - not available, leave blank
                    club,
                    FormatGSProValue(clubSpeed),
                    FormatGSProValue(path),
                    FormatGSProValue(aoa),
                    FormatGSProValue(faceToTarget),
                    FormatGSProValue(faceToPath),
                    FormatGSProValue(lie),
                    FormatGSProValue(loft),
                    FormatGSProValue(dynamicLoft),
                    "", // CR - not available, leave blank
                    FormatGSProValue(hi),
                    FormatGSProValue(vi),
                    FormatGSProValue(smashFactor)
                ));
            }

            if (!silent) Console.WriteLine($"Exported {shots.Count} shot(s) to GSPro CSV: {outputPath}");
        }
        catch (Exception ex)
        {
            if (!silent) Console.WriteLine($"Error exporting to GSPro CSV: {ex.Message}");
        }
    }

    private static string FormatGSProValue(double value)
    {
        // Return empty string for zero values (matches GSPro behavior for missing data)
        // Otherwise return the value without trailing zeros
        return value == 0 ? "" : value.ToString("G");
    }

    public static void ExportToShotPatternCsv(List<ShotData> shots, string outputPath, bool silent = false)
    {
        try
        {
            using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);

            // Write Shot Pattern header (5 columns)
            writer.WriteLine("Club,Type,Target,Total,Side");

            // Write data rows
            foreach (var shot in shots)
            {
                var clubName = shot.ClubData?.ClubName ?? "";
                var club = GetClubAbbreviation(clubName);
                var type = GetShotType(clubName);
                var target = shot.DistanceToTarget?.ToString("F0") ?? "";
                var total = ExtractNumericValue(shot.FlightData?.TotalDistance);
                var side = ExtractNumericValue(shot.FlightData?.OffLine);

                writer.WriteLine(string.Join(",",
                    club,
                    type,
                    target,
                    total > 0 ? total.ToString("F0") : "",
                    side != 0 ? side.ToString("F1") : "0"
                ));
            }

            if (!silent) Console.WriteLine($"Exported {shots.Count} shot(s) to Shot Pattern CSV: {outputPath}");
        }
        catch (Exception ex)
        {
            if (!silent) Console.WriteLine($"Error exporting to Shot Pattern CSV: {ex.Message}");
        }
    }

    private static string GetClubAbbreviation(string clubName)
    {
        if (string.IsNullOrEmpty(clubName))
            return "";

        var name = clubName.Trim().ToUpperInvariant();

        // Driver
        if (name.Contains("DRIVER") || name == "DR")
            return "Dr";

        // Woods
        if (name.Contains("WOOD") || name.EndsWith("W"))
        {
            if (name.Contains("3") || name.StartsWith("3"))
                return "3W";
            if (name.Contains("5") || name.StartsWith("5"))
                return "5W";
            if (name.Contains("7") || name.StartsWith("7"))
                return "7W";
            return "W";
        }

        // Hybrids
        if (name.Contains("HYBRID") || name.Contains("HY") || name.EndsWith("H"))
        {
            if (name.Contains("3") || name.StartsWith("3"))
                return "3H";
            if (name.Contains("4") || name.StartsWith("4"))
                return "4H";
            if (name.Contains("5") || name.StartsWith("5"))
                return "5H";
            if (name.Contains("6") || name.StartsWith("6"))
                return "6H";
            return "H";
        }

        // Wedges (check before irons since they contain numbers too)
        if (name.Contains("LOB") || name == "LW")
            return "Lw";
        if (name.Contains("SAND") || name == "SW")
            return "Sw";
        if (name.Contains("GAP") || name == "GW")
            return "Gw";
        if (name.Contains("PITCHING") || name == "PW")
            return "Pw";

        // Irons
        if (name.Contains("IRON") || name.EndsWith("I"))
        {
            if (name.Contains("3") || name.StartsWith("3"))
                return "3i";
            if (name.Contains("4") || name.StartsWith("4"))
                return "4i";
            if (name.Contains("5") || name.StartsWith("5"))
                return "5i";
            if (name.Contains("6") || name.StartsWith("6"))
                return "6i";
            if (name.Contains("7") || name.StartsWith("7"))
                return "7i";
            if (name.Contains("8") || name.StartsWith("8"))
                return "8i";
            if (name.Contains("9") || name.StartsWith("9"))
                return "9i";
        }

        // Putter
        if (name.Contains("PUTTER") || name == "PT")
            return "Pt";

        // Return original if no match
        return clubName;
    }

    private static string GetShotType(string clubName)
    {
        if (string.IsNullOrEmpty(clubName))
            return "Approach";

        var name = clubName.Trim().ToUpperInvariant();

        // Tee shots: Driver, Woods, Hybrids
        if (name.Contains("DRIVER") || name == "DR" ||
            name.Contains("WOOD") || name.EndsWith("W") ||
            name.Contains("HYBRID") || name.Contains("HY") || name.EndsWith("H"))
        {
            return "Tee";
        }

        // Putt
        if (name.Contains("PUTTER") || name == "PT")
        {
            return "Putt";
        }

        // Everything else is Approach (irons, wedges)
        return "Approach";
    }

    public static void ExportToJson(List<ShotData> shots, string outputPath, bool silent = false)
    {
        try
        {
            var data = new
            {
                ExportDate = DateTime.Now,
                TotalShots = shots.Count,
                RealShots = shots.Count(s => s.IsRealShot),
                Shots = shots
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(data, options);
            File.WriteAllText(outputPath, json, Encoding.UTF8);

            if (!silent) Console.WriteLine($"Exported data to JSON: {outputPath}");
        }
        catch (Exception ex)
        {
            if (!silent) Console.WriteLine($"Error exporting to JSON: {ex.Message}");
        }
    }

    public static void PrintSummary(List<ShotData> shots)
    {
        Console.WriteLine("\n=== Shot Summary ===");
        Console.WriteLine($"Total shots: {shots.Count}");
        Console.WriteLine($"Real shots: {shots.Count(s => s.IsRealShot)}");

        if (shots.Count > 0)
        {
            var validTimestamps = shots.Where(s => s.DirectoryTimestamp != DateTime.MinValue).ToList();

            if (validTimestamps.Count > 0)
            {
                Console.WriteLine($"\nFirst shot: {validTimestamps.First().DirectoryTimestamp:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"Last shot: {validTimestamps.Last().DirectoryTimestamp:yyyy-MM-dd HH:mm:ss}");
            }

            // Club statistics
            var clubCounts = shots
                .Where(s => !string.IsNullOrEmpty(s.ClubData?.ClubName))
                .GroupBy(s => s.ClubData!.ClubName)
                .OrderByDescending(g => g.Count())
                .Take(5);

            Console.WriteLine("\nTop clubs used:");
            foreach (var group in clubCounts)
            {
                Console.WriteLine($"  {group.Key}: {group.Count()} shots");
            }

            // Average carry distance (for shots with valid data)
            var carryShotsWithData = shots
                .Where(s => !string.IsNullOrEmpty(s.FlightData?.Carry))
                .Select(s => ExtractNumericValue(s.FlightData!.Carry))
                .Where(c => c > 0)
                .ToList();

            if (carryShotsWithData.Count > 0)
            {
                Console.WriteLine($"\nAverage carry: {carryShotsWithData.Average():F1} yards ({carryShotsWithData.Count} shots with data)");
            }

            // Average ball speed
            var ballSpeedData = shots
                .Where(s => !string.IsNullOrEmpty(s.BallData?.Speed))
                .Select(s => ExtractNumericValue(s.BallData!.Speed))
                .Where(s => s > 0)
                .ToList();

            if (ballSpeedData.Count > 0)
            {
                Console.WriteLine($"Average ball speed: {ballSpeedData.Average():F1} mph ({ballSpeedData.Count} shots with data)");
            }
        }
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    public static double ExtractNumericValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        var numbers = new string(value.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());

        if (double.TryParse(numbers, out var result))
        {
            return result;
        }

        return 0;
    }
}
