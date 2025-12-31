using Microsoft.Data.Sqlite;
using SimLogger.Core.Models;

namespace SimLogger.Core.Data;

public class ShotRepository
{
    private readonly DatabaseContext _context;

    public ShotRepository(DatabaseContext context)
    {
        _context = context;
    }

    public async Task<HashSet<string>> GetSyncedDirectoryNamesAsync()
    {
        var directoryNames = new HashSet<string>();
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT DirectoryName FROM Shots";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            directoryNames.Add(reader.GetString(0));
        }

        return directoryNames;
    }

    public async Task<HashSet<int>> GetSyncedGsProShotIdsAsync()
    {
        var ids = new HashSet<int>();
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT GSProShotId FROM Shots WHERE GSProShotId IS NOT NULL AND GSProShotId > 0";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetInt32(0));
        }

        return ids;
    }

    public async Task<bool> ExistsByDirectoryNameAsync(string directoryName)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM Shots WHERE DirectoryName = @directoryName";
        command.Parameters.AddWithValue("@directoryName", directoryName);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt64(result) > 0;
    }

    public async Task<long> InsertShotAsync(ShotData shot)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();

        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // Insert main shot record
            var shotCommand = connection.CreateCommand();
            shotCommand.CommandText = """
                INSERT INTO Shots (SessionID, ShotID, ShotNumber, DateTime, DirectoryName, DirectoryTimestamp,
                                   SmashFactor, IsRealShot, UseOverrideData, DistanceToTarget, GSProShotId, SyncedAt)
                VALUES (@sessionId, @shotId, @shotNumber, @dateTime, @directoryName, @directoryTimestamp,
                        @smashFactor, @isRealShot, @useOverrideData, @distanceToTarget, @gsProShotId, @syncedAt);
                SELECT last_insert_rowid();
                """;

            shotCommand.Parameters.AddWithValue("@sessionId", shot.SessionID);
            shotCommand.Parameters.AddWithValue("@shotId", shot.ShotID);
            shotCommand.Parameters.AddWithValue("@shotNumber", shot.ShotNumber);
            shotCommand.Parameters.AddWithValue("@dateTime", shot.DateTime.ToString("O"));
            shotCommand.Parameters.AddWithValue("@directoryName", shot.DirectoryName);
            shotCommand.Parameters.AddWithValue("@directoryTimestamp", shot.DirectoryTimestamp.ToString("O"));
            shotCommand.Parameters.AddWithValue("@smashFactor", shot.SmashFactor ?? (object)DBNull.Value);
            shotCommand.Parameters.AddWithValue("@isRealShot", shot.IsRealShot ? 1 : 0);
            shotCommand.Parameters.AddWithValue("@useOverrideData", shot.UseOverrideData ? 1 : 0);
            shotCommand.Parameters.AddWithValue("@distanceToTarget", shot.DistanceToTarget.HasValue ? shot.DistanceToTarget.Value : DBNull.Value);
            shotCommand.Parameters.AddWithValue("@gsProShotId", shot.GSProShotId > 0 ? shot.GSProShotId : DBNull.Value);
            shotCommand.Parameters.AddWithValue("@syncedAt", DateTime.UtcNow.ToString("O"));

            var dbShotId = Convert.ToInt64(await shotCommand.ExecuteScalarAsync());

            // Insert ClubData if present
            if (shot.ClubData != null)
            {
                await InsertClubDataAsync(connection, dbShotId, shot.ClubData);
            }

            // Insert BallData if present
            if (shot.BallData != null)
            {
                await InsertBallDataAsync(connection, dbShotId, shot.BallData);
            }

            // Insert FlightData if present
            if (shot.FlightData != null)
            {
                await InsertFlightDataAsync(connection, dbShotId, shot.FlightData);
            }

            // Insert PhysicsSettings if present
            if (shot.PhysicsSettings != null)
            {
                await InsertPhysicsSettingsAsync(connection, dbShotId, shot.PhysicsSettings);
            }

            await transaction.CommitAsync();
            return dbShotId;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task InsertClubDataAsync(SqliteConnection connection, long shotId, ClubData clubData)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ClubData (ShotId, Speed, Loft, FaceAngle, Lie, FaceToPath, AttackAngle, SwingPath,
                                  ImpactPointX, ImpactPointY, ImpactRatioX, ImpactRatioY, SpinLoft,
                                  DetectedClubType, ClubType, IsPredicted, ClubName)
            VALUES (@shotId, @speed, @loft, @faceAngle, @lie, @faceToPath, @attackAngle, @swingPath,
                    @impactPointX, @impactPointY, @impactRatioX, @impactRatioY, @spinLoft,
                    @detectedClubType, @clubType, @isPredicted, @clubName)
            """;

        command.Parameters.AddWithValue("@shotId", shotId);
        command.Parameters.AddWithValue("@speed", clubData.Speed ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@loft", clubData.Loft ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@faceAngle", clubData.FaceAngle ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@lie", clubData.Lie ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@faceToPath", clubData.FaceToPath ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@attackAngle", clubData.AttackAngle ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@swingPath", clubData.SwingPath ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@impactPointX", clubData.ImpactPointX ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@impactPointY", clubData.ImpactPointY ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@impactRatioX", clubData.ImpactRatioX ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@impactRatioY", clubData.ImpactRatioY ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@spinLoft", clubData.SpinLoft ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@detectedClubType", clubData.DetectedClubType);
        command.Parameters.AddWithValue("@clubType", clubData.ClubType);
        command.Parameters.AddWithValue("@isPredicted", clubData.IsPredicted ? 1 : 0);
        command.Parameters.AddWithValue("@clubName", clubData.ClubName ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertBallDataAsync(SqliteConnection connection, long shotId, BallData ballData)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO BallData (ShotId, Speed, LaunchDirection, LaunchAngle, BackSpin, SideSpin,
                                  TotalSpin, SpinAxis, IsPredicted)
            VALUES (@shotId, @speed, @launchDirection, @launchAngle, @backSpin, @sideSpin,
                    @totalSpin, @spinAxis, @isPredicted)
            """;

        command.Parameters.AddWithValue("@shotId", shotId);
        command.Parameters.AddWithValue("@speed", ballData.Speed ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@launchDirection", ballData.LaunchDirection ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@launchAngle", ballData.LaunchAngle ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@backSpin", ballData.BackSpin ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@sideSpin", ballData.SideSpin ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@totalSpin", ballData.TotalSpin ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@spinAxis", ballData.SpinAxis ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@isPredicted", ballData.IsPredicted ? 1 : 0);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertFlightDataAsync(SqliteConnection connection, long shotId, FlightData flightData)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO FlightData (ShotId, Apex, ApexTime, TotalDistance, Carry, Run, OffLine, AirTime, DescentAngle)
            VALUES (@shotId, @apex, @apexTime, @totalDistance, @carry, @run, @offLine, @airTime, @descentAngle)
            """;

        command.Parameters.AddWithValue("@shotId", shotId);
        command.Parameters.AddWithValue("@apex", flightData.Apex ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@apexTime", flightData.ApexTime ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@totalDistance", flightData.TotalDistance ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@carry", flightData.Carry ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@run", flightData.Run ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@offLine", flightData.OffLine ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@airTime", flightData.AirTime ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@descentAngle", flightData.DescentAngle ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertPhysicsSettingsAsync(SqliteConnection connection, long shotId, PhysicsSettings physics)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO PhysicsSettings (ShotId, Temperature, Altitude, RelativeHumidity, SurfaceType, StimpRating, IsDefault)
            VALUES (@shotId, @temperature, @altitude, @relativeHumidity, @surfaceType, @stimpRating, @isDefault)
            """;

        command.Parameters.AddWithValue("@shotId", shotId);
        command.Parameters.AddWithValue("@temperature", physics.Temperature);
        command.Parameters.AddWithValue("@altitude", physics.Altitude);
        command.Parameters.AddWithValue("@relativeHumidity", physics.RelativeHumidity);
        command.Parameters.AddWithValue("@surfaceType", physics.SurfaceType);
        command.Parameters.AddWithValue("@stimpRating", physics.StimpRating);
        command.Parameters.AddWithValue("@isDefault", physics.IsDefault ? 1 : 0);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<ShotData>> GetAllShotsAsync()
    {
        var shots = new List<ShotData>();
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();

        // Load all shots (newest first)
        var shotCommand = connection.CreateCommand();
        shotCommand.CommandText = "SELECT * FROM Shots ORDER BY DirectoryTimestamp DESC";

        await using var reader = await shotCommand.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var shot = new ShotData
            {
                SessionID = reader.GetInt32(reader.GetOrdinal("SessionID")),
                ShotID = reader.GetInt32(reader.GetOrdinal("ShotID")),
                ShotNumber = reader.GetInt32(reader.GetOrdinal("ShotNumber")),
                DateTime = DateTime.Parse(reader.GetString(reader.GetOrdinal("DateTime"))),
                DirectoryName = reader.GetString(reader.GetOrdinal("DirectoryName")),
                DirectoryTimestamp = DateTime.Parse(reader.GetString(reader.GetOrdinal("DirectoryTimestamp"))),
                SmashFactor = reader.IsDBNull(reader.GetOrdinal("SmashFactor")) ? string.Empty : reader.GetString(reader.GetOrdinal("SmashFactor")),
                IsRealShot = reader.GetInt32(reader.GetOrdinal("IsRealShot")) == 1,
                UseOverrideData = reader.GetInt32(reader.GetOrdinal("UseOverrideData")) == 1,
                DistanceToTarget = reader.IsDBNull(reader.GetOrdinal("DistanceToTarget")) ? null : reader.GetDouble(reader.GetOrdinal("DistanceToTarget")),
                GSProShotId = reader.IsDBNull(reader.GetOrdinal("GSProShotId")) ? 0 : reader.GetInt32(reader.GetOrdinal("GSProShotId")),
                IsSynced = true
            };

            var dbId = reader.GetInt64(reader.GetOrdinal("Id"));

            // Load related data
            shot.ClubData = await GetClubDataAsync(connection, dbId);
            shot.BallData = await GetBallDataAsync(connection, dbId);
            shot.FlightData = await GetFlightDataAsync(connection, dbId);
            shot.PhysicsSettings = await GetPhysicsSettingsAsync(connection, dbId);

            shots.Add(shot);
        }

        // Renumber shots
        for (int i = 0; i < shots.Count; i++)
        {
            shots[i].ShotNumber = i + 1;
        }

        return shots;
    }

    private static async Task<ClubData?> GetClubDataAsync(SqliteConnection connection, long shotId)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM ClubData WHERE ShotId = @shotId";
        command.Parameters.AddWithValue("@shotId", shotId);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new ClubData
        {
            Speed = reader.IsDBNull(reader.GetOrdinal("Speed")) ? string.Empty : reader.GetString(reader.GetOrdinal("Speed")),
            Loft = reader.IsDBNull(reader.GetOrdinal("Loft")) ? string.Empty : reader.GetString(reader.GetOrdinal("Loft")),
            FaceAngle = reader.IsDBNull(reader.GetOrdinal("FaceAngle")) ? string.Empty : reader.GetString(reader.GetOrdinal("FaceAngle")),
            Lie = reader.IsDBNull(reader.GetOrdinal("Lie")) ? string.Empty : reader.GetString(reader.GetOrdinal("Lie")),
            FaceToPath = reader.IsDBNull(reader.GetOrdinal("FaceToPath")) ? string.Empty : reader.GetString(reader.GetOrdinal("FaceToPath")),
            AttackAngle = reader.IsDBNull(reader.GetOrdinal("AttackAngle")) ? string.Empty : reader.GetString(reader.GetOrdinal("AttackAngle")),
            SwingPath = reader.IsDBNull(reader.GetOrdinal("SwingPath")) ? string.Empty : reader.GetString(reader.GetOrdinal("SwingPath")),
            ImpactPointX = reader.IsDBNull(reader.GetOrdinal("ImpactPointX")) ? string.Empty : reader.GetString(reader.GetOrdinal("ImpactPointX")),
            ImpactPointY = reader.IsDBNull(reader.GetOrdinal("ImpactPointY")) ? string.Empty : reader.GetString(reader.GetOrdinal("ImpactPointY")),
            ImpactRatioX = reader.IsDBNull(reader.GetOrdinal("ImpactRatioX")) ? string.Empty : reader.GetString(reader.GetOrdinal("ImpactRatioX")),
            ImpactRatioY = reader.IsDBNull(reader.GetOrdinal("ImpactRatioY")) ? string.Empty : reader.GetString(reader.GetOrdinal("ImpactRatioY")),
            SpinLoft = reader.IsDBNull(reader.GetOrdinal("SpinLoft")) ? string.Empty : reader.GetString(reader.GetOrdinal("SpinLoft")),
            DetectedClubType = reader.IsDBNull(reader.GetOrdinal("DetectedClubType")) ? 0 : reader.GetInt32(reader.GetOrdinal("DetectedClubType")),
            ClubType = reader.IsDBNull(reader.GetOrdinal("ClubType")) ? 0 : reader.GetInt32(reader.GetOrdinal("ClubType")),
            IsPredicted = reader.GetInt32(reader.GetOrdinal("IsPredicted")) == 1,
            ClubName = reader.IsDBNull(reader.GetOrdinal("ClubName")) ? string.Empty : reader.GetString(reader.GetOrdinal("ClubName"))
        };
    }

    private static async Task<BallData?> GetBallDataAsync(SqliteConnection connection, long shotId)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM BallData WHERE ShotId = @shotId";
        command.Parameters.AddWithValue("@shotId", shotId);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new BallData
        {
            Speed = reader.IsDBNull(reader.GetOrdinal("Speed")) ? string.Empty : reader.GetString(reader.GetOrdinal("Speed")),
            LaunchDirection = reader.IsDBNull(reader.GetOrdinal("LaunchDirection")) ? string.Empty : reader.GetString(reader.GetOrdinal("LaunchDirection")),
            LaunchAngle = reader.IsDBNull(reader.GetOrdinal("LaunchAngle")) ? string.Empty : reader.GetString(reader.GetOrdinal("LaunchAngle")),
            BackSpin = reader.IsDBNull(reader.GetOrdinal("BackSpin")) ? string.Empty : reader.GetString(reader.GetOrdinal("BackSpin")),
            SideSpin = reader.IsDBNull(reader.GetOrdinal("SideSpin")) ? string.Empty : reader.GetString(reader.GetOrdinal("SideSpin")),
            TotalSpin = reader.IsDBNull(reader.GetOrdinal("TotalSpin")) ? string.Empty : reader.GetString(reader.GetOrdinal("TotalSpin")),
            SpinAxis = reader.IsDBNull(reader.GetOrdinal("SpinAxis")) ? string.Empty : reader.GetString(reader.GetOrdinal("SpinAxis")),
            IsPredicted = reader.GetInt32(reader.GetOrdinal("IsPredicted")) == 1
        };
    }

    private static async Task<FlightData?> GetFlightDataAsync(SqliteConnection connection, long shotId)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM FlightData WHERE ShotId = @shotId";
        command.Parameters.AddWithValue("@shotId", shotId);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new FlightData
        {
            Apex = reader.IsDBNull(reader.GetOrdinal("Apex")) ? string.Empty : reader.GetString(reader.GetOrdinal("Apex")),
            ApexTime = reader.IsDBNull(reader.GetOrdinal("ApexTime")) ? string.Empty : reader.GetString(reader.GetOrdinal("ApexTime")),
            TotalDistance = reader.IsDBNull(reader.GetOrdinal("TotalDistance")) ? string.Empty : reader.GetString(reader.GetOrdinal("TotalDistance")),
            Carry = reader.IsDBNull(reader.GetOrdinal("Carry")) ? string.Empty : reader.GetString(reader.GetOrdinal("Carry")),
            Run = reader.IsDBNull(reader.GetOrdinal("Run")) ? string.Empty : reader.GetString(reader.GetOrdinal("Run")),
            OffLine = reader.IsDBNull(reader.GetOrdinal("OffLine")) ? string.Empty : reader.GetString(reader.GetOrdinal("OffLine")),
            AirTime = reader.IsDBNull(reader.GetOrdinal("AirTime")) ? string.Empty : reader.GetString(reader.GetOrdinal("AirTime")),
            DescentAngle = reader.IsDBNull(reader.GetOrdinal("DescentAngle")) ? string.Empty : reader.GetString(reader.GetOrdinal("DescentAngle"))
        };
    }

    private static async Task<PhysicsSettings?> GetPhysicsSettingsAsync(SqliteConnection connection, long shotId)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM PhysicsSettings WHERE ShotId = @shotId";
        command.Parameters.AddWithValue("@shotId", shotId);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new PhysicsSettings
        {
            Temperature = reader.GetDouble(reader.GetOrdinal("Temperature")),
            Altitude = reader.GetDouble(reader.GetOrdinal("Altitude")),
            RelativeHumidity = reader.GetDouble(reader.GetOrdinal("RelativeHumidity")),
            SurfaceType = reader.GetInt32(reader.GetOrdinal("SurfaceType")),
            StimpRating = reader.GetInt32(reader.GetOrdinal("StimpRating")),
            IsDefault = reader.GetInt32(reader.GetOrdinal("IsDefault")) == 1
        };
    }

    public async Task<int> GetShotCountAsync()
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Shots";

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task<bool> DeleteShotByDirectoryNameAsync(string directoryName)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();

        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // Get the shot ID first
            var getIdCommand = connection.CreateCommand();
            getIdCommand.CommandText = "SELECT Id FROM Shots WHERE DirectoryName = @directoryName";
            getIdCommand.Parameters.AddWithValue("@directoryName", directoryName);

            var shotIdResult = await getIdCommand.ExecuteScalarAsync();
            if (shotIdResult == null)
            {
                return false; // Shot not found
            }

            var shotId = Convert.ToInt64(shotIdResult);

            // Delete related records
            var deletePhysicsCommand = connection.CreateCommand();
            deletePhysicsCommand.CommandText = "DELETE FROM PhysicsSettings WHERE ShotId = @shotId";
            deletePhysicsCommand.Parameters.AddWithValue("@shotId", shotId);
            await deletePhysicsCommand.ExecuteNonQueryAsync();

            var deleteFlightCommand = connection.CreateCommand();
            deleteFlightCommand.CommandText = "DELETE FROM FlightData WHERE ShotId = @shotId";
            deleteFlightCommand.Parameters.AddWithValue("@shotId", shotId);
            await deleteFlightCommand.ExecuteNonQueryAsync();

            var deleteBallCommand = connection.CreateCommand();
            deleteBallCommand.CommandText = "DELETE FROM BallData WHERE ShotId = @shotId";
            deleteBallCommand.Parameters.AddWithValue("@shotId", shotId);
            await deleteBallCommand.ExecuteNonQueryAsync();

            var deleteClubCommand = connection.CreateCommand();
            deleteClubCommand.CommandText = "DELETE FROM ClubData WHERE ShotId = @shotId";
            deleteClubCommand.Parameters.AddWithValue("@shotId", shotId);
            await deleteClubCommand.ExecuteNonQueryAsync();

            // Delete the main shot record
            var deleteShotCommand = connection.CreateCommand();
            deleteShotCommand.CommandText = "DELETE FROM Shots WHERE Id = @shotId";
            deleteShotCommand.Parameters.AddWithValue("@shotId", shotId);
            await deleteShotCommand.ExecuteNonQueryAsync();

            await transaction.CommitAsync();

            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
