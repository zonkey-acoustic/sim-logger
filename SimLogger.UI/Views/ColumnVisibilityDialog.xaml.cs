using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SimLogger.UI.Views;

public partial class ColumnVisibilityDialog : Window
{
    // Windows DWM API for dark title bar
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public List<ColumnVisibilityItem> Columns { get; private set; }
    public List<string> HiddenColumns { get; private set; } = new();

    public ColumnVisibilityDialog(List<string> allColumns, List<string>? currentlyHidden)
    {
        InitializeComponent();

        // Enable dark title bar
        SourceInitialized += (s, e) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int value = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
        };

        // Build the column list with current visibility state, sorted alphabetically
        var hiddenSet = new HashSet<string>(currentlyHidden ?? new List<string>());
        Columns = allColumns
            .Where(c => !string.IsNullOrEmpty(c) && c != "Sel") // Exclude selection checkbox
            .OrderBy(c => c)
            .Select(c => new ColumnVisibilityItem
            {
                Header = c,
                IsVisible = !hiddenSet.Contains(c)
            })
            .ToList();

        ColumnList.ItemsSource = Columns;
    }

    private void ShowAllButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var column in Columns)
        {
            column.IsVisible = true;
        }
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        HiddenColumns = Columns
            .Where(c => !c.IsVisible)
            .Select(c => c.Header)
            .ToList();

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

public class ColumnVisibilityItem : INotifyPropertyChanged
{
    private bool _isVisible = true;

    public string Header { get; set; } = string.Empty;

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVisible)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
