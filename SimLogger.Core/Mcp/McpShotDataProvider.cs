using Microsoft.Data.Sqlite;
using SimLogger.Core.Data;
using SimLogger.Core.Mcp.Models;
using System.Globalization;

namespace SimLogger.Core.Mcp;

/// <summary>
/// Data provider for MCP tools, wrapping database access with MCP-friendly query methods.
/// </summary>
public class McpShotDataProvider
{
    private readonly DatabaseContext _context;

    public McpShotDataProvider(DatabaseContext context)
    {
        _context = context;
    }

    public string DatabasePath => _context.DatabasePath;

    /// <summary>
    /// Gets the most recent N shots.
    /// </summary>
    public async Task<List<ShotSummary>> GetRecentShotsAsync(int count)
    {
        var shots = new List<ShotSummary>();
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.ShotNumber, s.DateTime, s.SmashFactor, s.DistanceToTarget,
                   c.ClubName, c.Speed as ClubSpeed,
                   b.Speed as BallSpeed, b.LaunchAngle,
                   f.Carry, f.TotalDistance
            FROM Shots s
            LEFT JOIN ClubData c ON c.ShotId = s.Id
            LEFT JOIN BallData b ON b.ShotId = s.Id
            LEFT JOIN FlightData f ON f.ShotId = s.Id
            ORDER BY s.DirectoryTimestamp DESC
            LIMIT @count
            """;
        command.Parameters.AddWithValue("@count", count);

        await using var reader = await command.ExecuteReaderAsync();
        int shotNumber = 1;
        while (await reader.ReadAsync())
        {
            shots.Add(ReadShotSummary(reader, shotNumber++));
        }

        return shots;
    }

    /// <summary>
    /// Gets full details for a specific shot by shot number.
    /// </summary>
    public async Task<ShotDetails?> GetShotDetailsAsync(int shotNumber)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();

        // Get the shot at the specified position (1-indexed, newest first)
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.*, c.*, b.*, f.*, p.*
            FROM Shots s
            LEFT JOIN ClubData c ON c.ShotId = s.Id
            LEFT JOIN BallData b ON b.ShotId = s.Id
            LEFT JOIN FlightData f ON f.ShotId = s.Id
            LEFT JOIN PhysicsSettings p ON p.ShotId = s.Id
            ORDER BY s.DirectoryTimestamp DESC
            LIMIT 1 OFFSET @offset
            """;
        command.Parameters.AddWithValue("@offset", shotNumber - 1);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return ReadShotDetails(reader, shotNumber);
    }

    /// <summary>
    /// Searches shots with optional filters.
    /// </summary>
    public async Task<List<ShotSummary>> SearchShotsAsync(ShotSearchCriteria criteria)
    {
        var shots = new List<ShotSummary>();
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();

        var conditions = new List<string>();
        var command = connection.CreateCommand();

        if (!string.IsNullOrWhiteSpace(criteria.ClubName))
        {
            conditions.Add("c.ClubName LIKE @clubName");
            command.Parameters.AddWithValue("@clubName", $"%{criteria.ClubName}%");
        }

        if (criteria.StartDate.HasValue)
        {
            conditions.Add("s.DateTime >= @startDate");
            command.Parameters.AddWithValue("@startDate", criteria.StartDate.Value.ToString("O"));
        }

        if (criteria.EndDate.HasValue)
        {
            conditions.Add("s.DateTime <= @endDate");
            command.Parameters.AddWithValue("@endDate", criteria.EndDate.Value.ToString("O"));
        }

        if (criteria.MinCarry.HasValue)
        {
            conditions.Add("CAST(REPLACE(f.Carry, ' yds', '') AS REAL) >= @minCarry");
            command.Parameters.AddWithValue("@minCarry", criteria.MinCarry.Value);
        }

        if (criteria.MaxCarry.HasValue)
        {
            conditions.Add("CAST(REPLACE(f.Carry, ' yds', '') AS REAL) <= @maxCarry");
            command.Parameters.AddWithValue("@maxCarry", criteria.MaxCarry.Value);
        }

        var whereClause = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";

        command.CommandText = $"""
            SELECT s.ShotNumber, s.DateTime, s.SmashFactor, s.DistanceToTarget,
                   c.ClubName, c.Speed as ClubSpeed,
                   b.Speed as BallSpeed, b.LaunchAngle,
                   f.Carry, f.TotalDistance
            FROM Shots s
            LEFT JOIN ClubData c ON c.ShotId = s.Id
            LEFT JOIN BallData b ON b.ShotId = s.Id
            LEFT JOIN FlightData f ON f.ShotId = s.Id
            {whereClause}
            ORDER BY s.DirectoryTimestamp DESC
            LIMIT @maxResults
            """;
        command.Parameters.AddWithValue("@maxResults", criteria.MaxResults);

        await using var reader = await command.ExecuteReaderAsync();
        int shotNumber = 1;
        while (await reader.ReadAsync())
        {
            shots.Add(ReadShotSummary(reader, shotNumber++));
        }

        return shots;
    }

