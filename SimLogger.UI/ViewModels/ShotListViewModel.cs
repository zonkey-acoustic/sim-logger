using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimLogger.Core.Models;
using SimLogger.UI.Services;

namespace SimLogger.UI.ViewModels;

public partial class ShotListViewModel : ObservableObject
{
    private readonly ShotDataService _shotDataService;
    private List<ShotData> _allShots = new();
    private List<ShotData> _filteredShots = new();

    [ObservableProperty]
    private ObservableCollection<ShotData> _shots = new();

    [ObservableProperty]
    private ObservableCollection<string> _clubFilters = new() { "All Clubs" };

    [ObservableProperty]
    private string _selectedClubFilter = "All Clubs";

    [ObservableProperty]
    private ShotData? _selectedShot;

    [ObservableProperty]
    private ObservableCollection<ShotData> _selectedShots = new();

    // Pagination properties
    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _pageSize = 50;

    [ObservableProperty]
    private int _totalFilteredCount;

    [ObservableProperty]
    private int _totalPages = 1;

    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;

    public int DisplayStartIndex => TotalFilteredCount == 0 ? 0 : (CurrentPage - 1) * PageSize + 1;
    public int DisplayEndIndex => Math.Min(CurrentPage * PageSize, TotalFilteredCount);

    public ShotListViewModel(ShotDataService shotDataService)
    {
        _shotDataService = shotDataService;
    }

    partial void OnCurrentPageChanged(int value)
    {
        ApplyPagination();
        OnPropertyChanged(nameof(HasPreviousPage));
        OnPropertyChanged(nameof(HasNextPage));
        OnPropertyChanged(nameof(DisplayStartIndex));
        OnPropertyChanged(nameof(DisplayEndIndex));
    }

    partial void OnPageSizeChanged(int value)
    {
        CurrentPage = 1;
        UpdatePaginationInfo();
        ApplyPagination();
    }

    public async Task RefreshAsync()
    {
        var shots = await _shotDataService.LoadShotsAsync();
        _allShots = shots;

        // Update club filters
        var clubs = _shotDataService.GetUniqueClubNames().ToList();
        ClubFilters.Clear();
        ClubFilters.Add("All Clubs");
        foreach (var club in clubs)
        {
            ClubFilters.Add(club);
        }

        // Ensure "All Clubs" is selected after rebuilding the filter list
        SelectedClubFilter = "All Clubs";

        ApplyFilters();
    }

    partial void OnSelectedClubFilterChanged(string value)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var filtered = _allShots.AsEnumerable();

        // Filter by club
        if (!string.IsNullOrEmpty(SelectedClubFilter) && SelectedClubFilter != "All Clubs")
        {
            filtered = filtered.Where(s => s.ClubData?.ClubName == SelectedClubFilter);
        }

        // Store filtered results for pagination
        _filteredShots = filtered.ToList();

        // Reset to page 1 when filters change and update pagination info
        CurrentPage = 1;
        UpdatePaginationInfo();
        ApplyPagination();
    }

    private void UpdatePaginationInfo()
    {
        TotalFilteredCount = _filteredShots.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling((double)TotalFilteredCount / PageSize));

        // Ensure current page is valid
        if (CurrentPage > TotalPages)
        {
            CurrentPage = TotalPages;
        }

        OnPropertyChanged(nameof(HasPreviousPage));
        OnPropertyChanged(nameof(HasNextPage));
        OnPropertyChanged(nameof(DisplayStartIndex));
        OnPropertyChanged(nameof(DisplayEndIndex));
    }

    private void ApplyPagination()
    {
        var pagedShots = _filteredShots
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize);

        // Clear and re-add instead of replacing collection to ensure UI updates
        Shots.Clear();
        foreach (var shot in pagedShots)
        {
            Shots.Add(shot);
        }

        // Notify that Shots has been updated (for button state updates)
        OnPropertyChanged(nameof(Shots));
    }

    [RelayCommand]
    private void FirstPage()
    {
        if (HasPreviousPage)
        {
            CurrentPage = 1;
        }
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (HasPreviousPage)
        {
            CurrentPage--;
        }
    }

    [RelayCommand]
    private void NextPage()
    {
        if (HasNextPage)
        {
            CurrentPage++;
        }
    }

    [RelayCommand]
    private void LastPage()
    {
        if (HasNextPage)
        {
            CurrentPage = TotalPages;
        }
    }

    [RelayCommand]
    private void ToggleShotSelection(ShotData? shot)
    {
        if (shot == null) return;

        if (SelectedShots.Contains(shot))
        {
            SelectedShots.Remove(shot);
        }
        else
        {
            SelectedShots.Add(shot);
        }
    }

    [RelayCommand]
    private void ClearSelection()
    {
        SelectedShots.Clear();
    }

    public string GetBallSpeed(ShotData shot)
    {
        return shot.BallData?.Speed ?? "-";
    }

    public string GetClubSpeed(ShotData shot)
    {
        return shot.ClubData?.Speed ?? "-";
    }

    public string GetCarry(ShotData shot)
    {
        return shot.FlightData?.Carry ?? "-";
    }

    public string GetTotalSpin(ShotData shot)
    {
        return shot.BallData?.TotalSpin ?? "-";
    }
}
