using SimLogger.Core.Models;
using SimLogger.Core.Parsers;

namespace SimLogger.Core.Services;

/// <summary>
/// Polls the GSPro database for new shots.
/// </summary>
public class FileWatcherService : IDisposable
{
    private Timer? _pollTimer;
    private int _lastKnownShotId;
    private bool _isRunning;
    private bool _disposed;
    private readonly object _lockObject = new();
    private readonly string? _gsProDatabasePath;

    private const int PollIntervalMs = 500; // Poll every 500ms for faster detection

    public event EventHandler<NewShotDetectedEventArgs>? NewShotDetected;

    public bool IsRunning => _isRunning;

    public FileWatcherService(string? gsProDatabasePath = null)
    {
        _gsProDatabasePath = gsProDatabasePath;
    }

    public void Start()
    {
        if (_isRunning || _disposed)
            return;

        // Get the current max shot ID to avoid detecting existing shots as new
        _lastKnownShotId = GSProDatabaseParser.GetMaxShotId(_gsProDatabasePath);
        System.Diagnostics.Debug.WriteLine($"FileWatcherService: Starting with lastKnownShotId={_lastKnownShotId}");

        _pollTimer = new Timer(PollForNewShots, null, PollIntervalMs, PollIntervalMs);
        _isRunning = true;
    }

    public void Stop()
    {
        if (!_isRunning)
            return;

        _pollTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _pollTimer?.Dispose();
        _pollTimer = null;
        _isRunning = false;
    }

    private void PollForNewShots(object? state)
    {
        if (!_isRunning || _disposed)
            return;

        lock (_lockObject)
        {
            try
            {
                var newShots = GSProDatabaseParser.GetShotsAfterId(_lastKnownShotId, _gsProDatabasePath);

                foreach (var gsProShot in newShots)
                {
                    var shot = GSProDatabaseParser.ToShotData(gsProShot);

                    System.Diagnostics.Debug.WriteLine($"FileWatcherService: New shot detected - ID={gsProShot.ID}, Club={shot.ClubData?.ClubName}");

                    NewShotDetected?.Invoke(this, new NewShotDetectedEventArgs
                    {
                        Shot = shot,
                        DirectoryPath = string.Empty // No longer using directory path
                    });

                    // Update last known ID
                    if (gsProShot.ID > _lastKnownShotId)
                    {
                        _lastKnownShotId = gsProShot.ID;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FileWatcherService: Error polling for new shots: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

public class NewShotDetectedEventArgs : EventArgs
{
    public required ShotData Shot { get; set; }
    public required string DirectoryPath { get; set; }
}
