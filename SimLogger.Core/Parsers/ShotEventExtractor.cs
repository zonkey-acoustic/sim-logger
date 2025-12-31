using System.Text.RegularExpressions;
using System.Text.Json;
using SimLogger.Core.Models;

namespace SimLogger.Core.Parsers;

public class ShotEventExtractor
{
    private static readonly Regex BallSpinRegex = new(@"Ball spin took ([\d.]+) ms", RegexOptions.Compiled);
    private static readonly Regex ClubDetectionRegex = new(@"Club detection took ([\d.]+) ms", RegexOptions.Compiled);
    private static readonly Regex ShotAnalysesRegex = new(@"ShotAnalyses took ([\d.]+) ms", RegexOptions.Compiled);
    private static readonly Regex TotalTimeRegex = new(@"Total took ([\d.]+) ms", RegexOptions.Compiled);
    private static readonly Regex BytesSentRegex = new(@"Sent (\d+) bytes to GSPro API", RegexOptions.Compiled);
    private static readonly Regex SendingToGameRegex = new(@"Sending shot to game: \[ball=(\w+)\], \[club=(\w+)\]", RegexOptions.Compiled);
    private static readonly Regex HandednessRegex = new(@"Detected a (right|left) handed (iron|wood|driver|putter|wedge)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex GsProDataRegex = new(@"GSPro read data: (.+)$", RegexOptions.Compiled);

    public static List<ShotEvent> ExtractShotEvents(List<LogEntry> logEntries)
    {
        var shots = new List<ShotEvent>();
        ShotEvent? currentShot = null;
        GsProPlayerInfo? latestGsProInfo = null;

        foreach (var entry in logEntries)
        {
            // Extract GSPro player information
            var gsproMatch = GsProDataRegex.Match(entry.Message);
            if (gsproMatch.Success)
            {
                latestGsProInfo = ParseGsProData(gsproMatch.Groups[1].Value);
            }

            // Check for shot start
            var sendingMatch = SendingToGameRegex.Match(entry.Message);
            if (sendingMatch.Success)
            {
                if (currentShot != null)
                {
                    shots.Add(currentShot);
                }

                currentShot = new ShotEvent
                {
                    Timestamp = entry.Timestamp,
                    HasBallData = sendingMatch.Groups[1].Value.ToLower() == "true",
                    HasClubData = sendingMatch.Groups[2].Value.ToLower() == "true",
                    GsProInfo = latestGsProInfo
                };
                continue;
            }

            if (currentShot == null)
                continue;

            // Extract timing data
            var ballSpinMatch = BallSpinRegex.Match(entry.Message);
            if (ballSpinMatch.Success)
            {
                currentShot.BallSpinTimeMs = double.Parse(ballSpinMatch.Groups[1].Value);
            }

            var clubDetectionMatch = ClubDetectionRegex.Match(entry.Message);
            if (clubDetectionMatch.Success)
            {
                currentShot.ClubDetectionTimeMs = double.Parse(clubDetectionMatch.Groups[1].Value);
            }

            var analysesMatch = ShotAnalysesRegex.Match(entry.Message);
            if (analysesMatch.Success)
            {
                currentShot.AnalysisTimeMs = double.Parse(analysesMatch.Groups[1].Value);
            }

            var totalMatch = TotalTimeRegex.Match(entry.Message);
            if (totalMatch.Success)
            {
                currentShot.TotalTimeMs = double.Parse(totalMatch.Groups[1].Value);
            }

            var bytesMatch = BytesSentRegex.Match(entry.Message);
            if (bytesMatch.Success)
            {
                currentShot.BytesSentToGSPro = int.Parse(bytesMatch.Groups[1].Value);
            }

            // Extract handedness and club type
            var handednessMatch = HandednessRegex.Match(entry.Message);
            if (handednessMatch.Success)
            {
                currentShot.Handed = handednessMatch.Groups[1].Value;
                currentShot.ClubType = handednessMatch.Groups[2].Value;
            }
        }

        // Add the last shot if exists
        if (currentShot != null)
        {
            shots.Add(currentShot);
        }

        // Number the shots
        for (int i = 0; i < shots.Count; i++)
        {
            shots[i].ShotNumber = i + 1;
        }

        return shots;
    }

    private static GsProPlayerInfo? ParseGsProData(string jsonData)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonData);
            var root = doc.RootElement;

            if (!root.TryGetProperty("Player", out var player) || player.ValueKind == JsonValueKind.Null)
                return null;

            return new GsProPlayerInfo
            {
                Handed = player.TryGetProperty("Handed", out var handed) ? handed.GetString() ?? "" : "",
                Club = player.TryGetProperty("Club", out var club) ? club.GetString() ?? "" : "",
                DistanceToTarget = player.TryGetProperty("DistanceToTarget", out var distance) ? distance.GetDouble() : 0,
                Surface = player.TryGetProperty("Surface", out var surface) && surface.ValueKind != JsonValueKind.Null
                    ? surface.GetString()
                    : null
            };
        }
        catch
        {
            return null;
        }
    }
}
