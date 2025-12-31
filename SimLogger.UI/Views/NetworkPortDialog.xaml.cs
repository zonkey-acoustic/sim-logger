using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using SimLogger.UI.Services;

namespace SimLogger.UI.Views;

public partial class NetworkPortDialog : Window
{
    private readonly NetworkTriggerService _networkService;

    // Windows DWM API for dark title bar
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public int SelectedPort { get; private set; }
    public string SelectedHost { get; private set; } = "127.0.0.1";

    public NetworkPortDialog(NetworkTriggerService networkService, int currentPort, string? currentHost)
    {
        InitializeComponent();
        _networkService = networkService;

        // Enable dark title bar
        SourceInitialized += (s, e) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int value = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
        };

        // Set current values
        HostTextBox.Text = currentHost ?? "127.0.0.1";
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

    private bool ValidateInput(out int port, out string host)
    {
        port = 0;
        host = HostTextBox.Text.Trim();

        if (string.IsNullOrEmpty(host))
        {
            host = "127.0.0.1";
        }

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

    private void TestButton_Click(object sender, RoutedEventArgs e)
    {
        if (ValidateInput(out int port, out string host))
        {
            _networkService.TestPacket(port, host);
            MessageDialog.Show(this, "Test", $"Test packet sent to {host}:{port}", MessageDialogType.Information);
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (ValidateInput(out int port, out string host))
        {
            SelectedPort = port;
            SelectedHost = host;
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
