using System.IO;
using System.Text.Json;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace SimLogger.UI.Services;

public class AudioTriggerService : IDisposable
{
    private readonly string _settingsPath;
    private WaveOutEvent? _waveOut;
    private bool _disposed;

    public bool IsEnabled { get; set; }
    public int SelectedDeviceIndex { get; set; } = -1;
    public string? SelectedDeviceName { get; set; }
    public string? DataStoragePath { get; set; }
    public string? GSProPath { get; set; }

    // Network trigger settings
    public bool NetworkTriggerEnabled { get; set; }
    public int NetworkTriggerPort { get; set; } = 8875;
    public string? NetworkTriggerHost { get; set; } = "127.0.0.1";

    // Tone envelope parameters (golf impact sound)
    public double ToneFrequencyHz { get; set; } = 5800;    // 500-8000 Hz
    public double ToneNoiseDecay { get; set; } = 60;       // 10-100 (higher = shorter)
    public double ToneToneDecay { get; set; } = 200;       // 5-500 (higher = shorter)
    public double ToneMix { get; set; } = 0.1;             // 0-1 (0=noise, 1=tone)
    public double ToneDurationMs { get; set; } = 500;      // 100-1000 ms

    // Column order preferences
    public List<string>? ColumnOrder { get; set; }

    // Column visibility preferences (columns that are hidden)
    public List<string>? HiddenColumns { get; set; }

