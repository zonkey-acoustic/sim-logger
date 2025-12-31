using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using SimLogger.UI.Services;

namespace SimLogger.UI.Views;

public partial class AudioDeviceDialog : Window
{
    private readonly AudioTriggerService _audioService;
    private bool _isInitialized = false;

    // Windows DWM API for dark title bar
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public AudioDeviceInfo? SelectedDevice { get; private set; }

    public AudioDeviceDialog(AudioTriggerService audioService)
    {
        InitializeComponent();
        _audioService = audioService;

        // Enable dark title bar
        SourceInitialized += (s, e) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int value = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
        };

        LoadDevices();
        LoadToneParameters();
        _isInitialized = true;
    }

    private void LoadDevices()
    {
        var devices = _audioService.GetAudioOutputDevices();
        DeviceListBox.ItemsSource = devices;

        // Attach selection changed handler first
        DeviceListBox.SelectionChanged += (s, e) =>
        {
            UpdateButtonStates();
        };

        // Select current device if set
        if (_audioService.SelectedDeviceIndex >= 0)
        {
            var currentDevice = devices.FirstOrDefault(d => d.Index == _audioService.SelectedDeviceIndex);
            if (currentDevice != null)
            {
                DeviceListBox.SelectedItem = currentDevice;
                CurrentDeviceText.Text = $"Current: {_audioService.SelectedDeviceName}";
            }
        }

        // Update button states based on initial selection
        UpdateButtonStates();
    }

    private void LoadToneParameters()
    {
        FrequencySlider.Value = _audioService.ToneFrequencyHz;
        NoiseDecaySlider.Value = _audioService.ToneNoiseDecay;
        ToneDecaySlider.Value = _audioService.ToneToneDecay;
        ToneMixSlider.Value = _audioService.ToneMix;
        DurationSlider.Value = _audioService.ToneDurationMs;
    }

    private void UpdateButtonStates()
    {
        var hasSelection = DeviceListBox.SelectedItem != null;
        TestButton.IsEnabled = hasSelection;
        OkButton.IsEnabled = hasSelection;
    }

    private void ToneParameter_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized) return;

        // Update service properties immediately for real-time preview
        _audioService.ToneFrequencyHz = FrequencySlider.Value;
        _audioService.ToneNoiseDecay = NoiseDecaySlider.Value;
        _audioService.ToneToneDecay = ToneDecaySlider.Value;
        _audioService.ToneMix = ToneMixSlider.Value;
        _audioService.ToneDurationMs = DurationSlider.Value;
    }

    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        FrequencySlider.Value = 5800;
        NoiseDecaySlider.Value = 60;
        ToneDecaySlider.Value = 200;
        ToneMixSlider.Value = 0.1;
        DurationSlider.Value = 500;
    }

    private void TestButton_Click(object sender, RoutedEventArgs e)
    {
        if (DeviceListBox.SelectedItem is AudioDeviceInfo device)
        {
            _audioService.TestTone(device.Index);
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (DeviceListBox.SelectedItem is AudioDeviceInfo device)
        {
            SelectedDevice = device;

            // Save tone parameters to service
            _audioService.ToneFrequencyHz = FrequencySlider.Value;
            _audioService.ToneNoiseDecay = NoiseDecaySlider.Value;
            _audioService.ToneToneDecay = ToneDecaySlider.Value;
            _audioService.ToneMix = ToneMixSlider.Value;
            _audioService.ToneDurationMs = DurationSlider.Value;

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
