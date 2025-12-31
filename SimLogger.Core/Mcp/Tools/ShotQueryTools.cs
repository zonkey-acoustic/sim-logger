using ModelContextProtocol.Server;
using SimLogger.Core.Mcp.Models;
using System.ComponentModel;
using System.Text.Json;

namespace SimLogger.Core.Mcp.Tools;

/// <summary>
/// MCP tools for querying golf shot data.
/// </summary>
[McpServerToolType]
public static class ShotQueryTools
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [McpServerTool(Name = "get_recent_shots"), Description("Get the most recent golf shots from the database. Returns shot number, date/time, club, carry distance, total distance, ball speed, club speed, launch angle, and smash factor.")]
    public static async Task<string> GetRecentShots(
        McpShotDataProvider provider,
        [Description("Number of recent shots to retrieve (default: 10, max: 100)")]
        int count = 10)
    {
        count = Math.Clamp(count, 1, 100);
        var shots = await provider.GetRecentShotsAsync(count);
        return JsonSerializer.Serialize(new { shots, count = shots.Count }, JsonOptions);
    }

    [McpServerTool(Name = "get_shot_details"), Description("Get full details for a specific golf shot including club data, ball data, flight data, and physics settings. Shot numbers start at 1 for the most recent shot.")]
    public static async Task<string> GetShotDetails(
        McpShotDataProvider provider,
        [Description("The shot number to retrieve (1 = most recent)")]
        int shotNumber)
    {
        if (shotNumber < 1)
            return JsonSerializer.Serialize(new { error = "Shot number must be 1 or greater" }, JsonOptions);

        var shot = await provider.GetShotDetailsAsync(shotNumber);
        if (shot == null)
            return JsonSerializer.Serialize(new { error = $"Shot #{shotNumber} not found" }, JsonOptions);

        return JsonSerializer.Serialize(shot, JsonOptions);
    }

    [McpServerTool(Name = "search_shots"), Description("Search for golf shots with optional filters for club name, date range, and carry distance range.")]
    public static async Task<string> SearchShots(
        McpShotDataProvider provider,
        [Description("Filter by club name (e.g., 'Driver', '7 Iron'). Partial matches supported.")]
        string? clubName = null,
        [Description("Start date in yyyy-MM-dd format")]
        string? startDate = null,
        [Description("End date in yyyy-MM-dd format")]
        string? endDate = null,
        [Description("Minimum carry distance in yards")]
        double? minCarry = null,
        [Description("Maximum carry distance in yards")]
        double? maxCarry = null,
        [Description("Maximum number of results to return (default: 50, max: 100)")]
        int maxResults = 50)
    {
        DateTime? parsedStartDate = null;
        DateTime? parsedEndDate = null;

        if (!string.IsNullOrEmpty(startDate))
        {
            if (!DateTime.TryParse(startDate, out var sd))
                return JsonSerializer.Serialize(new { error = "Invalid startDate format. Use yyyy-MM-dd" }, JsonOptions);
            parsedStartDate = sd;
        }

        if (!string.IsNullOrEmpty(endDate))
        {
            if (!DateTime.TryParse(endDate, out var ed))
                return JsonSerializer.Serialize(new { error = "Invalid endDate format. Use yyyy-MM-dd" }, JsonOptions);
            parsedEndDate = ed.AddDays(1).AddSeconds(-1); // End of day
        }

        var criteria = new ShotSearchCriteria(
            clubName,
            parsedStartDate,
            parsedEndDate,
            minCarry,
            maxCarry,
            Math.Clamp(maxResults, 1, 100)
        );

        var shots = await provider.SearchShotsAsync(criteria);
        return JsonSerializer.Serialize(new { shots, count = shots.Count, filters = new { clubName, startDate, endDate, minCarry, maxCarry } }, JsonOptions);
    }

    [McpServerTool(Name = "get_shots_by_club"), Description("Get all shots for a specific golf club.")]
    public static async Task<string> GetShotsByClub(
        McpShotDataProvider provider,
        [Description("The club name (e.g., 'Driver', '7 Iron', 'Putter'). Partial matches supported.")]
        string clubName,
        [Description("Maximum number of results to return (default: 50, max: 100)")]
        int maxResults = 50)
    {
        if (string.IsNullOrWhiteSpace(clubName))
            return JsonSerializer.Serialize(new { error = "Club name is required" }, JsonOptions);

        var criteria = new ShotSearchCriteria(clubName, null, null, null, null, Math.Clamp(maxResults, 1, 100));
        var shots = await provider.SearchShotsAsync(criteria);
        return JsonSerializer.Serialize(new { club = clubName, shots, count = shots.Count }, JsonOptions);
    }

    [McpServerTool(Name = "list_clubs"), Description("List all unique golf club names in the shot database, ordered by typical bag order (Driver, Woods, Hybrids, Irons, Wedges, Putter).")]
    public static async Task<string> ListClubs(McpShotDataProvider provider)
    {
        var clubs = await provider.GetUniqueClubNamesAsync();
        return JsonSerializer.Serialize(new { clubs, count = clubs.Count }, JsonOptions);
    }
}