    public string GetDataStoragePath()
    {
        if (!string.IsNullOrEmpty(DataStoragePath) && Directory.Exists(DataStoragePath))
            return DataStoragePath;

        // Default to Documents/SimLogger
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documentsPath, "SimLogger");
    }

    public string? GetGSProDatabasePath()
    {
        // If custom path is set, use it
        if (!string.IsNullOrEmpty(GSProPath))
        {
            var customDbPath = Path.Combine(GSProPath, "GSPro.db");
            if (File.Exists(customDbPath))
                return customDbPath;
        }

        // Default to AppData LocalLow location
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low";
        var defaultPath = Path.Combine(appDataPath, "GSPro", "GSPro", "GSPro.db");
        if (File.Exists(defaultPath))
            return defaultPath;

        return null;
    }

    public static string GetDefaultGSProPath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low";
        return Path.Combine(appDataPath, "GSPro", "GSPro");
    }

    public AudioTriggerService()
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var simLoggerPath = Path.Combine(documentsPath, "SimLogger");
        Directory.CreateDirectory(simLoggerPath);
        _settingsPath = Path.Combine(simLoggerPath, "settings.json");

        LoadSettings();
    }

    public List<AudioDeviceInfo> GetAudioOutputDevices()
    {
        var devices = new List<AudioDeviceInfo>();

        for (int i = 0; i < WaveOut.DeviceCount; i++)
        {
            var capabilities = WaveOut.GetCapabilities(i);
            devices.Add(new AudioDeviceInfo
            {
                Index = i,
                Name = capabilities.ProductName
            });
        }

        return devices;
    }

    public void PlayTriggerTone()
    {
        if (!IsEnabled || SelectedDeviceIndex < 0)
            return;

        PlayToneOnDevice(SelectedDeviceIndex);
    }

    public void TestTone(int deviceIndex)
    {
        PlayToneOnDevice(deviceIndex, useTestWav: false);
    }

    private void PlayToneOnDevice(int deviceIndex, bool useTestWav = false)
    {
        try
        {
            // Stop any existing playback
            StopPlayback();

            _waveOut = new WaveOutEvent
            {
                DeviceNumber = deviceIndex
            };

            // Use the actual WAV file for testing, generated sound for triggers
            if (useTestWav)
            {
                var testWavPath = @"C:\Users\jonth\OneDrive\Documents\Audacity\hit.wav";
                if (File.Exists(testWavPath))
                {
                    var audioFile = new AudioFileReader(testWavPath);
                    _waveOut.Init(audioFile);
                    _waveOut.PlaybackStopped += (s, e) =>
                    {
                        audioFile.Dispose();
                        var wo = s as WaveOutEvent;
                        wo?.Dispose();
                        if (_waveOut == wo)
                            _waveOut = null;
                    };
                }
                else
                {
                    // Fallback to generated sound if WAV not found
                    var impactSound = new GolfImpactSampleProvider(
                        ToneFrequencyHz,
                        ToneNoiseDecay,
                        ToneToneDecay,
                        ToneMix,
                        ToneDurationMs);
                    _waveOut.Init(impactSound);
                    _waveOut.PlaybackStopped += (s, e) =>
                    {
                        var wo = s as WaveOutEvent;
                        wo?.Dispose();
                        if (_waveOut == wo)
                            _waveOut = null;
                    };
                }
            }
            else
            {
                var impactSound = new GolfImpactSampleProvider(
                    ToneFrequencyHz,
                    ToneNoiseDecay,
                    ToneToneDecay,
                    ToneMix,
                    ToneDurationMs);
                _waveOut.Init(impactSound);
                _waveOut.PlaybackStopped += (s, e) =>
                {
                    var wo = s as WaveOutEvent;
                    wo?.Dispose();
                    if (_waveOut == wo)
                        _waveOut = null;
                };
            }

            _waveOut.Play();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error playing audio trigger: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates a realistic golf club impact sound.
    /// Based on separate decay rates for noise (thwack) and tone (ping) components.
    /// </summary>
    private class GolfImpactSampleProvider : ISampleProvider
    {
        private readonly WaveFormat _waveFormat;
        private readonly Random _random;
        private int _sampleIndex;
        private readonly int _totalSamples;
        private readonly int _sampleRate;

        // Configurable parameters
        private readonly double _frequencyHz;
        private readonly double _noiseDecay;
        private readonly double _toneDecay;
        private readonly double _toneMix;

        public WaveFormat WaveFormat => _waveFormat;

        public GolfImpactSampleProvider(
            double frequencyHz = 3200,
            double noiseDecay = 60,
            double toneDecay = 8,
            double toneMix = 0.7,
            double durationMs = 500)
        {
            _sampleRate = 44100;
            _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(_sampleRate, 1);
            _random = new Random();

            _frequencyHz = frequencyHz;
            _noiseDecay = noiseDecay;
            _toneDecay = toneDecay;
            _toneMix = toneMix;

            _totalSamples = (int)(_sampleRate * durationMs / 1000.0);
            _sampleIndex = 0;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesWritten = 0;

            for (int i = 0; i < count; i++)
            {
                if (_sampleIndex >= _totalSamples)
                {
                    buffer[offset + i] = 0;
                    samplesWritten++;
                    continue;
                }

                double timeSec = _sampleIndex / (double)_sampleRate;

                // Generate the sound
                double sample = GenerateSound(timeSec);

                // Clip to valid range
                sample = Math.Max(-1.0, Math.Min(1.0, sample));

                buffer[offset + i] = (float)sample;
                _sampleIndex++;
                samplesWritten++;
            }

            // Return 0 when we've played all samples to signal end
            if (_sampleIndex >= _totalSamples && samplesWritten == count)
            {
                bool allZeros = true;
                for (int i = 0; i < count && allZeros; i++)
                {
                    if (buffer[offset + i] != 0) allZeros = false;
                }
                if (allZeros) return 0;
            }

            return samplesWritten;
        }

        private double GenerateSound(double timeSec)
        {
            // Tone component (the "ping" of the metal)
            double tone = Math.Sin(2.0 * Math.PI * _frequencyHz * timeSec);
            double toneEnvelope = Math.Exp(-_toneDecay * timeSec);
            double toneComponent = tone * toneEnvelope;

            // Noise component (the "thwack" of compression)
            double noise = _random.NextDouble() * 2.0 - 1.0;
            double noiseEnvelope = Math.Exp(-_noiseDecay * timeSec);
            double noiseComponent = noise * noiseEnvelope;

            // Mix based on toneMix (0 = all noise, 1 = all tone)
            return (toneComponent * _toneMix) + (noiseComponent * (1.0 - _toneMix));
        }
    }

    private void StopPlayback()
    {
        if (_waveOut != null)
        {
            try
            {
                _waveOut.Stop();
                _waveOut.Dispose();
            }
            catch { }
            _waveOut = null;
        }
    }

    public void SaveSettings()
    {
        try
        {
            var settings = new AudioTriggerSettings
            {
                AudioTriggerEnabled = IsEnabled,
                SelectedDeviceIndex = SelectedDeviceIndex,
                SelectedDeviceName = SelectedDeviceName,
                DataStoragePath = DataStoragePath,
                GSProPath = GSProPath,
                NetworkTriggerEnabled = NetworkTriggerEnabled,
                NetworkTriggerPort = NetworkTriggerPort,
                NetworkTriggerHost = NetworkTriggerHost,
                ToneFrequencyHz = ToneFrequencyHz,
                ToneNoiseDecay = ToneNoiseDecay,
                ToneToneDecay = ToneToneDecay,
                ToneMix = ToneMix,
                ToneDurationMs = ToneDurationMs,
                ColumnOrder = ColumnOrder,
                HiddenColumns = HiddenColumns
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
        }
    }

    public void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<AudioTriggerSettings>(json);

                if (settings != null)
                {
                    IsEnabled = settings.AudioTriggerEnabled;
                    SelectedDeviceIndex = settings.SelectedDeviceIndex;
                    SelectedDeviceName = settings.SelectedDeviceName;
                    DataStoragePath = settings.DataStoragePath;
                    GSProPath = settings.GSProPath;
                    NetworkTriggerEnabled = settings.NetworkTriggerEnabled;
                    NetworkTriggerPort = settings.NetworkTriggerPort;
                    NetworkTriggerHost = settings.NetworkTriggerHost ?? "127.0.0.1";

                    // Load tone parameters (with defaults for backward compatibility)
                    ToneFrequencyHz = settings.ToneFrequencyHz > 0 ? settings.ToneFrequencyHz : 5800;
                    ToneNoiseDecay = settings.ToneNoiseDecay > 0 ? settings.ToneNoiseDecay : 60;
                    ToneToneDecay = settings.ToneToneDecay > 0 ? settings.ToneToneDecay : 200;
                    ToneMix = settings.ToneMix > 0 ? settings.ToneMix : 0.1;
                    ToneDurationMs = settings.ToneDurationMs > 0 ? settings.ToneDurationMs : 500;

                    // Load column order preferences
                    ColumnOrder = settings.ColumnOrder;

                    // Load column visibility preferences
                    HiddenColumns = settings.HiddenColumns;

                    // Validate device still exists
                    if (SelectedDeviceIndex >= 0 && SelectedDeviceIndex >= WaveOut.DeviceCount)
                    {
                        SelectedDeviceIndex = -1;
                        SelectedDeviceName = null;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        StopPlayback();
    }
}

public class AudioDeviceInfo
{
    public int Index { get; set; }
    public required string Name { get; set; }
}

public class AudioTriggerSettings
{
    public bool AudioTriggerEnabled { get; set; }
    public int SelectedDeviceIndex { get; set; } = -1;
    public string? SelectedDeviceName { get; set; }
    public string? DataStoragePath { get; set; }
    public string? GSProPath { get; set; }

    // Network trigger settings
    public bool NetworkTriggerEnabled { get; set; }
    public int NetworkTriggerPort { get; set; } = 8875;
    public string? NetworkTriggerHost { get; set; } = "127.0.0.1";

    // Tone envelope parameters (golf impact sound)
    public double ToneFrequencyHz { get; set; } = 5800;    // 500-8000 Hz
    public double ToneNoiseDecay { get; set; } = 60;       // 10-100 (higher = shorter)
    public double ToneToneDecay { get; set; } = 200;       // 5-500 (higher = shorter)
    public double ToneMix { get; set; } = 0.1;             // 0-1 (0=noise, 1=tone)
    public double ToneDurationMs { get; set; } = 500;      // 100-1000 ms

    // Column order preferences (list of column headers in display order)
    public List<string>? ColumnOrder { get; set; }

    // Column visibility preferences (columns that are hidden)
    public List<string>? HiddenColumns { get; set; }
}
