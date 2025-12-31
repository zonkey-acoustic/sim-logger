namespace SimLogger.Core.Models;

public class ShotEvent
{
    public DateTime Timestamp { get; set; }
    public string? VideoFileName { get; set; }
    public int ShotNumber { get; set; }
    public double AnalysisTimeMs { get; set; }
    public double BallSpinTimeMs { get; set; }
    public double ClubDetectionTimeMs { get; set; }
    public double TotalTimeMs { get; set; }
    public string Handed { get; set; } = string.Empty;
    public string ClubType { get; set; } = string.Empty;
    public bool HasBallData { get; set; }
    public bool HasClubData { get; set; }
    public int BytesSentToGSPro { get; set; }
    public GsProPlayerInfo? GsProInfo { get; set; }
}

public class GsProPlayerInfo
{
    public string Handed { get; set; } = string.Empty;
    public string Club { get; set; } = string.Empty;
    public double DistanceToTarget { get; set; }
    public string? Surface { get; set; }
}
