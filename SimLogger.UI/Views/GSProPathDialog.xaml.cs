using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;

namespace SimLogger.UI.Views;

public partial class GSProPathDialog : Window
{
    // Windows DWM API for dark title bar
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public string? SelectedPath { get; private set; }

    public GSProPathDialog(string? currentPath)
    {
        InitializeComponent();

        // Enable dark title bar
        SourceInitialized += (s, e) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int value = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
        };

        // Set current path or try to auto-detect
        if (!string.IsNullOrEmpty(currentPath))
        {
            PathTextBox.Text = currentPath;
            CurrentPathText.Text = $"Current: {currentPath}";
        }
        else
        {
            var detectedPath = AutoDetectGSProPath();
            PathTextBox.Text = detectedPath ?? "";
            CurrentPathText.Text = "Current: Using auto-detected location";
        }

        SelectedPath = PathTextBox.Text;
        UpdateStatus();
    }

    private static string? AutoDetectGSProPath()
    {
        // Default to AppData LocalLow
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low";
        var appDataGsProPath = Path.Combine(appDataPath, "GSPro", "GSPro");
        if (File.Exists(Path.Combine(appDataGsProPath, "GSPro.db")))
            return appDataGsProPath;

        return null;
    }

    private void UpdateStatus()
    {
        var path = PathTextBox.Text;
        var dbPath = string.IsNullOrEmpty(path) ? null : Path.Combine(path, "GSPro.db");
        var dbExists = !string.IsNullOrEmpty(dbPath) && File.Exists(dbPath);

        if (dbExists)
        {
            StatusIcon.Kind = PackIconKind.CheckCircle;
            StatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
            StatusText.Text = "GSPro database found";
            DatabasePathText.Text = $"Database: {dbPath}";
            OkButton.IsEnabled = true;
        }
        else if (string.IsNullOrEmpty(path))
        {
            StatusIcon.Kind = PackIconKind.HelpCircle;
            StatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(255, 152, 0)); // Orange
            StatusText.Text = "No path selected";
            DatabasePathText.Text = "";
            OkButton.IsEnabled = false;
        }
        else
        {
            StatusIcon.Kind = PackIconKind.AlertCircle;
            StatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
            StatusText.Text = "GSPro.db not found in this folder";
            DatabasePathText.Text = $"Expected: {dbPath}";
            OkButton.IsEnabled = false;
        }
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select GSPro Installation Folder",
            InitialDirectory = string.IsNullOrEmpty(PathTextBox.Text) ? @"C:\" : PathTextBox.Text
        };

        if (dialog.ShowDialog() == true)
        {
            PathTextBox.Text = dialog.FolderName;
            SelectedPath = dialog.FolderName;
            UpdateStatus();
        }
    }

    private void AutoDetectButton_Click(object sender, RoutedEventArgs e)
    {
        var detectedPath = AutoDetectGSProPath();
        if (detectedPath != null)
        {
            PathTextBox.Text = detectedPath;
            SelectedPath = detectedPath;
            UpdateStatus();
        }
        else
        {
            MessageDialog.Show(this, "Not Found",
                "Could not automatically detect GSPro installation.\n\n" +
                "Please browse to the folder containing GSPro.db manually.\n\n" +
                "Default location:\n" +
                "%LOCALAPPDATA%Low\\GSPro\\GSPro",
                MessageDialogType.Warning);
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var path = PathTextBox.Text;

        // Validate the path
        if (string.IsNullOrWhiteSpace(path))
        {
            MessageDialog.Show(this, "Invalid Path", "Please select a valid folder path.", MessageDialogType.Warning);
            return;
        }

        // Check if database exists
        var dbPath = Path.Combine(path, "GSPro.db");
        if (!File.Exists(dbPath))
        {
            MessageDialog.Show(this, "Database Not Found",
                $"GSPro.db was not found in the selected folder.\n\nExpected: {dbPath}",
                MessageDialogType.Error);
            return;
        }

        SelectedPath = path;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
