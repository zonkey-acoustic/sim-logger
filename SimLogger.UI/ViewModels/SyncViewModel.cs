using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimLogger.Core.Data;
using SimLogger.Core.Services;

namespace SimLogger.UI.ViewModels;

public partial class SyncViewModel : ObservableObject
{
    private readonly SyncService _syncService;
    private CancellationTokenSource? _cancellationTokenSource;

    public event EventHandler? SyncCompleted;

    [ObservableProperty]
    private bool _isSyncing;

    [ObservableProperty]
    private int _syncProgress;

    [ObservableProperty]
    private string _syncStatus = string.Empty;

    [ObservableProperty]
    private string _syncCurrentItem = string.Empty;

    [ObservableProperty]
    private bool _canSync = true;

    public SyncViewModel(SyncService syncService)
    {
        _syncService = syncService;
        _syncService.ProgressChanged += OnProgressChanged;
    }

    private void OnProgressChanged(object? sender, SyncProgressEventArgs e)
    {
        // Update UI on the dispatcher thread
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            SyncProgress = e.PercentComplete;
            SyncStatus = e.Status;
            SyncCurrentItem = e.CurrentItem;
        });
    }

    [RelayCommand]
    private async Task SyncAsync()
    {
        if (IsSyncing) return;

        IsSyncing = true;
        CanSync = false;
        SyncProgress = 0;
        SyncStatus = "Starting sync...";
        SyncCurrentItem = string.Empty;

        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            var result = await Task.Run(() => _syncService.SyncAsync(_cancellationTokenSource.Token));

            if (result.WasCancelled)
            {
                SyncStatus = "Sync cancelled";
            }
            else if (result.Success)
            {
                SyncStatus = result.ShotsProcessed == 0
                    ? "No new shots to sync"
                    : $"Synced {result.ShotsProcessed} shot(s)";
            }
            else
            {
                SyncStatus = $"Sync completed with {result.Errors.Count} error(s)";
            }

            SyncCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            SyncStatus = "Sync cancelled";
        }
        catch (Exception ex)
        {
            SyncStatus = $"Sync failed: {ex.Message}";
        }
        finally
        {
            IsSyncing = false;
            CanSync = true;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    [RelayCommand]
    private void CancelSync()
    {
        _cancellationTokenSource?.Cancel();
        SyncStatus = "Cancelling...";
    }
}
