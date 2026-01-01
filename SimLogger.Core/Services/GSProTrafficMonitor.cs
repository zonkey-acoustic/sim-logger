using System.Text;
using System.Text.Json;
using SharpPcap;
using PacketDotNet;

namespace SimLogger.Core.Services;

/// <summary>
/// Monitors network traffic to GSPro's OpenConnect API port to detect shots in real-time.
/// This allows triggering audio/network notifications the moment shot data is sent,
/// before GSPro writes to its database.
///
/// Requires Npcap to be installed: https://npcap.com/
/// </summary>
public class GSProTrafficMonitor : IDisposable
{
    private ILiveDevice? _device;
    private CancellationTokenSource? _cts;
    private Task? _captureTask;
    private bool _disposed;
    private readonly object _lockObject = new();

    /// <summary>
    /// The port GSPro listens on for OpenConnect API connections.
    /// </summary>
    public int GSProPort { get; set; } = 12321;

    /// <summary>
    /// Whether the monitor is currently running.
    /// </summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Fired immediately when shot data is detected being sent to GSPro.
    /// </summary>
    public event EventHandler<ShotTrafficDetectedEventArgs>? ShotDetected;

    /// <summary>
    /// Fired when an error occurs.
    /// </summary>
    public event EventHandler<TrafficMonitorErrorEventArgs>? Error;

    /// <summary>
    /// Fired when the monitor status changes.
    /// </summary>
    public event EventHandler<string>? StatusChanged;

    /// <summary>
    /// Checks if Npcap is installed and available.
    /// </summary>
    public static bool IsNpcapInstalled()
    {
        try
        {
            var devices = CaptureDeviceList.Instance;
            return devices.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the list of available network devices.
    /// </summary>
    public static List<string> GetAvailableDevices()
    {
        var result = new List<string>();
        try
        {
            var devices = CaptureDeviceList.Instance;
            foreach (var device in devices)
            {
                result.Add($"{device.Name}: {device.Description}");
            }
        }
        catch
        {
            // Npcap not installed
        }
        return result;
    }

    public void Start()
    {
        lock (_lockObject)
        {
            if (IsRunning || _disposed)
                return;

            try
            {
                var devices = CaptureDeviceList.Instance;
                Console.WriteLine($"[TrafficMonitor] Found {devices.Count} capture devices:");
                foreach (var d in devices)
                {
                    Console.WriteLine($"  - {d.Name}: {d.Description}");
                }

                if (devices.Count == 0)
                {
                    throw new InvalidOperationException("No capture devices found. Please install Npcap from https://npcap.com/");
                }

                // Find the loopback adapter (for localhost traffic) or use the first available device
                _device = FindLoopbackDevice(devices) ?? devices[0];
                Console.WriteLine($"[TrafficMonitor] Selected device: {_device.Name} - {_device.Description}");

                // Open device for capture
                _device.Open(new DeviceConfiguration
                {
                    Mode = DeviceModes.Promiscuous,
                    ReadTimeout = 1000
                });
                Console.WriteLine($"[TrafficMonitor] Device opened successfully");

                // Set filter for TCP traffic TO GSPro port only (ignore responses)
                _device.Filter = $"tcp dst port {GSProPort}";
                Console.WriteLine($"[TrafficMonitor] Filter set: tcp dst port {GSProPort}");

                _cts = new CancellationTokenSource();
                IsRunning = true;

                _captureTask = Task.Run(() => CaptureLoop(_cts.Token));

                Console.WriteLine($"[TrafficMonitor] Started monitoring on port {GSProPort}");
                StatusChanged?.Invoke(this, $"Monitoring traffic on port {GSProPort}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TrafficMonitor] ERROR: {ex.Message}");
                IsRunning = false;
                Error?.Invoke(this, new TrafficMonitorErrorEventArgs
                {
                    Message = $"Failed to start traffic monitor: {ex.Message}",
                    Exception = ex
                });
                throw;
            }
        }
    }

    public void Stop()
    {
        lock (_lockObject)
        {
            if (!IsRunning)
                return;

            _cts?.Cancel();

            try
            {
                _device?.StopCapture();
                _device?.Close();
            }
            catch
            {
                // Ignore errors during shutdown
            }

            _device = null;
            IsRunning = false;

            StatusChanged?.Invoke(this, "Traffic monitor stopped");
            System.Diagnostics.Debug.WriteLine("GSProTrafficMonitor: Stopped");
        }
    }

    private ILiveDevice? FindLoopbackDevice(CaptureDeviceList devices)
    {
        // Look for Npcap loopback adapter
        foreach (var device in devices)
        {
            var desc = device.Description?.ToLowerInvariant() ?? "";
            var name = device.Name?.ToLowerInvariant() ?? "";

            if (desc.Contains("loopback") || desc.Contains("npcap") ||
                name.Contains("loopback") || name.Contains("npf_loopback"))
            {
                return device;
            }
        }

        // Look for any adapter that might handle localhost
        foreach (var device in devices)
        {
            var desc = device.Description?.ToLowerInvariant() ?? "";
            if (desc.Contains("adapter") || desc.Contains("ethernet") || desc.Contains("wi-fi"))
            {
                return device;
            }
        }

        return null;
    }

    private void CaptureLoop(CancellationToken cancellationToken)
    {
        var dataBuffer = new StringBuilder();
        int packetCount = 0;
        int rawPacketCount = 0;

        Console.WriteLine("[TrafficMonitor] Capture loop started, waiting for packets...");

        while (!cancellationToken.IsCancellationRequested && _device != null)
        {
            try
            {
                var result = _device.GetNextPacket(out var packetCapture);

                if (result != GetPacketStatus.PacketRead)
                    continue;

                rawPacketCount++;
                if (rawPacketCount <= 5 || rawPacketCount % 50 == 0)
                {
                    Console.WriteLine($"[TrafficMonitor] Raw packet #{rawPacketCount} received");
                }

                var rawPacket = packetCapture.GetPacket();
                if (rawPacket == null)
                    continue;

                var packet = Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);
                var tcpPacket = packet.Extract<TcpPacket>();

                if (tcpPacket == null)
                    continue;

                packetCount++;

                // Log every 100th packet to show we're capturing
                if (packetCount % 100 == 0)
                {
                    Console.WriteLine($"[TrafficMonitor] Captured {packetCount} packets so far...");
                }

                // Log TCP packets to GSPro port
                if (tcpPacket.DestinationPort == GSProPort)
                {
                    var payloadLen = tcpPacket.PayloadData?.Length ?? 0;
                    Console.WriteLine($"[TrafficMonitor] Packet #{packetCount} TO GSPro - Payload: {payloadLen} bytes");
                }

                // We're interested in packets going TO GSPro (destination port = GSProPort)
                // This is the launch monitor sending shot data
                if (tcpPacket.DestinationPort == GSProPort && tcpPacket.PayloadData?.Length > 0)
                {
                    var payload = Encoding.UTF8.GetString(tcpPacket.PayloadData);
                    Console.WriteLine($"[TrafficMonitor] Payload preview: {payload.Substring(0, Math.Min(200, payload.Length))}...");

                    // Check if this looks like shot data
                    if (ContainsShotData(payload))
                    {
                        Console.WriteLine($"[TrafficMonitor] *** SHOT DATA DETECTED! *** Length={payload.Length}");

                        ShotDetected?.Invoke(this, new ShotTrafficDetectedEventArgs
                        {
                            RawPayload = payload,
                            Timestamp = DateTime.Now,
                            SourcePort = tcpPacket.SourcePort,
                            PayloadLength = tcpPacket.PayloadData.Length
                        });
                    }
                    else
                    {
                        Console.WriteLine($"[TrafficMonitor] Payload does not match shot data pattern");
                    }
                }
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine($"[TrafficMonitor] Capture error: {ex.Message}");
                }
            }
        }

        Console.WriteLine("[TrafficMonitor] Capture loop ended");
    }

