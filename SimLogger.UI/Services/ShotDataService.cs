using SimLogger.Core.Data;
using SimLogger.Core.Models;
using SimLogger.Core.Parsers;

namespace SimLogger.UI.Services;

public class ShotDataService
{
    private readonly string? _gsProDatabasePath;
    private readonly DatabaseContext _dbContext;
    private readonly ShotRepository _repository;
    private List<ShotData>? _cachedShots;

    public ShotDataService() : this(null, null)
    {
    }

    public ShotDataService(string? dataStoragePath, string? gsProDatabasePath = null)
    {
        _gsProDatabasePath = gsProDatabasePath;

        _dbContext = new DatabaseContext(dataStoragePath);
        _repository = new ShotRepository(_dbContext);
    }

    public string? GSProDatabasePath => _gsProDatabasePath;
    public DatabaseContext DatabaseContext => _dbContext;
    public ShotRepository Repository => _repository;

    public async Task<List<ShotData>> LoadShotsAsync(bool forceReload = false)
    {
        if (_cachedShots != null && !forceReload)
            return _cachedShots;

        return await Task.Run(async () =>
        {
            // Initialize database if needed
            await _dbContext.InitializeDatabaseAsync();

            // Load synced shots from database (these have videos copied)
            var dbShots = await _repository.GetAllShotsAsync();
            var syncedGsProIds = dbShots
                .Where(s => s.GSProShotId > 0)
                .Select(s => s.GSProShotId)
                .ToHashSet();

            // Load shots directly from GSPro database
            var gsProShots = GSProDatabaseParser.GetLatestShots(500, _gsProDatabasePath);

            // Convert unsynced GSPro shots to ShotData
            var unsyncedShots = gsProShots
                .Where(gs => !syncedGsProIds.Contains(gs.ID))
                .Select(GSProDatabaseParser.ToShotData)
                .ToList();

            // Combine: database shots + unsynced GSPro shots (newest first)
            var allShots = dbShots.Concat(unsyncedShots)
                .OrderByDescending(s => s.DirectoryTimestamp)
                .ToList();

            // Renumber shots sequentially
            for (int i = 0; i < allShots.Count; i++)
            {
                allShots[i].ShotNumber = i + 1;
            }

            _cachedShots = allShots;
            return allShots;
        });
    }

    public async Task<List<ShotData>> LoadShotsFromDatabaseOnlyAsync()
    {
        await _dbContext.InitializeDatabaseAsync();
        return await _repository.GetAllShotsAsync();
    }

    public List<ShotData>? GetCachedShots() => _cachedShots;

    public void ClearCache()
    {
        _cachedShots = null;
    }

    public IEnumerable<string> GetUniqueClubNames()
    {
        if (_cachedShots == null)
            return Enumerable.Empty<string>();

        return _cachedShots
            .Where(s => !string.IsNullOrEmpty(s.ClubData?.ClubName))
            .Select(s => s.ClubData!.ClubName)
            .Distinct()
            .OrderBy(c => c);
    }

    public IEnumerable<string> GetUniqueTags()
    {
        if (_cachedShots == null)
            return Enumerable.Empty<string>();

        return _cachedShots
            .SelectMany(s => s.Tags)
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct()
            .OrderBy(t => t);
    }

    public async Task UpdateShotTagsAsync(ShotData shot, List<string> tags)
    {
        if (shot.IsSynced)
        {
            await _repository.UpdateShotTagsAsync(shot.DirectoryName, tags);
        }
        shot.Tags = tags;
    }

    public IEnumerable<DateTime> GetUniqueDates()
    {
        if (_cachedShots == null)
            return Enumerable.Empty<DateTime>();

        return _cachedShots
            .Where(s => s.DirectoryTimestamp != DateTime.MinValue)
            .Select(s => s.DirectoryTimestamp.Date)
            .Distinct()
            .OrderByDescending(d => d);
    }
}
