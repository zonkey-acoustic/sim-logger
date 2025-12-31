using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace SimLogger.Core.Mcp.Tools;

/// <summary>
/// MCP tools for golf shot statistics and database information.
/// </summary>
[McpServerToolType]
public static class ShotStatisticsTools
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [McpServerTool(Name = "get_shot_count"), Description("Get the total number of golf shots recorded in the database.")]
    public static async Task<string> GetShotCount(McpShotDataProvider provider)
    {
        var count = await provider.GetShotCountAsync();
        return JsonSerializer.Serialize(new { totalShots = count }, JsonOptions);
    }

    [McpServerTool(Name = "get_club_averages"), Description("Get average statistics for a specific golf club including average carry, total distance, ball speed, club speed, launch angle, backspin, and smash factor.")]
    public static async Task<string> GetClubAverages(
        McpShotDataProvider provider,
        [Description("The club name to get averages for (e.g., 'Driver', '7 Iron'). Partial matches supported.")]
        string clubName)
    {
        if (string.IsNullOrWhiteSpace(clubName))
            return JsonSerializer.Serialize(new { error = "Club name is required" }, JsonOptions);

        var stats = await provider.GetClubStatisticsAsync(clubName);
        if (stats == null)
            return JsonSerializer.Serialize(new { error = $"No shots found for club '{clubName}'" }, JsonOptions);

        return JsonSerializer.Serialize(new
        {
            club = stats.ClubName,
            shotCount = stats.ShotCount,
            averages = new
            {
                carry = $"{stats.AvgCarry:F1} yds",
                totalDistance = $"{stats.AvgTotalDistance:F1} yds",
                ballSpeed = $"{stats.AvgBallSpeed:F1} mph",
                clubSpeed = $"{stats.AvgClubSpeed:F1} mph",
                launchAngle = $"{stats.AvgLaunchAngle:F1}°",
                backSpin = $"{stats.AvgBackSpin:F0} rpm",
                smashFactor = $"{stats.AvgSmashFactor:F2}"
            }
        }, JsonOptions);
    }

    [McpServerTool(Name = "get_all_club_statistics"), Description("Get statistics for all golf clubs in the database, grouped by club name. Includes shot count and averages for each club.")]
    public static async Task<string> GetAllClubStatistics(McpShotDataProvider provider)
    {
        var stats = await provider.GetAllClubStatisticsAsync();

        var formattedStats = stats.Select(s => new
        {
            club = s.ClubName,
            shotCount = s.ShotCount,
            avgCarry = $"{s.AvgCarry:F1} yds",
            avgTotal = $"{s.AvgTotalDistance:F1} yds",
            avgBallSpeed = $"{s.AvgBallSpeed:F1} mph",
            avgClubSpeed = $"{s.AvgClubSpeed:F1} mph",
            avgLaunchAngle = $"{s.AvgLaunchAngle:F1}°",
            avgSmash = $"{s.AvgSmashFactor:F2}"
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            clubs = formattedStats,
            totalClubs = formattedStats.Count,
            totalShots = stats.Sum(s => s.ShotCount)
        }, JsonOptions);
    }

    [McpServerTool(Name = "get_database_info"), Description("Get database information including file path, total shot count, number of unique clubs, and date range of recorded shots.")]
    public static async Task<string> GetDatabaseInfo(McpShotDataProvider provider)
    {
        var info = await provider.GetDatabaseInfoAsync();
        return JsonSerializer.Serialize(new
        {
            databasePath = info.DatabasePath,
            totalShots = info.TotalShots,
            uniqueClubs = info.UniqueClubs,
            dateRange = new
            {
                oldest = info.OldestShot?.ToString("yyyy-MM-dd HH:mm:ss"),
                newest = info.NewestShot?.ToString("yyyy-MM-dd HH:mm:ss")
            }
        }, JsonOptions);
    }

    [McpServerTool(Name = "compare_clubs"), Description("Compare statistics between two golf clubs to see differences in performance.")]
    public static async Task<string> CompareClubs(
        McpShotDataProvider provider,
        [Description("First club name to compare (e.g., 'Driver')")]
        string club1,
        [Description("Second club name to compare (e.g., '3 Wood')")]
        string club2)
    {
        if (string.IsNullOrWhiteSpace(club1) || string.IsNullOrWhiteSpace(club2))
            return JsonSerializer.Serialize(new { error = "Both club names are required" }, JsonOptions);

        var stats1 = await provider.GetClubStatisticsAsync(club1);
        var stats2 = await provider.GetClubStatisticsAsync(club2);

        if (stats1 == null)
            return JsonSerializer.Serialize(new { error = $"No shots found for club '{club1}'" }, JsonOptions);
        if (stats2 == null)
            return JsonSerializer.Serialize(new { error = $"No shots found for club '{club2}'" }, JsonOptions);

        return JsonSerializer.Serialize(new
        {
            comparison = new[]
            {
                new
                {
                    club = stats1.ClubName,
                    shotCount = stats1.ShotCount,
                    avgCarry = stats1.AvgCarry,
                    avgTotal = stats1.AvgTotalDistance,
                    avgBallSpeed = stats1.AvgBallSpeed,
                    avgClubSpeed = stats1.AvgClubSpeed,
                    avgLaunchAngle = stats1.AvgLaunchAngle,
                    avgSmash = stats1.AvgSmashFactor
                },
                new
                {
                    club = stats2.ClubName,
                    shotCount = stats2.ShotCount,
                    avgCarry = stats2.AvgCarry,
                    avgTotal = stats2.AvgTotalDistance,
                    avgBallSpeed = stats2.AvgBallSpeed,
                    avgClubSpeed = stats2.AvgClubSpeed,
                    avgLaunchAngle = stats2.AvgLaunchAngle,
                    avgSmash = stats2.AvgSmashFactor
                }
            },
            differences = new
            {
                carryDiff = $"{stats1.AvgCarry - stats2.AvgCarry:+0.0;-0.0;0} yds",
                totalDiff = $"{stats1.AvgTotalDistance - stats2.AvgTotalDistance:+0.0;-0.0;0} yds",
                ballSpeedDiff = $"{stats1.AvgBallSpeed - stats2.AvgBallSpeed:+0.0;-0.0;0} mph",
                clubSpeedDiff = $"{stats1.AvgClubSpeed - stats2.AvgClubSpeed:+0.0;-0.0;0} mph"
            }
        }, JsonOptions);
    }
}