    private bool ContainsShotData(string payload)
    {
        // GSPro OpenConnect protocol sends JSON with specific structure
        // Shot data has "BallData" with actual ball flight values
        if (string.IsNullOrEmpty(payload))
            return false;

        // Must contain BallData section - this is the definitive shot indicator
        if (!payload.Contains("\"BallData\""))
            return false;

        // BallData must have actual values (Speed > 0 indicates real shot)
        // Exclude heartbeats, status updates, and other non-shot messages
        if (!payload.Contains("\"Speed\""))
            return false;

        // Verify it's valid JSON and has meaningful ball data
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            // Check for BallData with Speed > 0
            if (root.TryGetProperty("BallData", out var ballData))
            {
                if (ballData.TryGetProperty("Speed", out var speed))
                {
                    // Speed > 0 means actual shot, not just a status message
                    if (speed.ValueKind == JsonValueKind.Number && speed.GetDouble() > 0)
                    {
                        Console.WriteLine($"[TrafficMonitor] Shot detected: BallSpeed={speed.GetDouble():F1}");
                        return true;
                    }
                }
            }
        }
        catch
        {
            // JSON parse failed - not a valid shot packet
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _cts?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

public class ShotTrafficDetectedEventArgs : EventArgs
{
    public required string RawPayload { get; init; }
    public required DateTime Timestamp { get; init; }
    public int SourcePort { get; init; }
    public int PayloadLength { get; init; }
}

public class TrafficMonitorErrorEventArgs : EventArgs
{
    public required string Message { get; init; }
    public Exception? Exception { get; init; }
}
