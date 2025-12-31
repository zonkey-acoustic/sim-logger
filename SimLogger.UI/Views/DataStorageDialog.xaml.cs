using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace SimLogger.UI.Views;

public partial class DataStorageDialog : Window
{
    private readonly string _defaultPath;

    // Windows DWM API for dark title bar
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public string? SelectedPath { get; private set; }

    public DataStorageDialog(string? currentPath)
    {
        InitializeComponent();

        // Enable dark title bar
        SourceInitialized += (s, e) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int value = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
        };

        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        _defaultPath = Path.Combine(documentsPath, "SimLogger");

        // Set current path in text box
        PathTextBox.Text = currentPath ?? _defaultPath;
        SelectedPath = PathTextBox.Text;

        // Show info text
        if (!string.IsNullOrEmpty(currentPath))
        {
            CurrentPathText.Text = $"Current: {currentPath}";
        }
        else
        {
            CurrentPathText.Text = "Current: Using default location";
        }
        DefaultPathText.Text = $"Default: {_defaultPath}";
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Data Storage Folder",
            InitialDirectory = PathTextBox.Text
        };

        if (dialog.ShowDialog() == true)
        {
            PathTextBox.Text = dialog.FolderName;
            SelectedPath = dialog.FolderName;
        }
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        PathTextBox.Text = _defaultPath;
        SelectedPath = null; // null means use default
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

        // Check if path is the default (store as null)
        if (path.Equals(_defaultPath, StringComparison.OrdinalIgnoreCase))
        {
            SelectedPath = null;
        }
        else
        {
            // Try to create the directory if it doesn't exist
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                SelectedPath = path;
            }
            catch (Exception ex)
            {
                MessageDialog.Show(this, "Error", $"Cannot create or access the selected folder: {ex.Message}", MessageDialogType.Error);
                return;
            }
        }

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
