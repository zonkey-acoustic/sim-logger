using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using SimLogger.Core.Models;
using SimLogger.Core.Exporters;
using SimLogger.Core.Services;
using SimLogger.UI.Services;
using SimLogger.UI.ViewModels;

namespace SimLogger.UI.Views;

public partial class MainWindow : Window
{
    private readonly ShotDataService _shotDataService;
    private readonly ShotListViewModel _shotListViewModel;
    private readonly SyncViewModel _syncViewModel;
    private readonly SyncService _syncService;
    private readonly FileWatcherService _fileWatcher;
    private readonly GSProTrafficMonitor? _trafficMonitor;
    private readonly AudioTriggerService _audioTriggerService;
    private readonly NetworkTriggerService _networkTriggerService;

    // Deduplication: track recent trigger timestamps to prevent double-firing
    private DateTime _lastTriggerTime = DateTime.MinValue;
    private readonly TimeSpan _triggerDebounceInterval = TimeSpan.FromMilliseconds(500);

    // Windows DWM API for dark title bar
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public MainWindow()
    {
        InitializeComponent();

        // Enable dark title bar
        SourceInitialized += (s, e) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int value = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
        };

        // Initialize audio/settings service first to get data storage path
        _audioTriggerService = new AudioTriggerService();

        // Initialize network trigger service
        _networkTriggerService = new NetworkTriggerService
        {
            IsEnabled = _audioTriggerService.NetworkTriggerEnabled,
            Port = _audioTriggerService.NetworkTriggerPort,
            TargetHost = _audioTriggerService.NetworkTriggerHost
        };

        // Initialize real-time traffic monitor (if enabled and Npcap is installed)
        if (_audioTriggerService.RealtimeDetectionEnabled && GSProTrafficMonitor.IsNpcapInstalled())
        {
            _trafficMonitor = new GSProTrafficMonitor
            {
                GSProPort = _audioTriggerService.GSProApiPort
            };
            _trafficMonitor.ShotDetected += OnTrafficShotDetected;
            _trafficMonitor.Error += OnTrafficMonitorError;
        }

        // Initialize shot data service with configured data storage path and GSPro path
        _shotDataService = new ShotDataService(_audioTriggerService.DataStoragePath, _audioTriggerService.GetGSProDatabasePath());
        _shotListViewModel = new ShotListViewModel(_shotDataService);

        // Initialize sync service and view model
        _syncService = new SyncService(_shotDataService.DatabaseContext, _shotDataService.Repository);
        _syncViewModel = new SyncViewModel(_syncService);

        // Wire up view models to views
        ShotListView.DataContext = _shotListViewModel;

        // Wire up events
        _shotListViewModel.SelectedShots.CollectionChanged += OnSelectedShotsChanged;
        _shotListViewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Set initial toggle states from saved settings BEFORE wiring up events
        // This prevents event handlers from running during initialization
        ShotListView.AudioTriggerToggleControl.IsChecked = _audioTriggerService.IsEnabled;
        ShotListView.SetAudioDeviceTooltip(_audioTriggerService.SelectedDeviceName);
        ShotListView.SetDataStorageTooltip(_audioTriggerService.DataStoragePath);
        ShotListView.SetGSProPathTooltip(_audioTriggerService.GSProPath);
        ShotListView.NetworkTriggerToggleControl.IsChecked = _audioTriggerService.NetworkTriggerEnabled;
        ShotListView.SetNetworkPortTooltip(_audioTriggerService.NetworkTriggerPort, _audioTriggerService.NetworkTriggerHost);

        // Wire up sync button events from ShotListView
        ShotListView.SyncButtonClick += SyncButton_Click;
        ShotListView.CancelSyncButtonClick += CancelSyncButton_Click;

        // Wire up audio trigger events
        ShotListView.AudioTriggerToggleChanged += AudioTriggerToggle_Changed;
        ShotListView.AudioDeviceButtonClick += AudioDeviceButton_Click;

