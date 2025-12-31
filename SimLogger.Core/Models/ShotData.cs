namespace SimLogger.Core.Models;

public class ShotData
{
    public ClubData? ClubData { get; set; }
    public BallData? BallData { get; set; }
    public string SmashFactor { get; set; } = string.Empty;
    public FlightData? FlightData { get; set; }
    public PhysicsSettings? PhysicsSettings { get; set; }
    public bool IsRealShot { get; set; }
    public bool UseOverrideData { get; set; }
    public int SessionID { get; set; }
    public int ShotID { get; set; }
    public DateTime DateTime { get; set; }

    // GSPro data
    public double? DistanceToTarget { get; set; }
    public int GSProShotId { get; set; }

    // Added for tracking
    public string DirectoryName { get; set; } = string.Empty;
    public DateTime DirectoryTimestamp { get; set; }
    public int ShotNumber { get; set; }

    // Indicates whether the shot has been synced to the database
    public bool IsSynced { get; set; }
}

public class ClubData
{
    public string Speed { get; set; } = string.Empty;
    public string Loft { get; set; } = string.Empty;
    public string FaceAngle { get; set; } = string.Empty;
    public string Lie { get; set; } = string.Empty;
    public string FaceToPath { get; set; } = string.Empty;
    public string AttackAngle { get; set; } = string.Empty;
    public string SwingPath { get; set; } = string.Empty;
    public string ImpactPointX { get; set; } = string.Empty;
    public string ImpactPointY { get; set; } = string.Empty;
    public string ImpactRatioX { get; set; } = string.Empty;
    public string ImpactRatioY { get; set; } = string.Empty;
    public string SpinLoft { get; set; } = string.Empty;
    public int DetectedClubType { get; set; }
    public int ClubType { get; set; }
    public bool IsPredicted { get; set; }
    public string ClubName { get; set; } = string.Empty;
}

public class BallData
{
    public string Speed { get; set; } = string.Empty;
    public string LaunchDirection { get; set; } = string.Empty;
    public string LaunchAngle { get; set; } = string.Empty;
    public string BackSpin { get; set; } = string.Empty;
    public string SideSpin { get; set; } = string.Empty;
    public string TotalSpin { get; set; } = string.Empty;
    public string SpinAxis { get; set; } = string.Empty;
    public bool IsPredicted { get; set; }
}

public class FlightData
{
    public string Apex { get; set; } = string.Empty;
    public string ApexTime { get; set; } = string.Empty;
    public string TotalDistance { get; set; } = string.Empty;
    public string Carry { get; set; } = string.Empty;
    public string Run { get; set; } = string.Empty;
    public string OffLine { get; set; } = string.Empty;
    public string AirTime { get; set; } = string.Empty;
    public string DescentAngle { get; set; } = string.Empty;
}

public class PhysicsSettings
{
    public double Temperature { get; set; }
    public double Altitude { get; set; }
    public double RelativeHumidity { get; set; }
    public int SurfaceType { get; set; }
    public int StimpRating { get; set; }
    public bool IsDefault { get; set; }
}
