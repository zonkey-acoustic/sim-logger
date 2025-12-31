using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimLogger.UI.Services;

namespace SimLogger.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ShotDataService _shotDataService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private int _totalShots;

    [ObservableProperty]
    private int _currentViewIndex;

    [ObservableProperty]
    private ShotListViewModel _shotListViewModel;

    public MainViewModel()
    {
        _shotDataService = new ShotDataService();
        _shotListViewModel = new ShotListViewModel(_shotDataService);
    }

    public MainViewModel(ShotListViewModel shotListVm)
    {
        _shotDataService = new ShotDataService();
        _shotListViewModel = shotListVm;
    }

    [RelayCommand]
    private async Task LoadShotsAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading shots...";

        try
        {
            var shots = await _shotDataService.LoadShotsAsync(forceReload: true);
            TotalShots = shots.Count;
            await ShotListViewModel.RefreshAsync();
            StatusMessage = $"Loaded {TotalShots} shots";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ShowShotList()
    {
        CurrentViewIndex = 0;
    }
}