        // Wire up network trigger events
        ShotListView.NetworkTriggerToggleChanged += NetworkTriggerToggle_Changed;
        ShotListView.NetworkPortButtonClick += NetworkPortButton_Click;

        // Wire up export event
        ShotListView.ExportCsvButtonClick += ExportCsvButton_Click;

        // Wire up data storage event
        ShotListView.DataStorageButtonClick += DataStorageButton_Click;

        // Wire up GSPro path event
        ShotListView.GSProPathButtonClick += GSProPathButton_Click;

        // Wire up column order changed event
        ShotListView.ColumnOrderChanged += OnColumnOrderChanged;

        // Wire up column visibility events
        ShotListView.ColumnsButtonClick += ColumnsButton_Click;
        ShotListView.ColumnVisibilityChanged += OnColumnVisibilityChanged;

        // Wire up sync events
        _syncService.ProgressChanged += OnSyncProgressChanged;
        _syncViewModel.SyncCompleted += OnSyncCompleted;

        // Initialize GSPro database watcher for real-time shot detection
        _fileWatcher = new FileWatcherService(_audioTriggerService.GetGSProDatabasePath());
        _fileWatcher.NewShotDetected += OnNewShotDetected;

        // Load data on startup
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Apply saved column order and visibility
        ShotListView.ApplyColumnOrder(_audioTriggerService.ColumnOrder);
        ShotListView.ApplyColumnVisibility(_audioTriggerService.HiddenColumns);

        await LoadShotsAsync();

        // Sync any existing unsynced shots from GSPro on startup
        await InitialSyncAsync();

