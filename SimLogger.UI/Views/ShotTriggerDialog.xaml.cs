using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using SimLogger.UI.Services;

namespace SimLogger.UI.Views;

public partial class ShotTriggerDialog : Window
{
    private readonly AudioTriggerService _audioService;
    private readonly NetworkTriggerService _networkService;
    private bool _isInitialized;

    // Windows DWM API for dark title bar
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public bool UseAudioTrigger { get; private set; }
    public bool UseNetworkTrigger { get; private set; }
    public AudioDeviceInfo? SelectedAudioDevice { get; private set; }
    public string NetworkHost { get; private set; } = "127.0.0.1";
    public int NetworkPort { get; private set; }
    public int RealtimePort { get; private set; }

    // Tone parameters
    public double ToneFrequencyHz { get; private set; }
    public double ToneNoiseDecay { get; private set; }
    public double ToneToneDecay { get; private set; }
    public double ToneMix { get; private set; }
    public double ToneDurationMs { get; private set; }

    public ShotTriggerDialog(
        AudioTriggerService audioService,
        NetworkTriggerService networkService,
        bool currentUseAudio,
        bool currentUseNetwork,
        int currentNetworkPort,
        string? currentNetworkHost,
        int currentRealtimePort)
    {
        InitializeComponent();
        _audioService = audioService;
        _networkService = networkService;

        // Enable dark title bar
        SourceInitialized += (s, e) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int value = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
        };

        // Load audio devices
        var devices = _audioService.GetAudioOutputDevices();
        AudioDeviceCombo.ItemsSource = devices;

        // Set current audio device selection
        if (_audioService.SelectedDeviceIndex >= 0 && _audioService.SelectedDeviceIndex < devices.Count)
        {
            AudioDeviceCombo.SelectedIndex = _audioService.SelectedDeviceIndex;
        }

        // Set current values
        if (currentUseNetwork)
        {
            NetworkRadio.IsChecked = true;
        }
        else
        {
            AudioRadio.IsChecked = true;
        }

        NetworkHostTextBox.Text = currentNetworkHost ?? "127.0.0.1";
        if (currentNetworkPort > 0)
        {
            NetworkPortTextBox.Text = currentNetworkPort.ToString();
        }

        if (currentRealtimePort > 0)
        {
            RealtimePortTextBox.Text = currentRealtimePort.ToString();
        }

        // Initialize tone sliders with current values
        FrequencySlider.Value = _audioService.ToneFrequencyHz;
        NoiseDecaySlider.Value = _audioService.ToneNoiseDecay;
        ToneDecaySlider.Value = _audioService.ToneToneDecay;
        ToneMixSlider.Value = _audioService.ToneMix;
        DurationSlider.Value = _audioService.ToneDurationMs;

        _isInitialized = true;
        UpdateSliderDisplayValues();

