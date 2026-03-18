using Microsoft.Data.Sqlite;

namespace SimLogger.Core.Data;

public class DatabaseContext : IDisposable
{
    private readonly string _connectionString;
    private readonly string _databasePath;
    private bool _disposed;

    public string DatabasePath => _databasePath;
    public string DataDirectory { get; }

    public DatabaseContext() : this(null)
    {
    }

    public DatabaseContext(string? dataStoragePath)
    {
        // Use provided path or default to Documents/SimLogger
        string simLoggerDirectory;
        if (!string.IsNullOrEmpty(dataStoragePath))
        {
            simLoggerDirectory = dataStoragePath;
        }
        else
        {
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            simLoggerDirectory = Path.Combine(documentsPath, "SimLogger");
        }

        Directory.CreateDirectory(simLoggerDirectory);
        DataDirectory = simLoggerDirectory;

        _databasePath = Path.Combine(simLoggerDirectory, "simlogger.db");
        _connectionString = $"Data Source={_databasePath};Cache=Shared";
    }

    public SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.StateChange += (s, e) =>
        {
            if (e.CurrentState == System.Data.ConnectionState.Open)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "PRAGMA busy_timeout=5000;";
                cmd.ExecuteNonQuery();
            }
        };
        return connection;
    }

    public async Task InitializeDatabaseAsync()
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync();

        // Enable WAL mode for better concurrent read/write performance
        var walCommand = connection.CreateCommand();
        walCommand.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
        await walCommand.ExecuteNonQueryAsync();

        var createTablesCommand = connection.CreateCommand();
        createTablesCommand.CommandText = GetCreateTablesSql();
        await createTablesCommand.ExecuteNonQueryAsync();

        // Run migrations for existing databases
        await RunMigrationsAsync(connection);
    }

    private static async Task RunMigrationsAsync(SqliteConnection connection)
    {
        // Get existing columns
        var checkColumnCommand = connection.CreateCommand();
        checkColumnCommand.CommandText = "PRAGMA table_info(Shots)";

        var existingColumns = new HashSet<string>();
        await using (var reader = await checkColumnCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var columnName = reader.GetString(1); // Column name is at index 1
                existingColumns.Add(columnName);
            }
        }

        // Migration: Add DistanceToTarget column if it doesn't exist
        if (!existingColumns.Contains("DistanceToTarget"))
        {
            var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = "ALTER TABLE Shots ADD COLUMN DistanceToTarget REAL";
            await alterCommand.ExecuteNonQueryAsync();
        }

        // Migration: Add Tags column if it doesn't exist
        if (!existingColumns.Contains("Tags"))
        {
            var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = "ALTER TABLE Shots ADD COLUMN Tags TEXT";
            await alterCommand.ExecuteNonQueryAsync();
        }

        // Migration: Add GSProShotId column if it doesn't exist
        if (!existingColumns.Contains("GSProShotId"))
        {
            var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = "ALTER TABLE Shots ADD COLUMN GSProShotId INTEGER";
            await alterCommand.ExecuteNonQueryAsync();

            // Create index for GSProShotId
            var indexCommand = connection.CreateCommand();
            indexCommand.CommandText = "CREATE INDEX IF NOT EXISTS idx_shots_gsproshotid ON Shots(GSProShotId)";
            await indexCommand.ExecuteNonQueryAsync();
        }
    }

    private static string GetCreateTablesSql()
    {
        return """
            -- Main shots table
            CREATE TABLE IF NOT EXISTS Shots (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SessionID INTEGER NOT NULL,
                ShotID INTEGER NOT NULL,
                ShotNumber INTEGER NOT NULL,
                DateTime TEXT NOT NULL,
                DirectoryName TEXT NOT NULL,
                DirectoryTimestamp TEXT NOT NULL,
                SmashFactor TEXT,
                IsRealShot INTEGER NOT NULL DEFAULT 1,
                UseOverrideData INTEGER NOT NULL DEFAULT 0,
                DistanceToTarget REAL,
                GSProShotId INTEGER,
                Tags TEXT,
                SyncedAt TEXT NOT NULL,
                UNIQUE(DirectoryName)
            );

            -- Club data (1:1 with Shots)
            CREATE TABLE IF NOT EXISTS ClubData (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ShotId INTEGER NOT NULL UNIQUE,
                Speed TEXT,
                Loft TEXT,
                FaceAngle TEXT,
                Lie TEXT,
                FaceToPath TEXT,
                AttackAngle TEXT,
                SwingPath TEXT,
                ImpactPointX TEXT,
                ImpactPointY TEXT,
                ImpactRatioX TEXT,
                ImpactRatioY TEXT,
                SpinLoft TEXT,
                DetectedClubType INTEGER,
                ClubType INTEGER,
                IsPredicted INTEGER NOT NULL DEFAULT 0,
                ClubName TEXT,
                FOREIGN KEY (ShotId) REFERENCES Shots(Id) ON DELETE CASCADE
            );

            -- Ball data (1:1 with Shots)
            CREATE TABLE IF NOT EXISTS BallData (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ShotId INTEGER NOT NULL UNIQUE,
                Speed TEXT,
                LaunchDirection TEXT,
                LaunchAngle TEXT,
                BackSpin TEXT,
                SideSpin TEXT,
                TotalSpin TEXT,
                SpinAxis TEXT,
                IsPredicted INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (ShotId) REFERENCES Shots(Id) ON DELETE CASCADE
            );

            -- Flight data (1:1 with Shots)
            CREATE TABLE IF NOT EXISTS FlightData (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ShotId INTEGER NOT NULL UNIQUE,
                Apex TEXT,
                ApexTime TEXT,
                TotalDistance TEXT,
                Carry TEXT,
                Run TEXT,
                OffLine TEXT,
                AirTime TEXT,
                DescentAngle TEXT,
                FOREIGN KEY (ShotId) REFERENCES Shots(Id) ON DELETE CASCADE
            );

            -- Physics settings (1:1 with Shots)
            CREATE TABLE IF NOT EXISTS PhysicsSettings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ShotId INTEGER NOT NULL UNIQUE,
                Temperature REAL,
                Altitude REAL,
                RelativeHumidity REAL,
                SurfaceType INTEGER,
                StimpRating INTEGER,
                IsDefault INTEGER NOT NULL DEFAULT 1,
                FOREIGN KEY (ShotId) REFERENCES Shots(Id) ON DELETE CASCADE
            );

            -- Indexes for common queries
            CREATE INDEX IF NOT EXISTS idx_shots_datetime ON Shots(DateTime);
            CREATE INDEX IF NOT EXISTS idx_shots_directory ON Shots(DirectoryName);
            CREATE INDEX IF NOT EXISTS idx_clubdata_clubname ON ClubData(ClubName);
            """;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}
