using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using SimLogger.Core.Exporters;

namespace SimLogger.UI.Views;

public partial class ExportFormatDialog : Window
{
    // Windows DWM API for dark title bar
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public ExportFormat SelectedFormat { get; private set; } = ExportFormat.GSPro;

    public ExportFormatDialog()
    {
        InitializeComponent();

        // Enable dark title bar
        SourceInitialized += (s, e) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int value = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
        };
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (ShotPatternFormatRadio.IsChecked == true)
            SelectedFormat = ExportFormat.ShotPattern;
        else
            SelectedFormat = ExportFormat.GSPro;

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