        // Start traffic monitor for real-time shot detection (if available)
        if (_trafficMonitor != null)
        {
            try
            {
                _trafficMonitor.Start();
                ShotListView.StatusTextControl.Text = $"Real-time detection active (port {_audioTriggerService.GSProApiPort})";
                // Skip file watcher when using real-time detection
            }
            catch (Exception ex)
            {
                ShotListView.StatusTextControl.Text = $"Real-time detection failed: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Traffic monitor failed to start: {ex.Message}");
                // Fall back to file watcher if traffic monitor fails
                _fileWatcher.Start();
            }
        }
        else
        {
            // Use file watcher as fallback when real-time detection is not available
            _fileWatcher.Start();
        }
    }

    private async Task InitialSyncAsync()
    {
        try
        {
            ShotListView.StatusTextControl.Text = "Syncing shots from GSPro...";
            var result = await Task.Run(() => _syncService.SyncAsync(CancellationToken.None));

            if (result.ShotsProcessed > 0)
            {
                ShotListView.StatusTextControl.Text = $"Synced {result.ShotsProcessed} shot(s) from GSPro";
                _shotDataService.ClearCache();
                await LoadShotsAsync();
            }
            else
            {
                ShotListView.StatusTextControl.Text = "Ready";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Initial sync failed: {ex.Message}");
            ShotListView.StatusTextControl.Text = "Ready";
        }
    }

    private void OnColumnOrderChanged(object? sender, List<string> columnOrder)
    {
        _audioTriggerService.ColumnOrder = columnOrder;
        _audioTriggerService.SaveSettings();
    }

    private void OnColumnVisibilityChanged(object? sender, List<string> hiddenColumns)
    {
        _audioTriggerService.HiddenColumns = hiddenColumns;
        _audioTriggerService.SaveSettings();
    }

    private void ColumnsButton_Click(object? sender, RoutedEventArgs e)
    {
        var allColumns = ShotListView.GetAllColumnHeaders();
        var dialog = new ColumnVisibilityDialog(allColumns, _audioTriggerService.HiddenColumns)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            ShotListView.SetColumnVisibility(dialog.HiddenColumns);
        }
    }

    private async Task LoadShotsAsync()
    {
        ShotListView.StatusTextControl.Text = "Loading shots...";
        try
        {
            await _shotListViewModel.RefreshAsync();
            ShotListView.StatusTextControl.Text = "Ready";
            UpdateSyncButtonState();
            UpdateExportButtonState();
        }
        catch (Exception ex)
        {
            ShotListView.StatusTextControl.Text = $"Error: {ex.Message}";
            MessageDialog.Show(this, "Error", $"Error loading shots: {ex.Message}", MessageDialogType.Error);
        }
    }

    private void OnSelectedShotsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateSyncButtonState();
        UpdateExportButtonState();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Only update button states when Shots property changes, not when filter changes
        // (filter change triggers Shots change, so we avoid updating with stale data)
        if (e.PropertyName == nameof(ShotListViewModel.Shots))
        {
            UpdateSyncButtonState();
            UpdateExportButtonState();
        }
    }

    private void UpdateSyncButtonState()
    {
        // Sync button is hidden - all shots are automatically synced
        ShotListView.SyncButtonControl.Visibility = Visibility.Collapsed;
    }

    private async void SyncButton_Click(object? sender, RoutedEventArgs e)
    {
        // This is now only used for manual sync if needed
        await PerformSyncAsync();
    }

    private async Task PerformSyncAsync()
    {
        // Show sync progress UI
        ShotListView.SyncButtonControl.IsEnabled = false;
        ShotListView.SyncProgressPanelControl.Visibility = Visibility.Visible;
        ShotListView.SyncProgressBarControl.Value = 0;
        ShotListView.SyncStatusTextControl.Text = "Starting sync...";

        var selectedShots = _shotListViewModel.SelectedShots;
        if (selectedShots.Count > 0)
        {
            // Sync only selected shots
            await SyncSelectedShotsAsync(selectedShots.ToList());
        }
        else
        {
            // Sync all unsynced shots
            await _syncViewModel.SyncCommand.ExecuteAsync(null);
        }
    }

    private async Task PerformUnsyncAsync()
    {
        var selectedShots = _shotListViewModel.SelectedShots;
        List<ShotData> syncedShots;

        if (selectedShots.Count > 0)
        {
            // Unsync selected shots
            syncedShots = selectedShots.Where(s => s.IsSynced).ToList();
        }
        else
        {
            // Unsync all visible shots
            syncedShots = _shotListViewModel.Shots.Where(s => s.IsSynced).ToList();
        }

        if (syncedShots.Count == 0)
            return;

        // Show progress
        ShotListView.SyncButtonControl.IsEnabled = false;
        ShotListView.SyncProgressPanelControl.Visibility = Visibility.Visible;
        ShotListView.SyncProgressBarControl.Value = 0;
        ShotListView.SyncStatusTextControl.Text = "Removing shots from database...";

        try
        {
            var unsyncResult = await Task.Run(() => _syncService.UnsyncAsync(syncedShots));

            if (unsyncResult.Success)
            {
                ShotListView.StatusTextControl.Text = unsyncResult.ShotsRemoved == 1
                    ? "Removed 1 shot from database"
                    : $"Removed {unsyncResult.ShotsRemoved} shots from database";
            }
            else
            {
                ShotListView.StatusTextControl.Text = $"Unsync completed with {unsyncResult.Errors.Count} error(s)";
            }
        }
        catch (Exception ex)
        {
            ShotListView.StatusTextControl.Text = $"Unsync failed: {ex.Message}";
            MessageDialog.Show(this, "Error", $"Error during unsync: {ex.Message}", MessageDialogType.Error);
        }
        finally
        {
            ShotListView.SyncProgressPanelControl.Visibility = Visibility.Collapsed;

            // Refresh the shot list
            _shotDataService.ClearCache();
            await LoadShotsAsync();

            // Clear the selection
            _shotListViewModel.ClearSelectionCommand.Execute(null);
        }
    }

    private async Task SyncSelectedShotsAsync(List<ShotData> shots)
    {
        var cancellationTokenSource = new CancellationTokenSource();

        try
        {
            var result = await Task.Run(() => _syncService.SyncAsync(shots, cancellationTokenSource.Token));

            if (result.WasCancelled)
            {
                _syncViewModel.SyncStatus = "Sync cancelled";
            }
            else if (result.Success)
            {
                _syncViewModel.SyncStatus = result.ShotsProcessed == 0
                    ? "No new shots to sync"
                    : $"Synced {result.ShotsProcessed} shot(s)";
            }
            else
            {
                _syncViewModel.SyncStatus = $"Sync completed with {result.Errors.Count} error(s)";
            }
        }
        catch (Exception ex)
        {
            _syncViewModel.SyncStatus = $"Sync failed: {ex.Message}";
        }
        finally
        {
            Dispatcher.Invoke(() =>
            {
                ShotListView.SyncProgressPanelControl.Visibility = Visibility.Collapsed;
                ShotListView.CancelSyncButtonControl.IsEnabled = true;
                ShotListView.StatusTextControl.Text = _syncViewModel.SyncStatus;
            });

            // Refresh the shot list
            _shotDataService.ClearCache();
            await LoadShotsAsync();

            // Clear the selection after syncing (this will trigger UpdateSyncButtonState)
            _shotListViewModel.ClearSelectionCommand.Execute(null);
        }
    }

    private void CancelSyncButton_Click(object? sender, RoutedEventArgs e)
    {
        _syncViewModel.CancelSyncCommand.Execute(null);
        ShotListView.CancelSyncButtonControl.IsEnabled = false;
        ShotListView.SyncStatusTextControl.Text = "Cancelling...";
    }

    private void OnSyncProgressChanged(object? sender, SyncProgressEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            ShotListView.SyncProgressBarControl.Value = e.PercentComplete;
            ShotListView.SyncStatusTextControl.Text = $"{e.CurrentItem} - {e.Status}";
        });
    }

    private async void OnSyncCompleted(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            ShotListView.SyncProgressPanelControl.Visibility = Visibility.Collapsed;
            ShotListView.CancelSyncButtonControl.IsEnabled = true;
            ShotListView.StatusTextControl.Text = _syncViewModel.SyncStatus;
        });

        // Refresh the shot list to show newly synced shots
        _shotDataService.ClearCache();
        await LoadShotsAsync();
    }

    private async void OnNewShotDetected(object? sender, NewShotDetectedEventArgs e)
    {
        await Dispatcher.InvokeAsync(async () =>
        {
            ShotListView.StatusTextControl.Text = "New shot detected...";
            _shotDataService.ClearCache();
            await LoadShotsAsync();

            // Fire triggers (with debouncing - traffic monitor may have already fired them)
            FireTriggersIfNotRecent();

            // Auto-sync new shot to database
            if (e.Shot != null)
            {
                await AutoSyncShotAsync(e.Shot);
            }
        });
    }

    private void OnTrafficShotDetected(object? sender, ShotTrafficDetectedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            ShotListView.StatusTextControl.Text = "Shot detected (real-time)!";

            // Fire triggers immediately - this is the real-time path
            FireTriggersIfNotRecent();
        });
    }

    private void OnTrafficMonitorError(object? sender, TrafficMonitorErrorEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            System.Diagnostics.Debug.WriteLine($"Traffic monitor error: {e.Message}");
        });
    }

    /// <summary>
    /// Fires audio and network triggers if not fired recently (debouncing).
    /// Prevents double-firing when both traffic monitor and file watcher detect the same shot.
    /// </summary>
    private void FireTriggersIfNotRecent()
    {
        var now = DateTime.Now;
        if (now - _lastTriggerTime < _triggerDebounceInterval)
        {
            // Already fired recently, skip to prevent double-triggering
            return;
        }

        _lastTriggerTime = now;

        // Play audio trigger if enabled
        _audioTriggerService.PlayTriggerTone();

        // Send network trigger if enabled
        _networkTriggerService.SendTriggerPacket();
    }

    private void AudioTriggerToggle_Changed(object? sender, RoutedEventArgs e)
    {
        var isEnabled = ShotListView.AudioTriggerToggleControl.IsChecked == true;

        // If enabling, check that an audio device has been selected
        if (isEnabled && _audioTriggerService.SelectedDeviceIndex < 0)
        {
            ShotListView.AudioTriggerToggleControl.IsChecked = false;
            MessageDialog.Show(
                this,
                "Configuration Required",
                "Please select an audio output device before enabling the audio trigger.",
                MessageDialogType.Warning);
            return;
        }

        _audioTriggerService.IsEnabled = isEnabled;
        _audioTriggerService.SaveSettings();
    }

    private void AudioDeviceButton_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new AudioDeviceDialog(_audioTriggerService)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true && dialog.SelectedDevice != null)
        {
            _audioTriggerService.SelectedDeviceIndex = dialog.SelectedDevice.Index;
            _audioTriggerService.SelectedDeviceName = dialog.SelectedDevice.Name;
            _audioTriggerService.SaveSettings();
            ShotListView.SetAudioDeviceTooltip(_audioTriggerService.SelectedDeviceName);
        }
    }

    private void NetworkTriggerToggle_Changed(object? sender, RoutedEventArgs e)
    {
        var isEnabled = ShotListView.NetworkTriggerToggleControl.IsChecked == true;

        // If enabling, check that a port has been configured
        if (isEnabled && _audioTriggerService.NetworkTriggerPort <= 0)
        {
            ShotListView.NetworkTriggerToggleControl.IsChecked = false;
            MessageDialog.Show(
                this,
                "Configuration Required",
                "Please configure a UDP port before enabling the network trigger.",
                MessageDialogType.Warning);
            return;
        }

        _networkTriggerService.IsEnabled = isEnabled;
        _audioTriggerService.NetworkTriggerEnabled = isEnabled;
        _audioTriggerService.SaveSettings();
    }

    private void NetworkPortButton_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new NetworkPortDialog(
            _networkTriggerService,
            _audioTriggerService.NetworkTriggerPort,
            _audioTriggerService.NetworkTriggerHost)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            _networkTriggerService.Port = dialog.SelectedPort;
            _networkTriggerService.TargetHost = dialog.SelectedHost;
            _audioTriggerService.NetworkTriggerPort = dialog.SelectedPort;
            _audioTriggerService.NetworkTriggerHost = dialog.SelectedHost;
            _audioTriggerService.SaveSettings();
            ShotListView.SetNetworkPortTooltip(dialog.SelectedPort, dialog.SelectedHost);
        }
    }

    private void DataStorageButton_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new DataStorageDialog(_audioTriggerService.DataStoragePath)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            var oldPath = _audioTriggerService.DataStoragePath;
            var newPath = dialog.SelectedPath;

            // Only process if path actually changed
            if (oldPath != newPath)
            {
                _audioTriggerService.DataStoragePath = newPath;
                _audioTriggerService.SaveSettings();
                ShotListView.SetDataStorageTooltip(newPath);

                // Notify user that restart is required for changes to take effect
                MessageDialog.Show(
                    this,
                    "Restart Required",
                    "Data storage location has been updated.\n\n" +
                    "Please restart Sim Logger for the changes to take effect.\n\n" +
                    "Note: Existing data will remain in the previous location and will not be migrated automatically.",
                    MessageDialogType.Information);
            }
        }
    }

    private void GSProPathButton_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new GSProPathDialog(_audioTriggerService.GSProPath)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            var oldPath = _audioTriggerService.GSProPath;
            var newPath = dialog.SelectedPath;

            // Only process if path actually changed
            if (oldPath != newPath)
            {
                _audioTriggerService.GSProPath = newPath;
                _audioTriggerService.SaveSettings();
                ShotListView.SetGSProPathTooltip(newPath);

                // Notify user that restart is required for changes to take effect
                MessageDialog.Show(
                    this,
                    "Restart Required",
                    "GSPro location has been updated.\n\n" +
                    "Please restart Sim Logger for the changes to take effect.",
                    MessageDialogType.Information);
            }
        }
    }

    private void UpdateExportButtonState()
    {
        var selectedShots = _shotListViewModel.SelectedShots;
        var hasShots = _shotListViewModel.Shots.Any();

        if (selectedShots.Count > 0)
        {
            ShotListView.ExportCsvButtonControl.Content = "Export Selected";
            ShotListView.ExportCsvButtonControl.IsEnabled = true;
        }
        else
        {
            ShotListView.ExportCsvButtonControl.Content = "Export All";
            ShotListView.ExportCsvButtonControl.IsEnabled = hasShots;
        }
    }

    private void ExportCsvButton_Click(object? sender, RoutedEventArgs e)
    {
        var selectedShots = _shotListViewModel.SelectedShots;
        List<ShotData> shotsToExport;
        string exportType;

        if (selectedShots.Count > 0)
        {
            // Export selected shots
            shotsToExport = selectedShots.ToList();
            exportType = "selected";
        }
        else
        {
            // Export all visible shots (button only enabled when filter is "Synced")
            shotsToExport = _shotListViewModel.Shots.ToList();
            exportType = "synced";
        }

        if (shotsToExport.Count == 0)
        {
            MessageDialog.Show(this, "Export", "No shots to export.", MessageDialogType.Information);
            return;
        }

        // Show format selection dialog
        var formatDialog = new ExportFormatDialog { Owner = this };
        if (formatDialog.ShowDialog() != true)
        {
            return;
        }

        var selectedFormat = formatDialog.SelectedFormat;
        var defaultFileName = selectedFormat switch
        {
            ExportFormat.ShotPattern => $"shotpattern-export-{DateTime.Now:yyyy-MM-dd}",
            _ => $"gspro-export-{DateTime.Now:MM-dd-yy-HH-mm-ss}"
        };

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            DefaultExt = ".csv",
            FileName = defaultFileName
        };

        if (dialog.ShowDialog() == true)
        {
            if (selectedFormat == ExportFormat.ShotPattern)
            {
                ShotDataExporter.ExportToShotPatternCsv(shotsToExport, dialog.FileName, silent: true);
                ShotListView.StatusTextControl.Text = $"Exported {shotsToExport.Count} {exportType} shot{(shotsToExport.Count != 1 ? "s" : "")} to Shot Pattern CSV";
            }
            else
            {
                ShotDataExporter.ExportToGSProCsv(shotsToExport, dialog.FileName, silent: true);
                ShotListView.StatusTextControl.Text = $"Exported {shotsToExport.Count} {exportType} shot{(shotsToExport.Count != 1 ? "s" : "")} to GSPro CSV";
            }
        }
    }

    private async Task AutoSyncShotAsync(ShotData shot)
    {
        ShotListView.StatusTextControl.Text = "Syncing new shot...";

        try
        {
            var result = await Task.Run(() => _syncService.SyncAsync(new[] { shot }, CancellationToken.None));

            if (result.Success && result.ShotsProcessed > 0)
            {
                ShotListView.StatusTextControl.Text = $"Synced: {result.ShotsProcessed} shot(s)";
            }
            else if (result.ShotsProcessed == 0)
            {
                ShotListView.StatusTextControl.Text = "Shot already synced";
            }
            else
            {
                ShotListView.StatusTextControl.Text = $"Sync error: {string.Join(", ", result.Errors)}";
            }

            // Refresh to show updated sync status
            _shotDataService.ClearCache();
            await LoadShotsAsync();
        }
        catch (Exception ex)
        {
            ShotListView.StatusTextControl.Text = $"Sync failed: {ex.Message}";
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _fileWatcher.Dispose();
        _trafficMonitor?.Dispose();
        _audioTriggerService.Dispose();
        _networkTriggerService.Dispose();
        base.OnClosed(e);
    }
}