    /// <summary>
    /// Gets statistics for a specific club.
    /// </summary>
    public async Task<ClubStatistics?> GetClubStatisticsAsync(string clubName)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                c.ClubName,
                COUNT(*) as ShotCount,
                AVG(CAST(REPLACE(REPLACE(f.Carry, ' yds', ''), ',', '') AS REAL)) as AvgCarry,
                AVG(CAST(REPLACE(REPLACE(f.TotalDistance, ' yds', ''), ',', '') AS REAL)) as AvgTotal,
                AVG(CAST(REPLACE(REPLACE(b.Speed, ' mph', ''), ',', '') AS REAL)) as AvgBallSpeed,
                AVG(CAST(REPLACE(REPLACE(c.Speed, ' mph', ''), ',', '') AS REAL)) as AvgClubSpeed,
                AVG(CAST(REPLACE(b.LaunchAngle, '°', '') AS REAL)) as AvgLaunchAngle,
                AVG(CAST(REPLACE(REPLACE(b.BackSpin, ' rpm', ''), ',', '') AS REAL)) as AvgBackSpin,
                AVG(CAST(s.SmashFactor AS REAL)) as AvgSmash
            FROM Shots s
            LEFT JOIN ClubData c ON c.ShotId = s.Id
            LEFT JOIN BallData b ON b.ShotId = s.Id
            LEFT JOIN FlightData f ON f.ShotId = s.Id
            WHERE c.ClubName LIKE @clubName
            GROUP BY c.ClubName
            """;
        command.Parameters.AddWithValue("@clubName", $"%{clubName}%");

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return ReadClubStatistics(reader);
    }

    /// <summary>
    /// Gets statistics for all clubs.
    /// </summary>
    public async Task<List<ClubStatistics>> GetAllClubStatisticsAsync()
    {
        var stats = new List<ClubStatistics>();
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                c.ClubName,
                COUNT(*) as ShotCount,
                AVG(CAST(REPLACE(REPLACE(f.Carry, ' yds', ''), ',', '') AS REAL)) as AvgCarry,
                AVG(CAST(REPLACE(REPLACE(f.TotalDistance, ' yds', ''), ',', '') AS REAL)) as AvgTotal,
                AVG(CAST(REPLACE(REPLACE(b.Speed, ' mph', ''), ',', '') AS REAL)) as AvgBallSpeed,
                AVG(CAST(REPLACE(REPLACE(c.Speed, ' mph', ''), ',', '') AS REAL)) as AvgClubSpeed,
                AVG(CAST(REPLACE(b.LaunchAngle, '°', '') AS REAL)) as AvgLaunchAngle,
                AVG(CAST(REPLACE(REPLACE(b.BackSpin, ' rpm', ''), ',', '') AS REAL)) as AvgBackSpin,
                AVG(CAST(s.SmashFactor AS REAL)) as AvgSmash
            FROM Shots s
            LEFT JOIN ClubData c ON c.ShotId = s.Id
            LEFT JOIN BallData b ON b.ShotId = s.Id
            LEFT JOIN FlightData f ON f.ShotId = s.Id
            WHERE c.ClubName IS NOT NULL AND c.ClubName != ''
            GROUP BY c.ClubName
            ORDER BY
                CASE
                    WHEN c.ClubName LIKE '%Driver%' THEN 1
                    WHEN c.ClubName LIKE '%Wood%' THEN 2
                    WHEN c.ClubName LIKE '%Hybrid%' THEN 3
                    WHEN c.ClubName LIKE '%Iron%' THEN 4
                    WHEN c.ClubName LIKE '%Wedge%' THEN 5
                    WHEN c.ClubName LIKE '%Putter%' THEN 6
                    ELSE 7
                END,
                c.ClubName
            """;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var stat = ReadClubStatistics(reader);
            if (stat != null)
                stats.Add(stat);
        }

        return stats;
    }

    /// <summary>
    /// Gets all unique club names.
    /// </summary>
    public async Task<List<string>> GetUniqueClubNamesAsync()
    {
        var clubs = new List<string>();
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DISTINCT c.ClubName
            FROM ClubData c
            WHERE c.ClubName IS NOT NULL AND c.ClubName != ''
            ORDER BY
                CASE
                    WHEN c.ClubName LIKE '%Driver%' THEN 1
                    WHEN c.ClubName LIKE '%Wood%' THEN 2
                    WHEN c.ClubName LIKE '%Hybrid%' THEN 3
                    WHEN c.ClubName LIKE '%Iron%' THEN 4
                    WHEN c.ClubName LIKE '%Wedge%' THEN 5
                    WHEN c.ClubName LIKE '%Putter%' THEN 6
                    ELSE 7
                END,
                c.ClubName
            """;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            clubs.Add(reader.GetString(0));
        }

        return clubs;
    }

    /// <summary>
    /// Gets total shot count.
    /// </summary>
    public async Task<int> GetShotCountAsync()
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Shots";

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    /// <summary>
    /// Gets database metadata.
    /// </summary>
    public async Task<DatabaseInfo> GetDatabaseInfoAsync()
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                COUNT(*) as TotalShots,
                COUNT(DISTINCT c.ClubName) as UniqueClubs,
                MIN(s.DateTime) as OldestShot,
                MAX(s.DateTime) as NewestShot
            FROM Shots s
            LEFT JOIN ClubData c ON c.ShotId = s.Id
            """;

        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();

        var totalShots = reader.GetInt32(0);
        var uniqueClubs = reader.GetInt32(1);
        DateTime? oldestShot = reader.IsDBNull(2) ? null : DateTime.Parse(reader.GetString(2));
        DateTime? newestShot = reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3));

        return new DatabaseInfo(
            DatabasePath,
            totalShots,
            uniqueClubs,
            oldestShot,
            newestShot
        );
    }

    private static ShotSummary ReadShotSummary(SqliteDataReader reader, int shotNumber)
    {
        return new ShotSummary(
            shotNumber,
            reader.IsDBNull(reader.GetOrdinal("DateTime"))
                ? DateTime.MinValue
                : DateTime.Parse(reader.GetString(reader.GetOrdinal("DateTime"))),
            GetStringOrEmpty(reader, "ClubName"),
            GetStringOrEmpty(reader, "Carry"),
            GetStringOrEmpty(reader, "TotalDistance"),
            GetStringOrEmpty(reader, "BallSpeed"),
            GetStringOrEmpty(reader, "ClubSpeed"),
            GetStringOrEmpty(reader, "LaunchAngle"),
            GetStringOrEmpty(reader, "SmashFactor"),
            reader.IsDBNull(reader.GetOrdinal("DistanceToTarget"))
                ? null
                : reader.GetDouble(reader.GetOrdinal("DistanceToTarget"))
        );
    }

    private static ShotDetails ReadShotDetails(SqliteDataReader reader, int shotNumber)
    {
        // Read main shot data
        var dateTime = DateTime.Parse(reader.GetString(reader.GetOrdinal("DateTime")));
        var smashFactor = GetStringOrEmpty(reader, "SmashFactor");
        double? distanceToTarget = null;
        try
        {
            var ordinal = reader.GetOrdinal("DistanceToTarget");
            if (!reader.IsDBNull(ordinal))
                distanceToTarget = reader.GetDouble(ordinal);
        }
        catch { }

        var sessionId = reader.GetInt32(reader.GetOrdinal("SessionID"));

        // Read club data
        ClubDataDto? club = null;
        var clubName = GetStringOrDefault(reader, "ClubName");
        if (clubName != null)
        {
            club = new ClubDataDto(
                clubName,
                GetStringOrEmpty(reader, "Speed"),
                GetStringOrEmpty(reader, "Loft"),
                GetStringOrEmpty(reader, "FaceAngle"),
                GetStringOrEmpty(reader, "Lie"),
                GetStringOrEmpty(reader, "FaceToPath"),
                GetStringOrEmpty(reader, "AttackAngle"),
                GetStringOrEmpty(reader, "SwingPath"),
                GetStringOrEmpty(reader, "SpinLoft")
            );
        }

        // Read ball data (need to handle column name conflicts)
        BallDataDto? ball = null;
        try
        {
            ball = new BallDataDto(
                GetStringOrEmpty(reader, "Speed"),
                GetStringOrEmpty(reader, "LaunchAngle"),
                GetStringOrEmpty(reader, "LaunchDirection"),
                GetStringOrEmpty(reader, "BackSpin"),
                GetStringOrEmpty(reader, "SideSpin"),
                GetStringOrEmpty(reader, "TotalSpin"),
                GetStringOrEmpty(reader, "SpinAxis")
            );
        }
        catch { }

        // Read flight data
        FlightDataDto? flight = null;
        try
        {
            flight = new FlightDataDto(
                GetStringOrEmpty(reader, "Carry"),
                GetStringOrEmpty(reader, "TotalDistance"),
                GetStringOrEmpty(reader, "OffLine"),
                GetStringOrEmpty(reader, "Apex"),
                GetStringOrEmpty(reader, "DescentAngle"),
                GetStringOrEmpty(reader, "AirTime"),
                GetStringOrEmpty(reader, "Run")
            );
        }
        catch { }

        // Read physics data
        PhysicsSettingsDto? physics = null;
        try
        {
            var tempOrdinal = reader.GetOrdinal("Temperature");
            if (!reader.IsDBNull(tempOrdinal))
            {
                physics = new PhysicsSettingsDto(
                    reader.GetDouble(reader.GetOrdinal("Temperature")),
                    reader.GetDouble(reader.GetOrdinal("Altitude")),
                    reader.GetDouble(reader.GetOrdinal("RelativeHumidity")),
                    GetSurfaceTypeName(reader.GetInt32(reader.GetOrdinal("SurfaceType"))),
                    reader.GetInt32(reader.GetOrdinal("StimpRating"))
                );
            }
        }
        catch { }

        return new ShotDetails(
            shotNumber,
            dateTime,
            smashFactor,
            distanceToTarget,
            sessionId,
            club,
            ball,
            flight,
            physics
        );
    }

    private static ClubStatistics? ReadClubStatistics(SqliteDataReader reader)
    {
        var clubName = GetStringOrDefault(reader, "ClubName");
        if (clubName == null)
            return null;

        return new ClubStatistics(
            clubName,
            reader.GetInt32(reader.GetOrdinal("ShotCount")),
            GetDoubleOrZero(reader, "AvgCarry"),
            GetDoubleOrZero(reader, "AvgTotal"),
            GetDoubleOrZero(reader, "AvgBallSpeed"),
            GetDoubleOrZero(reader, "AvgClubSpeed"),
            GetDoubleOrZero(reader, "AvgLaunchAngle"),
            GetDoubleOrZero(reader, "AvgBackSpin"),
            GetDoubleOrZero(reader, "AvgSmash")
        );
    }

    private static string GetStringOrEmpty(SqliteDataReader reader, string columnName)
    {
        try
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string? GetStringOrDefault(SqliteDataReader reader, string columnName)
    {
        try
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }
        catch
        {
            return null;
        }
    }

    private static double GetDoubleOrZero(SqliteDataReader reader, string columnName)
    {
        try
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? 0.0 : reader.GetDouble(ordinal);
        }
        catch
        {
            return 0.0;
        }
    }

    private static string GetSurfaceTypeName(int surfaceType)
    {
        return surfaceType switch
        {
            0 => "Tee",
            1 => "Fairway",
            2 => "Rough",
            3 => "Bunker",
            4 => "Green",
            _ => "Unknown"
        };
    }
}
