using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using SimLogger.Core.Models;

using SimLogger.UI.ViewModels;

namespace SimLogger.UI.Views;

public partial class ShotListView : UserControl
{
    // Events for MainWindow to subscribe to
    public event RoutedEventHandler? SyncButtonClick;
    public event RoutedEventHandler? CancelSyncButtonClick;
    public event RoutedEventHandler? ExportCsvButtonClick;
    public event RoutedEventHandler? DataStorageButtonClick;
    public event RoutedEventHandler? GSProPathButtonClick;
    public event RoutedEventHandler? ImportCsvButtonClick;

    // Event for column reorder to save preferences
    public event EventHandler<List<string>>? ColumnOrderChanged;

    // Event for column visibility changes
    public event EventHandler<List<string>>? ColumnVisibilityChanged;
    public event RoutedEventHandler? ColumnsButtonClick;

    // Event for tag editing
    public event EventHandler<List<ShotData>>? EditTagsRequested;

    // Event for shot deletion
    public event EventHandler<List<ShotData>>? DeleteShotsRequested;

    public ShotListView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        ShotDataGrid.ColumnReordered += OnColumnReordered;
        ShotDataGrid.Sorting += OnDataGridSorting;
    }

    private void OnColumnReordered(object? sender, DataGridColumnEventArgs e)
    {
        // Get current column order by display index
        var columnOrder = ShotDataGrid.Columns
            .OrderBy(c => c.DisplayIndex)
            .Select(c => c.Header?.ToString() ?? string.Empty)
            .Where(h => !string.IsNullOrEmpty(h))
            .ToList();

        ColumnOrderChanged?.Invoke(this, columnOrder);
    }

    private void OnDataGridSorting(object sender, DataGridSortingEventArgs e)
    {
        // Get the binding path from the column first
        var bindingPath = GetColumnBindingPath(e.Column);
        if (string.IsNullOrEmpty(bindingPath))
        {
            // Let default sorting handle non-bound columns (like template columns)
            return;
        }

        var view = CollectionViewSource.GetDefaultView(ShotDataGrid.ItemsSource);
        if (view == null)
            return;

        e.Handled = true;

        // Toggle sort direction (null -> Ascending -> Descending -> Ascending)
        ListSortDirection direction;
        if (e.Column.SortDirection == ListSortDirection.Ascending)
            direction = ListSortDirection.Descending;
        else
            direction = ListSortDirection.Ascending;

        // Clear other column sort indicators
        foreach (var col in ShotDataGrid.Columns)
        {
            if (col != e.Column)
                col.SortDirection = null;
        }

        e.Column.SortDirection = direction;

        // Sort using ListCollectionView for custom sorting
        if (view is ListCollectionView lcv)
        {
            lcv.CustomSort = new NumericStringComparer(bindingPath, direction);
            lcv.Refresh();
        }
        else
        {
            // Fallback for other view types - use standard sort
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription(bindingPath, direction));
        }
    }

    private static string? GetColumnBindingPath(DataGridColumn column)
    {
        if (column is DataGridBoundColumn boundColumn &&
            boundColumn.Binding is Binding binding)
        {
            return binding.Path.Path;
        }
        return null;
    }

    private class NumericStringComparer : System.Collections.IComparer
    {
        private readonly string _propertyPath;
        private readonly ListSortDirection _direction;

        public NumericStringComparer(string propertyPath, ListSortDirection direction)
        {
            _propertyPath = propertyPath;
            _direction = direction;
        }

        public int Compare(object? x, object? y)
        {
            var valueX = GetPropertyValue(x, _propertyPath);
            var valueY = GetPropertyValue(y, _propertyPath);

            int result = CompareValues(valueX, valueY);

            // For descending, reverse the comparison result
            return _direction == ListSortDirection.Descending ? -result : result;
        }

        private static object? GetPropertyValue(object? obj, string propertyPath)
        {
            if (obj == null || string.IsNullOrEmpty(propertyPath))
                return null;

            var parts = propertyPath.Split('.');
            object? current = obj;

            foreach (var part in parts)
            {
                if (current == null)
                    return null;

                var prop = current.GetType().GetProperty(part);
                if (prop == null)
                    return null;

                current = prop.GetValue(current);
            }

            return current;
        }

        private static int CompareValues(object? x, object? y)
        {
            // Handle nulls - push nulls to the end
            if (x == null && y == null) return 0;
            if (x == null) return 1;  // null goes after non-null
            if (y == null) return -1; // non-null goes before null

            // Try to convert both to double for numeric comparison
            var numX = TryGetNumericValue(x);
            var numY = TryGetNumericValue(y);

            if (numX.HasValue && numY.HasValue)
            {
                return numX.Value.CompareTo(numY.Value);
            }

            // If one is numeric and one isn't, numeric comes first
            if (numX.HasValue) return -1;
            if (numY.HasValue) return 1;

            // Fall back to string comparison
            var strX = x.ToString() ?? string.Empty;
            var strY = y.ToString() ?? string.Empty;

            // Handle empty strings - push to end
            if (string.IsNullOrEmpty(strX) && string.IsNullOrEmpty(strY)) return 0;
            if (string.IsNullOrEmpty(strX)) return 1;
            if (string.IsNullOrEmpty(strY)) return -1;

            return string.Compare(strX, strY, StringComparison.OrdinalIgnoreCase);
        }

        private static string ExtractNumericValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            // Find the end of the numeric portion (digits, decimal point, minus sign)
            int i = 0;
            bool hasDecimal = false;

            // Handle leading minus sign
            if (i < value.Length && value[i] == '-')
                i++;

            while (i < value.Length)
            {
                char c = value[i];
                if (char.IsDigit(c))
                {
                    i++;
                }
                else if (c == '.' && !hasDecimal)
                {
                    hasDecimal = true;
                    i++;
                }
                else
                {
                    break;
                }
            }

            return i > 0 ? value.Substring(0, i) : value;
        }

        private static double? TryGetNumericValue(object? value)
        {
            if (value == null) return null;

            // Handle direct numeric types
            if (value is double d) return d;
            if (value is float f) return f;
            if (value is int i) return i;
            if (value is long l) return l;
            if (value is decimal dec) return (double)dec;

            // Handle nullable numeric types
            var type = value.GetType();
            var underlyingType = Nullable.GetUnderlyingType(type);
            if (underlyingType != null)
            {
                // It's a nullable type - get the underlying value
                if (underlyingType == typeof(double) || underlyingType == typeof(float) ||
                    underlyingType == typeof(int) || underlyingType == typeof(long) ||
                    underlyingType == typeof(decimal))
                {
                    return Convert.ToDouble(value);
                }
            }

            // Try parsing string representation
            var str = value.ToString();
            if (!string.IsNullOrEmpty(str))
            {
                var numericStr = ExtractNumericValue(str);
                if (double.TryParse(numericStr, out var parsed))
                {
                    return parsed;
                }
            }

            return null;
        }
    }

    public void ApplyColumnOrder(List<string>? columnOrder)
    {
        if (columnOrder == null || columnOrder.Count == 0)
            return;

        // Create a dictionary of header to desired display index
        var orderMap = new Dictionary<string, int>();
        for (int i = 0; i < columnOrder.Count; i++)
        {
            orderMap[columnOrder[i]] = i;
        }

        // Apply display indices to columns
        foreach (var column in ShotDataGrid.Columns)
        {
            var header = column.Header?.ToString();
            if (!string.IsNullOrEmpty(header) && orderMap.TryGetValue(header, out int displayIndex))
            {
                column.DisplayIndex = displayIndex;
            }
        }
    }

    // Expose controls for MainWindow to update
    public Button SyncButtonControl => SyncButton;
    public StackPanel SyncProgressPanelControl => SyncProgressPanel;
    public ProgressBar SyncProgressBarControl => SyncProgressBar;
    public TextBlock SyncStatusTextControl => SyncStatusText;
    public Button CancelSyncButtonControl => CancelSyncButton;
    public Button ExportCsvButtonControl => ExportCsvButton;
    public Button DataStorageButtonControl => DataStorageButton;
    public Button GSProPathButtonControl => GSProPathButton;


    public void SetDataStorageTooltip(string? path)
    {
        DataStorageButton.ToolTip = string.IsNullOrEmpty(path)
            ? "Data storage: Default location\nClick to change"
            : $"Data storage: {path}\nClick to change";
    }

    public void SetGSProPathTooltip(string? path)
    {
        GSProPathButton.ToolTip = string.IsNullOrEmpty(path)
            ? "GSPro: Auto-detected\nClick to change"
            : $"GSPro: {path}\nClick to change";
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ShotListViewModel oldVm)
        {
            oldVm.SelectedShots.CollectionChanged -= OnSelectionCollectionChanged;
        }

        if (e.NewValue is ShotListViewModel newVm)
        {
            newVm.SelectedShots.CollectionChanged += OnSelectionCollectionChanged;
        }
    }

    private void OnSelectionCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            // Collection was cleared - uncheck all checkboxes
            UpdateAllCheckboxes(false);
            UpdateSelectAllCheckbox();
        }
        else
        {
            UpdateSelectAllCheckbox();
        }
    }

    private void UpdateSelectAllCheckbox()
    {
        var headerCheckBox = FindColumnHeaderCheckBox();
        if (headerCheckBox == null) return;

        if (DataContext is ShotListViewModel vm && vm.Shots.Count > 0)
        {
            bool allSelected = vm.Shots.All(s => vm.SelectedShots.Contains(s));
            headerCheckBox.IsChecked = allSelected;
        }
        else
        {
            headerCheckBox.IsChecked = false;
        }
    }

    private CheckBox? FindColumnHeaderCheckBox()
    {
        // Find the header presenter for the first column (selection column)
        var headerPresenter = FindVisualChild<DataGridColumnHeadersPresenter>(ShotDataGrid);
        if (headerPresenter != null)
        {
            return FindVisualChild<CheckBox>(headerPresenter);
        }
        return null;
    }

    private void UpdateAllCheckboxes(bool isChecked)
    {
        foreach (var item in ShotDataGrid.Items)
        {
            var row = ShotDataGrid.ItemContainerGenerator.ContainerFromItem(item) as DataGridRow;
            if (row != null)
            {
                var checkbox = FindVisualChild<CheckBox>(row);
                if (checkbox != null)
                {
                    checkbox.IsChecked = isChecked;
                }
            }
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                return typedChild;
            }

            var result = FindVisualChild<T>(child);
            if (result != null)
            {
                return result;
            }
        }
        return null;
    }

    private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && DataContext is ShotListViewModel vm)
        {
            if (checkBox.IsChecked == true)
            {
                // Select all shots on the current page
                foreach (var shot in vm.Shots)
                {
                    if (!vm.SelectedShots.Contains(shot))
                    {
                        vm.SelectedShots.Add(shot);
                    }
                }
            }
            else
            {
                // Deselect all shots on the current page
                foreach (var shot in vm.Shots.ToList())
                {
                    vm.SelectedShots.Remove(shot);
                }
            }

            // Update all visible row checkboxes
            UpdateAllCheckboxes(checkBox.IsChecked == true);
        }
    }

    private void ShotDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        if (e.Row.Item is ShotData shot && DataContext is ShotListViewModel vm)
        {
            // Defer to allow the visual tree to be fully built for recycled rows
            Dispatcher.BeginInvoke(() =>
            {
                var checkbox = FindVisualChild<CheckBox>(e.Row);
                if (checkbox != null)
                {
                    checkbox.IsChecked = vm.SelectedShots.Contains(shot);
                }
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private void SelectionCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.Tag is ShotData shot)
        {
            if (DataContext is ShotListViewModel vm)
            {
                vm.ToggleShotSelectionCommand.Execute(shot);
            }
        }
    }

    // Sync button click handlers - raise events for MainWindow
    private void SyncButton_Click(object sender, RoutedEventArgs e)
    {
        SyncButtonClick?.Invoke(sender, e);
    }

    private void CancelSyncButton_Click(object sender, RoutedEventArgs e)
    {
        CancelSyncButtonClick?.Invoke(sender, e);
    }

    private void ExportCsvButton_Click(object sender, RoutedEventArgs e)
    {
        ExportCsvButtonClick?.Invoke(sender, e);
    }

    private void DataStorageButton_Click(object sender, RoutedEventArgs e)
    {
        DataStorageButtonClick?.Invoke(sender, e);
    }

    private void GSProPathButton_Click(object sender, RoutedEventArgs e)
    {
        GSProPathButtonClick?.Invoke(sender, e);
    }

    private void ColumnsButton_Click(object sender, RoutedEventArgs e)
    {
        ColumnsButtonClick?.Invoke(sender, e);
    }

    private void ImportCsvButton_Click(object sender, RoutedEventArgs e)
    {
        ImportCsvButtonClick?.Invoke(sender, e);
    }

    private void EditTags_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ShotListViewModel vm)
        {
            // Use selected shots if any, otherwise use the single selected shot
            var shots = vm.SelectedShots.Count > 0
                ? vm.SelectedShots.ToList()
                : vm.SelectedShot != null
                    ? new List<ShotData> { vm.SelectedShot }
                    : new List<ShotData>();

            if (shots.Count > 0)
            {
                EditTagsRequested?.Invoke(this, shots);
            }
        }
    }

    private void DeleteShots_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ShotListViewModel vm)
        {
            var shots = vm.SelectedShots.Count > 0
                ? vm.SelectedShots.ToList()
                : vm.SelectedShot != null
                    ? new List<ShotData> { vm.SelectedShot }
                    : new List<ShotData>();

            if (shots.Count > 0)
            {
                DeleteShotsRequested?.Invoke(this, shots);
            }
        }
    }

    public List<string> GetAllColumnHeaders()
    {
        return ShotDataGrid.Columns
            .Select(c => c.Header?.ToString() ?? string.Empty)
            .Where(h => !string.IsNullOrEmpty(h))
            .ToList();
    }

    public void ApplyColumnVisibility(List<string>? hiddenColumns)
    {
        if (hiddenColumns == null)
            return;

        var hiddenSet = new HashSet<string>(hiddenColumns);

        foreach (var column in ShotDataGrid.Columns)
        {
            var header = column.Header?.ToString();
            if (!string.IsNullOrEmpty(header) && header != "Sel")
            {
                column.Visibility = hiddenSet.Contains(header)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }
    }

    public void SetColumnVisibility(List<string> hiddenColumns)
    {
        ApplyColumnVisibility(hiddenColumns);
        ColumnVisibilityChanged?.Invoke(this, hiddenColumns);
    }
}