        UpdateVisibility();
    }

    private void ToneSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateSliderDisplayValues();
    }

    private void UpdateSliderDisplayValues()
    {
        // Check if UI is fully initialized
        if (!_isInitialized) return;

        FrequencyValue.Text = $"{FrequencySlider.Value:0} Hz";
        NoiseDecayValue.Text = $"{NoiseDecaySlider.Value:0}";
        ToneDecayValue.Text = $"{ToneDecaySlider.Value:0}";
        ToneMixValue.Text = $"{ToneMixSlider.Value:0.00}";
        DurationValue.Text = $"{DurationSlider.Value:0} ms";
    }

    private void TriggerType_Changed(object sender, RoutedEventArgs e)
    {
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        if (AudioConfigGroup == null || NetworkConfigGroup == null)
            return;

        if (AudioRadio.IsChecked == true)
        {
            AudioConfigGroup.Visibility = Visibility.Visible;
            NetworkConfigGroup.Visibility = Visibility.Collapsed;
        }
        else
        {
            AudioConfigGroup.Visibility = Visibility.Collapsed;
            NetworkConfigGroup.Visibility = Visibility.Visible;
        }
    }

    private void PortTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !Regex.IsMatch(e.Text, @"^\d+$");
    }

    private void TestAudioButton_Click(object sender, RoutedEventArgs e)
    {
        if (AudioDeviceCombo.SelectedItem is AudioDeviceInfo device)
        {
            // Temporarily apply slider values for test
            var origFreq = _audioService.ToneFrequencyHz;
            var origNoiseDecay = _audioService.ToneNoiseDecay;
            var origToneDecay = _audioService.ToneToneDecay;
            var origMix = _audioService.ToneMix;
            var origDuration = _audioService.ToneDurationMs;

            _audioService.ToneFrequencyHz = FrequencySlider.Value;
            _audioService.ToneNoiseDecay = NoiseDecaySlider.Value;
            _audioService.ToneToneDecay = ToneDecaySlider.Value;
            _audioService.ToneMix = ToneMixSlider.Value;
            _audioService.ToneDurationMs = DurationSlider.Value;

            _audioService.TestTone(device.Index);

            // Restore original values (the test plays asynchronously, but the values are read at start)
            _audioService.ToneFrequencyHz = origFreq;
            _audioService.ToneNoiseDecay = origNoiseDecay;
            _audioService.ToneToneDecay = origToneDecay;
            _audioService.ToneMix = origMix;
            _audioService.ToneDurationMs = origDuration;
        }
        else
        {
            MessageDialog.Show(this, "Test", "Please select an audio device first.", MessageDialogType.Warning);
        }
    }

    private void TestNetworkButton_Click(object sender, RoutedEventArgs e)
    {
        var host = NetworkHostTextBox.Text.Trim();
        if (string.IsNullOrEmpty(host)) host = "127.0.0.1";

        if (!int.TryParse(NetworkPortTextBox.Text.Trim(), out int port) || port < 1 || port > 65535)
        {
            MessageDialog.Show(this, "Test", "Please enter a valid port number (1-65535).", MessageDialogType.Warning);
            return;
        }

        _networkService.TestPacket(port, host);
        MessageDialog.Show(this, "Test", $"Test packet sent to {host}:{port}", MessageDialogType.Information);
    }

    private bool Validate()
    {
        // Validate real-time port
        if (!int.TryParse(RealtimePortTextBox.Text.Trim(), out int realtimePort) || realtimePort < 1 || realtimePort > 65535)
        {
            MessageDialog.Show(this, "Validation Error", "Please enter a valid real-time detection port (1-65535).", MessageDialogType.Warning);
            return false;
        }
        RealtimePort = realtimePort;

        if (AudioRadio.IsChecked == true)
        {
            // Validate audio device selection
            if (AudioDeviceCombo.SelectedItem is not AudioDeviceInfo device)
            {
                MessageDialog.Show(this, "Validation Error", "Please select an audio output device.", MessageDialogType.Warning);
                return false;
            }
            SelectedAudioDevice = device;
            UseAudioTrigger = true;
            UseNetworkTrigger = false;

            // Capture tone parameters
            ToneFrequencyHz = FrequencySlider.Value;
            ToneNoiseDecay = NoiseDecaySlider.Value;
            ToneToneDecay = ToneDecaySlider.Value;
            ToneMix = ToneMixSlider.Value;
            ToneDurationMs = DurationSlider.Value;
        }
        else
        {
            // Validate network settings
            var host = NetworkHostTextBox.Text.Trim();
            if (string.IsNullOrEmpty(host)) host = "127.0.0.1";
            NetworkHost = host;

            if (!int.TryParse(NetworkPortTextBox.Text.Trim(), out int networkPort) || networkPort < 1 || networkPort > 65535)
            {
                MessageDialog.Show(this, "Validation Error", "Please enter a valid network port (1-65535).", MessageDialogType.Warning);
                return false;
            }
            NetworkPort = networkPort;
            UseAudioTrigger = false;
            UseNetworkTrigger = true;
        }

        return true;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (Validate())
        {
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
