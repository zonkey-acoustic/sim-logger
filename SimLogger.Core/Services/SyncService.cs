using SimLogger.Core.Data;
using SimLogger.Core.Models;
using SimLogger.Core.Parsers;

namespace SimLogger.Core.Services;

public class SyncService
{
    private readonly DatabaseContext _dbContext;
    private readonly ShotRepository _repository;

    public event EventHandler<SyncProgressEventArgs>? ProgressChanged;

    public SyncService(DatabaseContext dbContext, ShotRepository repository)
    {
        _dbContext = dbContext;
        _repository = repository;
    }

    public async Task<SyncResult> SyncAsync(CancellationToken cancellationToken = default)
    {
        return await SyncAsync(null, cancellationToken);
    }

    public async Task<SyncResult> SyncAsync(IEnumerable<ShotData>? specificShots, CancellationToken cancellationToken = default)
    {
        var result = new SyncResult();

        try
        {
            // Initialize database
            await _dbContext.InitializeDatabaseAsync();

            ReportProgress(0, 0, "Checking for new shots...", "Starting sync");

            // Get already-synced GSPro shot IDs
            var syncedGsProIds = await _repository.GetSyncedGsProShotIdsAsync();

            List<ShotData> shotsToSync;

            if (specificShots != null)
            {
                // Sync only the specified shots that aren't already synced
                shotsToSync = specificShots
                    .Where(s => !s.IsSynced && s.GSProShotId > 0 && !syncedGsProIds.Contains(s.GSProShotId))
                    .ToList();
            }
            else
            {
                // Load shots from GSPro database
                var gsProShots = GSProDatabaseParser.GetLatestShots(500);

                // Filter to only new shots (not already synced)
                shotsToSync = gsProShots
                    .Where(gs => !syncedGsProIds.Contains(gs.ID))
                    .Select(GSProDatabaseParser.ToShotData)
                    .ToList();
            }

            if (shotsToSync.Count == 0)
            {
                ReportProgress(100, 100, "No new shots to sync", "Complete");
                return result;
            }

            result.TotalShots = shotsToSync.Count;

            ReportProgress(0, shotsToSync.Count, "Syncing shots", $"Found {shotsToSync.Count} shots to sync");

            // Process each shot
            for (int i = 0; i < shotsToSync.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    result.WasCancelled = true;
                    break;
                }

                var shot = shotsToSync[i];
                var shotLabel = $"Shot {i + 1}/{shotsToSync.Count}";

                ReportProgress(i, shotsToSync.Count, shotLabel, $"Syncing GSPro shot {shot.GSProShotId}");

                try
                {
                    // Insert shot into database
                    await _repository.InsertShotAsync(shot);
                    result.ShotsProcessed++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"GSPro Shot {shot.GSProShotId}: {ex.Message}");
                }
            }

            var statusMessage = result.ShotsProcessed == 1
                ? "Synced 1 shot"
                : $"Synced {result.ShotsProcessed} shots";

            ReportProgress(shotsToSync.Count, shotsToSync.Count, statusMessage, "Complete");
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Sync failed: {ex.Message}");
        }

        return result;
    }

    public async Task<UnsyncResult> UnsyncAsync(IEnumerable<ShotData> shots)
    {
        var result = new UnsyncResult();
        var shotsList = shots.Where(s => s.IsSynced).ToList();

        if (shotsList.Count == 0)
        {
            return result;
        }

        result.TotalShots = shotsList.Count;

        for (int i = 0; i < shotsList.Count; i++)
        {
            var shot = shotsList[i];
            ReportProgress(i, shotsList.Count, $"Shot {i + 1}/{shotsList.Count}", $"Removing GSPro shot {shot.GSProShotId}");

            try
            {
                var deleted = await _repository.DeleteShotByDirectoryNameAsync(shot.DirectoryName);
                if (deleted)
                {
                    result.ShotsRemoved++;
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"GSPro Shot {shot.GSProShotId}: {ex.Message}");
            }
        }

        var statusMessage = result.ShotsRemoved == 1
            ? "Removed 1 shot from database"
            : $"Removed {result.ShotsRemoved} shots from database";

        ReportProgress(shotsList.Count, shotsList.Count, statusMessage, "Complete");

        return result;
    }

    private void ReportProgress(int current, int total, string currentItem, string status)
    {
        ProgressChanged?.Invoke(this, new SyncProgressEventArgs
        {
            Current = current,
            Total = total,
            CurrentItem = currentItem,
            Status = status,
            PercentComplete = total > 0 ? (int)((current / (double)total) * 100) : 0
        });
    }
}

public class SyncProgressEventArgs : EventArgs
{
    public int Current { get; set; }
    public int Total { get; set; }
    public string CurrentItem { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int PercentComplete { get; set; }
}

public class SyncResult
{
    public int TotalShots { get; set; }
    public int ShotsProcessed { get; set; }
    public List<string> Errors { get; set; } = new();
    public bool WasCancelled { get; set; }
    public bool Success => Errors.Count == 0 && !WasCancelled;
}

public class UnsyncResult
{
    public int TotalShots { get; set; }
    public int ShotsRemoved { get; set; }
    public List<string> Errors { get; set; } = new();
    public bool Success => Errors.Count == 0;
}
