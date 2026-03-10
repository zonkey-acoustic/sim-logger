using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using SimLogger.Core.Models;
using SimLogger.Core.Exporters;
using SimLogger.Core.Importers;
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
    private readonly SettingsService _settingsService;

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

        // Initialize settings service
        _settingsService = new SettingsService();

        // Initialize shot data service with configured data storage path and GSPro path
        _shotDataService = new ShotDataService(_settingsService.DataStoragePath, _settingsService.GetGSProDatabasePath());
        _shotListViewModel = new ShotListViewModel(_shotDataService);

        // Initialize sync service and view model
        _syncService = new SyncService(_shotDataService.DatabaseContext, _shotDataService.Repository);
        _syncViewModel = new SyncViewModel(_syncService);

        // Wire up view models to views
        ShotListView.DataContext = _shotListViewModel;

        // Wire up events
        _shotListViewModel.SelectedShots.CollectionChanged += OnSelectedShotsChanged;
        _shotListViewModel.PropertyChanged += OnViewModelPropertyChanged;

        ShotListView.SetDataStorageTooltip(_settingsService.DataStoragePath);
        ShotListView.SetGSProPathTooltip(_settingsService.GSProPath);

        // Wire up sync button events from ShotListView
        ShotListView.SyncButtonClick += SyncButton_Click;
        ShotListView.CancelSyncButtonClick += CancelSyncButton_Click;

        // Wire up export/import events
        ShotListView.ExportCsvButtonClick += ExportCsvButton_Click;
        ShotListView.ImportCsvButtonClick += ImportCsvButton_Click;

        // Wire up data storage event
        ShotListView.DataStorageButtonClick += DataStorageButton_Click;

        // Wire up GSPro path event
        ShotListView.GSProPathButtonClick += GSProPathButton_Click;

        // Wire up column order changed event
        ShotListView.ColumnOrderChanged += OnColumnOrderChanged;

        // Wire up column visibility events
        ShotListView.ColumnsButtonClick += ColumnsButton_Click;
        ShotListView.ColumnVisibilityChanged += OnColumnVisibilityChanged;

        // Wire up tag editing event
        ShotListView.EditTagsRequested += EditTags_Requested;

        // Wire up shot deletion event
        ShotListView.DeleteShotsRequested += DeleteShots_Requested;

        // Wire up sync events
        _syncService.ProgressChanged += OnSyncProgressChanged;
        _syncViewModel.SyncCompleted += OnSyncCompleted;

        // Initialize GSPro database watcher for shot detection
        _fileWatcher = new FileWatcherService(_settingsService.GetGSProDatabasePath());
        _fileWatcher.NewShotDetected += OnNewShotDetected;

        // Load data on startup
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Apply saved column order and visibility
        ShotListView.ApplyColumnOrder(_settingsService.ColumnOrder);
        ShotListView.ApplyColumnVisibility(_settingsService.HiddenColumns);

        await LoadShotsAsync();

        // Sync any existing unsynced shots from GSPro on startup
        await InitialSyncAsync();

        // Start file watcher for shot detection
        _fileWatcher.Start();
    }

    private async Task InitialSyncAsync()
    {
        try
        {
            var result = await Task.Run(() => _syncService.SyncAsync(CancellationToken.None));

            if (result.ShotsProcessed > 0)
            {
                _shotDataService.ClearCache();
                await LoadShotsAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Initial sync failed: {ex.Message}");
        }
    }

    private void OnColumnOrderChanged(object? sender, List<string> columnOrder)
    {
        _settingsService.ColumnOrder = columnOrder;
        _settingsService.SaveSettings();
    }

    private void OnColumnVisibilityChanged(object? sender, List<string> hiddenColumns)
    {
        _settingsService.HiddenColumns = hiddenColumns;
        _settingsService.SaveSettings();
    }

    private void ColumnsButton_Click(object? sender, RoutedEventArgs e)
    {
        var allColumns = ShotListView.GetAllColumnHeaders();
        var dialog = new ColumnVisibilityDialog(allColumns, _settingsService.HiddenColumns)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            ShotListView.SetColumnVisibility(dialog.HiddenColumns);
        }
    }

    private async void EditTags_Requested(object? sender, List<ShotData> shots)
    {
        // Only allow tag editing on synced shots
        var syncedShots = shots.Where(s => s.IsSynced).ToList();
        if (syncedShots.Count == 0)
        {
            MessageDialog.Show(this, "Edit Tags",
                "Tags can only be added to synced shots.\nPlease sync the shot(s) first.",
                MessageDialogType.Information);
            return;
        }

        // For bulk editing, start with common tags across all selected shots
        var commonTags = syncedShots.Count == 1
            ? syncedShots[0].Tags.ToList()
            : syncedShots
                .Select(s => s.Tags.AsEnumerable())
                .Aggregate((a, b) => a.Intersect(b))
                .ToList();

        var allTags = _shotDataService.GetUniqueTags().ToList();
        var dialog = new TagEditorDialog(commonTags, allTags)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var shot in syncedShots)
            {
                await _shotDataService.UpdateShotTagsAsync(shot, dialog.ResultTags.ToList());
            }
            await _shotListViewModel.RefreshAsync();
        }
    }

    private async void DeleteShots_Requested(object? sender, List<ShotData> shots)
    {
        var syncedShots = shots.Where(s => s.IsSynced).ToList();
        if (syncedShots.Count == 0)
        {
            MessageDialog.Show(this, "Delete Shots",
                "Only synced shots can be deleted from the database.",
                MessageDialogType.Information);
            return;
        }

        var shotWord = syncedShots.Count == 1 ? "shot" : "shots";
        var confirmed = MessageDialog.Confirm(this, "Delete Shots",
            $"Are you sure you want to delete {syncedShots.Count} {shotWord} from the database?\n\nThis action cannot be undone.",
            MessageDialogType.Warning);

        if (!confirmed)
            return;

        var errors = new List<string>();
        foreach (var shot in syncedShots)
        {
            try
            {
                await _shotDataService.Repository.DeleteShotByDirectoryNameAsync(shot.DirectoryName);
            }
            catch (Exception ex)
            {
                errors.Add($"Shot #{shot.ShotNumber}: {ex.Message}");
            }
        }

        _shotDataService.ClearCache();
        _shotListViewModel.ClearSelectionCommand.Execute(null);
        await LoadShotsAsync();

        if (errors.Count > 0)
        {
            MessageDialog.Show(this, "Delete Errors",
                $"Deleted {syncedShots.Count - errors.Count} shot(s) but {errors.Count} failed:\n{string.Join("\n", errors)}",
                MessageDialogType.Error);
        }
    }

    private async Task LoadShotsAsync()
    {
        try
        {
            await _shotListViewModel.RefreshAsync();
            UpdateSyncButtonState();
            UpdateExportButtonState();
        }
        catch (Exception ex)
        {
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
        if (e.PropertyName == nameof(ShotListViewModel.Shots))
        {
            UpdateSyncButtonState();
            UpdateExportButtonState();
        }
    }

    private void UpdateSyncButtonState()
    {
        ShotListView.SyncButtonControl.Visibility = Visibility.Collapsed;
    }

    private async void SyncButton_Click(object? sender, RoutedEventArgs e)
    {
        await PerformSyncAsync();
    }

    private async Task PerformSyncAsync()
    {
        ShotListView.SyncButtonControl.IsEnabled = false;
        ShotListView.SyncProgressPanelControl.Visibility = Visibility.Visible;
        ShotListView.SyncProgressBarControl.Value = 0;
        ShotListView.SyncStatusTextControl.Text = "Starting sync...";

        var selectedShots = _shotListViewModel.SelectedShots;
        if (selectedShots.Count > 0)
        {
            await SyncSelectedShotsAsync(selectedShots.ToList());
        }
        else
        {
            await _syncViewModel.SyncCommand.ExecuteAsync(null);
        }
    }

    private async Task PerformUnsyncAsync()
    {
        var selectedShots = _shotListViewModel.SelectedShots;
        List<ShotData> syncedShots;

        if (selectedShots.Count > 0)
        {
            syncedShots = selectedShots.Where(s => s.IsSynced).ToList();
        }
        else
        {
            syncedShots = _shotListViewModel.Shots.Where(s => s.IsSynced).ToList();
        }

        if (syncedShots.Count == 0)
            return;

        ShotListView.SyncButtonControl.IsEnabled = false;
        ShotListView.SyncProgressPanelControl.Visibility = Visibility.Visible;
        ShotListView.SyncProgressBarControl.Value = 0;
        ShotListView.SyncStatusTextControl.Text = "Removing shots from database...";

        try
        {
            await Task.Run(() => _syncService.UnsyncAsync(syncedShots));
        }
        catch (Exception ex)
        {
            MessageDialog.Show(this, "Error", $"Error during unsync: {ex.Message}", MessageDialogType.Error);
        }
        finally
        {
            ShotListView.SyncProgressPanelControl.Visibility = Visibility.Collapsed;

            _shotDataService.ClearCache();
            await LoadShotsAsync();

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
            });

            _shotDataService.ClearCache();
            await LoadShotsAsync();

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
        });

        _shotDataService.ClearCache();
        await LoadShotsAsync();
    }

    private async void OnNewShotDetected(object? sender, NewShotDetectedEventArgs e)
    {
        await Dispatcher.InvokeAsync(async () =>
        {
            _shotDataService.ClearCache();
            await LoadShotsAsync();

            // Auto-sync new shot to database
            if (e.Shot != null)
            {
                await AutoSyncShotAsync(e.Shot);
            }
        });
    }

    private void DataStorageButton_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new DataStorageDialog(_settingsService.DataStoragePath)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            var oldPath = _settingsService.DataStoragePath;
            var newPath = dialog.SelectedPath;

            if (oldPath != newPath)
            {
                _settingsService.DataStoragePath = newPath;
                _settingsService.SaveSettings();
                ShotListView.SetDataStorageTooltip(newPath);

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
        var dialog = new GSProPathDialog(_settingsService.GSProPath)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            var oldPath = _settingsService.GSProPath;
            var newPath = dialog.SelectedPath;

            if (oldPath != newPath)
            {
                _settingsService.GSProPath = newPath;
                _settingsService.SaveSettings();
                ShotListView.SetGSProPathTooltip(newPath);

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
            ShotListView.ExportCsvButtonControl.ToolTip = "Export selected shots to CSV";
            ShotListView.ExportCsvButtonControl.IsEnabled = true;
        }
        else
        {
            ShotListView.ExportCsvButtonControl.ToolTip = "Export all shots to CSV";
            ShotListView.ExportCsvButtonControl.IsEnabled = hasShots;
        }
    }

    private void ExportCsvButton_Click(object? sender, RoutedEventArgs e)
    {
        var selectedShots = _shotListViewModel.SelectedShots;
        List<ShotData> shotsToExport;

        if (selectedShots.Count > 0)
        {
            shotsToExport = selectedShots.ToList();
        }
        else
        {
            shotsToExport = _shotListViewModel.Shots.ToList();
        }

        if (shotsToExport.Count == 0)
        {
            MessageDialog.Show(this, "Export", "No shots to export.", MessageDialogType.Information);
            return;
        }

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
            }
            else
            {
                ShotDataExporter.ExportToGSProCsv(shotsToExport, dialog.FileName, silent: true);
            }
        }
    }

    private async void ImportCsvButton_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            DefaultExt = ".csv",
            Title = "Import GSPro CSV"
        };

        if (dialog.ShowDialog() != true)
            return;

        var importResult = ShotDataImporter.ImportFromGSProCsv(dialog.FileName);

        if (importResult.Shots.Count == 0)
        {
            var errorMsg = importResult.Errors.Count > 0
                ? string.Join("\n", importResult.Errors)
                : "No shots found in file.";
            MessageDialog.Show(this, "Import Failed", errorMsg, MessageDialogType.Error);
            return;
        }

        // Deduplicate against existing shots
        var existingNames = await _shotDataService.Repository.GetSyncedDirectoryNamesAsync();
        var existingSet = new HashSet<string>(existingNames);
        var newShots = importResult.Shots.Where(s => !existingSet.Contains(s.DirectoryName)).ToList();
        int duplicateCount = importResult.Shots.Count - newShots.Count;

        if (newShots.Count == 0)
        {
            MessageDialog.Show(this, "Import",
                "All shots in this file have already been imported.", MessageDialogType.Information);
            return;
        }

        // Insert into database
        int imported = 0;
        var dbErrors = new List<string>();

        foreach (var shot in newShots)
        {
            try
            {
                await _shotDataService.Repository.InsertShotAsync(shot);
                imported++;
            }
            catch (Exception ex)
            {
                dbErrors.Add($"Shot {shot.ShotNumber}: {ex.Message}");
            }
        }

        // Refresh
        _shotDataService.ClearCache();
        await LoadShotsAsync();

        // Show results
        var message = $"Imported {imported} shot(s) from CSV.";
        if (duplicateCount > 0)
            message += $"\n{duplicateCount} duplicate(s) skipped.";
        if (importResult.SkippedRows > 0)
            message += $"\n{importResult.SkippedRows} row(s) skipped due to parse errors.";
        if (dbErrors.Count > 0)
            message += $"\n{dbErrors.Count} database error(s).";

        MessageDialog.Show(this, "Import Complete", message, MessageDialogType.Information);
    }

    private async Task AutoSyncShotAsync(ShotData shot)
    {
        try
        {
            var result = await Task.Run(() => _syncService.SyncAsync(new[] { shot }, CancellationToken.None));

            if (result.Success && result.ShotsProcessed > 0)
            {
                _shotDataService.ClearCache();
                await LoadShotsAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Auto-sync failed: {ex.Message}");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _fileWatcher.Dispose();
        base.OnClosed(e);
    }
}
