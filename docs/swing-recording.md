# Swing Recording

Sim Logger can automatically trigger third-party swing recording software (such as [Kinovea](https://www.kinovea.org/) or [Swing Catalyst](https://swingcatalyst.com/)) the instant a shot is detected. Two trigger methods are available: **Audio** and **Network (UDP)**.

Both trigger types require **real-time shot detection** to be enabled, which monitors network traffic between your launch monitor software and GSPro using [Npcap](https://npcap.com/).

> **Note:** Triggers only fire in real-time monitoring mode. When using the file monitoring fallback, the shot list updates but triggers are disabled.

## Prerequisites

1. **Npcap** installed — download from [npcap.com](https://npcap.com/) (free for personal use)
   - During installation, select **"Install Npcap in WinPcap API-compatible Mode"**
2. **Run as Administrator** — Sim Logger requires admin privileges for packet capture
3. A supported swing recording application (Kinovea, Swing Catalyst, etc.)

## How It Works

Sim Logger captures network packets sent to the GSPro API port in real time. When a shot is detected in the traffic (validated by the presence of ball data with a speed > 0), the configured trigger fires immediately — before GSPro writes the shot to its database. This ensures your swing recording captures the moment of impact with minimal delay.

A 500ms debounce window prevents duplicate triggers from a single shot.

## Configuration

Click the **signal icon** in the toolbar to open the Shot Trigger configuration dialog. From here you can:

1. Choose between **Audio Trigger** or **Network Trigger (UDP)**
2. Configure trigger-specific settings (see below)
3. Set the **GSPro API Port** for real-time detection:
   - **12321** — ProTee VX / ProTee Labs connector (default)
   - **921** — OpenConnect API (other launch monitors)

After saving, enable the **Realtime** toggle in the settings bar. A restart is required for changes to take effect.

The status bar displays the current monitoring mode:
- **Real-time** — active packet monitoring on the configured port
- **File monitoring** — fallback mode (triggers disabled)

---

## Audio Trigger

The audio trigger plays a short impact-style tone on a selected audio output device when a shot is detected. This is designed to work with swing recording software that supports audio-based recording triggers.

### Typical Setup

1. Install a virtual audio cable such as [VB-Cable](https://vb-audio.com/Cable/)
2. In Sim Logger, select the virtual cable as the audio output device
3. In your recording software (e.g., Kinovea), configure the audio trigger to listen on the virtual cable input
4. When a shot is detected, Sim Logger plays a tone through the virtual cable, which triggers the recording

### Audio Settings

| Setting | Range | Default | Description |
|---------|-------|---------|-------------|
| **Output Device** | System audio devices | — | The audio device to play the trigger tone on |
| **Frequency** | 500–8000 Hz | 5800 Hz | Tone frequency of the generated sound |
| **Noise Decay** | 10–100 | 60 | How quickly the noise component fades (higher = shorter) |
| **Tone Decay** | 5–500 | 200 | How quickly the tone component fades (higher = shorter) |
| **Tone Mix** | 0–1 | 0.1 | Balance between noise and tone (0 = pure noise, 1 = pure tone) |
| **Duration** | 100–1000 ms | 500 ms | Total length of the trigger sound |

The default values produce a realistic golf club impact sound with a sharp attack and fast decay. The advanced tone settings are available in an expandable section of the configuration dialog.

Use the **Test** button to preview the sound on the selected device before saving.

---

## Network Trigger (UDP)

The network trigger sends a UDP packet to a configurable host and port when a shot is detected. This is designed for recording software that supports network-based triggers, such as Kinovea 2025.1.1+.

### Typical Setup

1. In Sim Logger, set the target host and port for the UDP trigger
2. In Kinovea (2025.1.1 or later), enable the UDP trigger listener on the matching port
3. When a shot is detected, Sim Logger sends a UDP packet containing a timestamped message, which triggers the recording

### Network Settings

| Setting | Default | Description |
|---------|---------|-------------|
| **Host** | `127.0.0.1` | Target IP address to send the UDP packet to |
| **Port** | `8875` | Target UDP port (1–65535) |

The UDP message format is: `SIMLOGGER_TRIGGER:<ISO 8601 timestamp>`

For local setups where the recording software runs on the same machine, the default host (`127.0.0.1`) works without any network configuration. For remote setups, enter the IP address of the machine running the recording software.

Use the **Test** button to send a test packet and verify the connection before saving.

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Triggers not firing | Ensure **Realtime** toggle is enabled and status bar shows "Real-time" |
| "Npcap not found" error | Install Npcap from [npcap.com](https://npcap.com/) with WinPcap compatibility mode |
| Real-time mode not starting | Run Sim Logger as Administrator (right-click → Run as administrator) |
| Audio trigger not heard | Verify the correct output device is selected and test the tone from the config dialog |
| Recording software not responding to audio | Check that the virtual audio cable is configured as both Sim Logger's output and the recording software's input |
| Network trigger not received | Confirm host/port match between Sim Logger and recording software; check firewall settings |
| Double triggers | The 500ms debounce should prevent this; if it persists, check for duplicate monitoring instances |
| Wrong shots detected | Verify the GSPro API port matches your setup (12321 for ProTee VX, 921 for OpenConnect) |
