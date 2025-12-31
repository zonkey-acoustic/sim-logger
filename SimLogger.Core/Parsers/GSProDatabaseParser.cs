using System.Text.Json;
using Microsoft.Data.Sqlite;
using SimLogger.Core.Models;

namespace SimLogger.Core.Parsers;

/// <summary>
/// Parses flight data from GSPro's SQLite database.
/// GSPro stores shot data in the DrivingRangeShot table with JSON-encoded ShotData.
/// </summary>
public class GSProDatabaseParser
{
    private static readonly string DefaultGsProDbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low",
        "GSPro", "GSPro", "GSPro.db");

    /// <summary>
    /// Represents a shot record from GSPro's DrivingRangeShot table.
    /// </summary>
    public class GSProShot
    {
        public int ID { get; set; }
        public DateTime DateCreated { get; set; }
        public GSProShotData? ShotData { get; set; }
    }

    /// <summary>
    /// Flight and ball data from GSPro's shot JSON.
    /// </summary>
    public class GSProShotData
    {
        // Flight results
        public double Carry { get; set; }
        public double TotalDistance { get; set; }
        public double PeakHeight { get; set; }
        public double Offline { get; set; }
        public double Decent { get; set; }  // Descent angle
        public double DistanceToPin { get; set; }

        // Ball data (for verification/matching)
        public double BallSpeed { get; set; }
        public double BackSpin { get; set; }
        public double SideSpin { get; set; }
        public double HLA { get; set; }  // Horizontal Launch Angle
        public double VLA { get; set; }  // Vertical Launch Angle

        // Club data
        public string Club { get; set; } = string.Empty;
        public double ClubSpeed { get; set; }
        public double Path { get; set; }
        public double AoA { get; set; }  // Angle of Attack
        public double FaceToTarget { get; set; }
        public double FaceToPath { get; set; }
        public double Lie { get; set; }
        public double Loft { get; set; }
        public double DynamicLoft { get; set; }
        public double SmashFactor { get; set; }

        // Raw data
        public double rawSpinAxis { get; set; }
        public double rawCarryGame { get; set; }
        public double rawCarryLM { get; set; }
    }

    /// <summary>
    /// Gets the default GSPro database path.
    /// </summary>
    public static string GetDefaultDatabasePath() => DefaultGsProDbPath;

    /// <summary>
    /// Checks if the GSPro database exists at the default location.
    /// </summary>
    public static bool DatabaseExists() => File.Exists(DefaultGsProDbPath);

    /// <summary>
    /// Gets the latest N shots from the GSPro database.
    /// </summary>
    public static List<GSProShot> GetLatestShots(int count = 100, string? databasePath = null)
    {
        var shots = new List<GSProShot>();
        var dbPath = databasePath ?? DefaultGsProDbPath;

        if (!File.Exists(dbPath))
        {
            System.Diagnostics.Debug.WriteLine($"GSPro database not found: {dbPath}");
            return shots;
        }

        try
        {
            var connectionString = $"Data Source={dbPath};Mode=ReadOnly;Cache=Shared";
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var sql = $"SELECT ID, DateCreated, ShotData FROM DrivingRangeShot ORDER BY ID DESC LIMIT {count}";

            using var command = new SqliteCommand(sql, connection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                try
                {
                    var dateStr = reader.GetString(1);
                    if (!DateTime.TryParse(dateStr, out var dateCreated))
                        continue;

                    var shot = new GSProShot
                    {
                        ID = reader.GetInt32(0),
                        DateCreated = dateCreated
                    };

                    var shotDataJson = reader.IsDBNull(2) ? null : reader.GetString(2);
                    if (!string.IsNullOrEmpty(shotDataJson))
                    {
                        shot.ShotData = JsonSerializer.Deserialize<GSProShotData>(shotDataJson, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                    }

                    shots.Add(shot);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing GSPro shot record: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GSProDatabaseParser ERROR: {ex.GetType().Name}: {ex.Message}");
        }

        // Reverse to get oldest first (chronological order)
        shots.Reverse();
        return shots;
    }

    /// <summary>
    /// Gets shots with ID greater than the specified ID (for polling new shots).
    /// </summary>
    public static List<GSProShot> GetShotsAfterId(int lastId, string? databasePath = null)
    {
        var shots = new List<GSProShot>();
        var dbPath = databasePath ?? DefaultGsProDbPath;

        if (!File.Exists(dbPath))
            return shots;

        try
        {
            var connectionString = $"Data Source={dbPath};Mode=ReadOnly;Cache=Shared";
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var sql = $"SELECT ID, DateCreated, ShotData FROM DrivingRangeShot WHERE ID > {lastId} ORDER BY ID ASC";

            using var command = new SqliteCommand(sql, connection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                try
                {
                    var dateStr = reader.GetString(1);
                    if (!DateTime.TryParse(dateStr, out var dateCreated))
                        continue;

                    var shot = new GSProShot
                    {
                        ID = reader.GetInt32(0),
                        DateCreated = dateCreated
                    };

                    var shotDataJson = reader.IsDBNull(2) ? null : reader.GetString(2);
                    if (!string.IsNullOrEmpty(shotDataJson))
                    {
                        shot.ShotData = JsonSerializer.Deserialize<GSProShotData>(shotDataJson, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                    }

                    shots.Add(shot);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing GSPro shot record: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GSProDatabaseParser ERROR: {ex.GetType().Name}: {ex.Message}");
        }

        return shots;
    }

    /// <summary>
    /// Gets the maximum shot ID in the database.
    /// </summary>
    public static int GetMaxShotId(string? databasePath = null)
    {
        var dbPath = databasePath ?? DefaultGsProDbPath;

        if (!File.Exists(dbPath))
            return 0;

        try
        {
            var connectionString = $"Data Source={dbPath};Mode=ReadOnly;Cache=Shared";
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var command = new SqliteCommand("SELECT MAX(ID) FROM DrivingRangeShot", connection);
            var result = command.ExecuteScalar();
            return result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Converts a GSProShot to a ShotData model.
    /// </summary>
    public static ShotData ToShotData(GSProShot gsProShot)
    {
        var shot = new ShotData
        {
            GSProShotId = gsProShot.ID,
            DateTime = gsProShot.DateCreated,
            DirectoryTimestamp = gsProShot.DateCreated,
            DirectoryName = $"gspro-{gsProShot.ID}",
            IsRealShot = true
        };

        if (gsProShot.ShotData != null)
        {
            var gsp = gsProShot.ShotData;

            // Club data
            shot.ClubData = new ClubData
            {
                ClubName = gsp.Club,
                Speed = $"{gsp.ClubSpeed:F1} mph",
                SwingPath = $"{gsp.Path:F1}°",
                AttackAngle = $"{gsp.AoA:F1}°",
                FaceAngle = $"{gsp.FaceToTarget:F1}°",
                FaceToPath = $"{gsp.FaceToPath:F1}°",
                Loft = $"{gsp.Loft:F1}°",
                Lie = $"{gsp.Lie:F1}°"
            };

            // Ball data
            shot.BallData = new BallData
            {
                Speed = $"{gsp.BallSpeed:F1} mph",
                LaunchDirection = $"{gsp.HLA:F1}°",
                LaunchAngle = $"{gsp.VLA:F1}°",
                BackSpin = $"{gsp.BackSpin:F0} rpm",
                SideSpin = $"{gsp.SideSpin:F0} rpm",
                SpinAxis = $"{gsp.rawSpinAxis:F1}°"
            };

            // Calculate total spin from backspin and sidespin
            var totalSpin = Math.Sqrt(gsp.BackSpin * gsp.BackSpin + gsp.SideSpin * gsp.SideSpin);
            shot.BallData.TotalSpin = $"{totalSpin:F0} rpm";

            // Flight data
            shot.FlightData = new FlightData
            {
                Carry = $"{gsp.Carry:F1} yds",
                TotalDistance = $"{gsp.TotalDistance:F1} yds",
                Apex = $"{gsp.PeakHeight:F1} ft",
                OffLine = $"{gsp.Offline:F1} yds",
                DescentAngle = $"{gsp.Decent:F1}°"
            };

            // Smash factor
            if (gsp.SmashFactor > 0)
            {
                shot.SmashFactor = $"{gsp.SmashFactor:F2}";
            }
            else if (gsp.ClubSpeed > 0)
            {
                // Calculate smash factor if not provided
                var calculatedSmash = gsp.BallSpeed / gsp.ClubSpeed;
                shot.SmashFactor = $"{calculatedSmash:F2}";
            }

            // Distance to pin
            if (gsp.DistanceToPin > 0)
            {
                shot.DistanceToTarget = gsp.DistanceToPin;
            }
        }

        return shot;
    }

    /// <summary>
    /// Parses all shots from the GSPro database within a date range.
    /// </summary>
    public static List<GSProShot> ParseShots(DateTime? fromDate = null, DateTime? toDate = null, string? databasePath = null)
    {
        var shots = new List<GSProShot>();
        var dbPath = databasePath ?? DefaultGsProDbPath;

        if (!File.Exists(dbPath))
        {
            System.Diagnostics.Debug.WriteLine($"GSPro database not found: {dbPath}");
            return shots;
        }

        try
        {
            // Use a more permissive connection string that works when GSPro has the DB open
            var connectionString = $"Data Source={dbPath};Mode=ReadOnly;Cache=Shared";

            System.Diagnostics.Debug.WriteLine($"GSProDatabaseParser: Opening database at {dbPath}");

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            System.Diagnostics.Debug.WriteLine($"GSProDatabaseParser: Database opened successfully");

            var sql = "SELECT ID, DateCreated, ShotData FROM DrivingRangeShot";
            var conditions = new List<string>();

            if (fromDate.HasValue)
                conditions.Add($"DateCreated >= '{fromDate.Value:yyyy-MM-dd HH:mm:ss}'");
            if (toDate.HasValue)
                conditions.Add($"DateCreated <= '{toDate.Value:yyyy-MM-dd HH:mm:ss}'");

            if (conditions.Count > 0)
                sql += " WHERE " + string.Join(" AND ", conditions);

            sql += " ORDER BY DateCreated DESC";

            System.Diagnostics.Debug.WriteLine($"GSProDatabaseParser: Executing query: {sql}");

            using var command = new SqliteCommand(sql, connection);
            using var reader = command.ExecuteReader();

            System.Diagnostics.Debug.WriteLine($"GSProDatabaseParser: Query executed, reading results...");

            while (reader.Read())
            {
                try
                {
                    // DateCreated is stored as TEXT in SQLite, so parse it manually
                    var dateStr = reader.GetString(1);
                    if (!DateTime.TryParse(dateStr, out var dateCreated))
                    {
                        System.Diagnostics.Debug.WriteLine($"GSProDatabaseParser: Failed to parse date: {dateStr}");
                        continue;
                    }

                    var shot = new GSProShot
                    {
                        ID = reader.GetInt32(0),
                        DateCreated = dateCreated
                    };

                    System.Diagnostics.Debug.WriteLine($"GSProDatabaseParser: Read shot ID={shot.ID}, DateCreated={shot.DateCreated:yyyy-MM-dd HH:mm:ss}");

                    var shotDataJson = reader.IsDBNull(2) ? null : reader.GetString(2);
                    if (!string.IsNullOrEmpty(shotDataJson))
                    {
                        shot.ShotData = JsonSerializer.Deserialize<GSProShotData>(shotDataJson, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                    }

                    shots.Add(shot);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing GSPro shot record: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GSProDatabaseParser ERROR: {ex.GetType().Name}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"GSProDatabaseParser Stack: {ex.StackTrace}");
        }

        System.Diagnostics.Debug.WriteLine($"GSProDatabaseParser: Returning {shots.Count} shots");
        return shots;
    }

    /// <summary>
    /// Gets shots within a time window around a specific timestamp.
    /// Useful for matching launch monitor shots to GSPro records.
    /// </summary>
    public static List<GSProShot> GetShotsNearTimestamp(DateTime timestamp, double toleranceSeconds = 30, string? databasePath = null)
    {
        var fromDate = timestamp.AddSeconds(-toleranceSeconds);
        var toDate = timestamp.AddSeconds(toleranceSeconds);

        System.Diagnostics.Debug.WriteLine($"GSProDatabaseParser: Looking for shots between {fromDate:yyyy-MM-dd HH:mm:ss} and {toDate:yyyy-MM-dd HH:mm:ss}");

        var shots = ParseShots(fromDate, toDate, databasePath);

        System.Diagnostics.Debug.WriteLine($"GSProDatabaseParser: Found {shots.Count} shots in time window");

        return shots;
    }

    /// <summary>
    /// Finds the best matching GSPro shot for a given launch monitor shot timestamp and ball speed.
    /// Uses both timestamp proximity and ball speed matching for accuracy.
    /// </summary>
    public static GSProShot? FindMatchingShot(DateTime timestamp, double ballSpeedMph, double toleranceSeconds = 30, string? databasePath = null)
    {
        var candidates = GetShotsNearTimestamp(timestamp, toleranceSeconds, databasePath);

        if (candidates.Count == 0)
            return null;

        // Score candidates by timestamp proximity and ball speed similarity
        var scored = candidates
            .Where(c => c.ShotData != null)
            .Select(c => new
            {
                Shot = c,
                TimeDiff = Math.Abs((c.DateCreated - timestamp).TotalSeconds),
                SpeedDiff = Math.Abs(c.ShotData!.BallSpeed - ballSpeedMph)
            })
            .OrderBy(x => x.TimeDiff + (x.SpeedDiff * 0.5))  // Weight timestamp more than speed
            .ToList();

        return scored.FirstOrDefault()?.Shot;
    }

    /// <summary>
    /// Enriches a ShotData object with flight data from GSPro.
    /// </summary>
    public static bool EnrichWithFlightData(ShotData shot, GSProShot gsProShot)
    {
        if (gsProShot.ShotData == null)
            return false;

        var gspData = gsProShot.ShotData;

        // Create or update FlightData
        shot.FlightData ??= new FlightData();

        // Populate flight data from GSPro
        shot.FlightData.Carry = $"{gspData.Carry:F1} yds";
        shot.FlightData.TotalDistance = $"{gspData.TotalDistance:F1} yds";
        shot.FlightData.Apex = $"{gspData.PeakHeight:F1} ft";
        shot.FlightData.OffLine = $"{gspData.Offline:F1} yds";
        shot.FlightData.DescentAngle = $"{gspData.Decent:F1}°";

        // DistanceToPin can be used as remaining distance
        if (gspData.DistanceToPin > 0)
        {
            shot.DistanceToTarget = gspData.DistanceToPin;
        }

        return true;
    }
}
