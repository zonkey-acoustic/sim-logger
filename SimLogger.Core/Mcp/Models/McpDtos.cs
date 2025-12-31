namespace SimLogger.Core.Mcp.Models;

/// <summary>
/// Compact shot summary for list views.
/// </summary>
public record ShotSummary(
    int ShotNumber,
    DateTime DateTime,
    string ClubName,
    string Carry,
    string TotalDistance,
    string BallSpeed,
    string ClubSpeed,
    string LaunchAngle,
    string SmashFactor,
    double? DistanceToTarget
);

/// <summary>
/// Full shot details including all related data.
/// </summary>
public record ShotDetails(
    int ShotNumber,
    DateTime DateTime,
    string SmashFactor,
    double? DistanceToTarget,
    int SessionId,
    ClubDataDto? Club,
    BallDataDto? Ball,
    FlightDataDto? Flight,
    PhysicsSettingsDto? Physics
);

/// <summary>
/// Club data DTO for MCP responses.
/// </summary>
public record ClubDataDto(
    string ClubName,
    string Speed,
    string Loft,
    string FaceAngle,
    string Lie,
    string FaceToPath,
    string AttackAngle,
    string SwingPath,
    string SpinLoft
);

/// <summary>
/// Ball data DTO for MCP responses.
/// </summary>
public record BallDataDto(
    string Speed,
    string LaunchAngle,
    string LaunchDirection,
    string BackSpin,
    string SideSpin,
    string TotalSpin,
    string SpinAxis
);

/// <summary>
/// Flight data DTO for MCP responses.
/// </summary>
public record FlightDataDto(
    string Carry,
    string TotalDistance,
    string OffLine,
    string Apex,
    string DescentAngle,
    string AirTime,
    string Run
);

/// <summary>
/// Physics settings DTO for MCP responses.
/// </summary>
public record PhysicsSettingsDto(
    double Temperature,
    double Altitude,
    double RelativeHumidity,
    string SurfaceType,
    int StimpRating
);

/// <summary>
/// Search criteria for filtering shots.
/// </summary>
public record ShotSearchCriteria(
    string? ClubName,
    DateTime? StartDate,
    DateTime? EndDate,
    double? MinCarry,
    double? MaxCarry,
    int MaxResults = 100
);

/// <summary>
/// Statistics for a specific club.
/// </summary>
public record ClubStatistics(
    string ClubName,
    int ShotCount,
    double AvgCarry,
    double AvgTotalDistance,
    double AvgBallSpeed,
    double AvgClubSpeed,
    double AvgLaunchAngle,
    double AvgBackSpin,
    double AvgSmashFactor
);

/// <summary>
/// Database metadata information.
/// </summary>
public record DatabaseInfo(
    string DatabasePath,
    int TotalShots,
    int UniqueClubs,
    DateTime? OldestShot,
    DateTime? NewestShot
);
