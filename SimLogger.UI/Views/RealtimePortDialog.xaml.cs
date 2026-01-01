using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace SimLogger.UI.Views;

public partial class RealtimePortDialog : Window
{
    // Windows DWM API for dark title bar
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public int SelectedPort { get; private set; }

    public RealtimePortDialog(int currentPort)
    {
        InitializeComponent();

        // Enable dark title bar
        SourceInitialized += (s, e) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int value = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
        };

        // Set current value
        if (currentPort > 0)
        {
            PortTextBox.Text = currentPort.ToString();
        }
    }

    private void PortTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // Only allow numeric input
        e.Handled = !Regex.IsMatch(e.Text, @"^\d+$");
    }

    private bool ValidateInput(out int port)
    {
        port = 0;

        var portText = PortTextBox.Text.Trim();
        if (string.IsNullOrEmpty(portText))
        {
            MessageDialog.Show(this, "Validation Error", "Please enter a port number.", MessageDialogType.Warning);
            return false;
        }

        if (!int.TryParse(portText, out port) || port < 1 || port > 65535)
        {
            MessageDialog.Show(this, "Validation Error", "Port must be a number between 1 and 65535.", MessageDialogType.Warning);
            return false;
        }

        return true;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (ValidateInput(out int port))
        {
            SelectedPort = port;
            DialogResult = true;
            Close();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
