# Troubleshooting

Most routing problems show up in one of three places: the input meter, the selected output, or the Windows endpoint. Work through them in that order.

## 1. The input meter shows −∞

The PC is not receiving audio from the effect processor.

- Confirm the USB cable is connected to the port that supports audio (not a charge-only hub).
- Check the Ampero's USB Audio output routing and output level. Some presets or modes can mute USB audio.
- Close DAWs or ASIO applications that may hold the capture endpoint exclusively.
- Press `F5` to refresh devices.
- Try another channel pair. On an Ampero II Stomp, `1+2` is common, but firmware routing may put the processed signal on `3+4`.

The input meter is measured after input gain, so raising gain should make even quiet signals visible. If it stays at −∞, the issue is upstream of Sound Console.

## 2. Input meter moves but the output is silent

- Check the output device is ticked, not merely listed. The switch on the left must be green.
- Check the per-output volume and mute button. `静音` means the output is muted.
- Check the Windows volume mixer level for that endpoint; Sound Console's slider is independent of the Windows endpoint volume.
- Make sure the correct output is selected when several devices are ticked.
- If exclusive mode is enabled and another application owns the device, disable exclusive mode or close the other application.

## 3. No output devices are listed

- Windows may have the endpoint disabled. Open **Settings → System → Sound → More sound settings** and enable it.
- Bluetooth devices must be connected and active before pressing `F5`.
- Virtual cables must be installed before they appear.
- Sound Console enumerates only active WASAPI render endpoints. Disconnected or disabled endpoints are intentionally hidden.

## 4. Exclusive mode fails or falls back

Sound Console tries exclusive mode at 44.1, 48, and 96 kHz stereo float. If the endpoint rejects all of them, it falls back to shared mode and shows a note.

Common reasons:

- Another application already holds the endpoint.
- The device does not support exclusive mode for stereo float.
- The device is a virtual or communications endpoint with a fixed format.

Use shared mode if exclusive is not required. Shared mode also allows other applications to keep playing through the same output.

## 5. Clipping / CLIP badge

`CLIP` means a sample reached 0 dBFS on the selected input pair after gain. It latches briefly so short transients are visible.

- Lower **输入增益** first.
- Lower the output level on the effect processor if the gain is already at or below 0 dB.
- Avoid extreme boost on quiet pickups; use the effect processor's level control instead.

## 6. Crackles, dropouts, or “缓冲丢帧”

- Increase the safety buffer: 40 → 60 → 100 ms.
- Disable exclusive mode and compare.
- Connect the interface directly to the PC rather than through an unpowered hub.
- Close CPU-heavy applications or set the Windows power plan to a performance mode.
- Bluetooth and virtual devices are especially sensitive; use 100 ms or more.
- Check the log for repeated device-invalidated errors; a flaky USB connection can reset the endpoint.

A single dropout at startup is suppressed by the application. If the counter keeps increasing, the audio path is genuinely under-running.

## 7. Latency feels too high

- Use 30 ms instead of 40 ms, then try 60 ms if unstable.
- Use exclusive mode on a dedicated output.
- Prefer a wired USB DAC/headphone output over Bluetooth.
- A 44.1 kHz input and a native 44.1 kHz output avoid resampling in the device chain.
- Remember that USB buffering, the effect processor's internal processing, and the Windows audio engine all add latency. For zero-latency performance, monitor directly through the effect processor.

## 8. Sending audio to OBS / a DAW / Discord

A Windows playback device is not a microphone. Use a virtual audio cable:

1. Install VB-CABLE, VoiceMeeter, or a similar virtual audio device.
2. In Sound Console, enable the virtual cable's **playback** endpoint (for example `CABLE Input`).
3. In the receiving application, choose the cable's **recording** endpoint (for example `CABLE Output`).
4. Optionally also enable a real headphone output for local monitoring.

Do not enable the effect processor's own output as a Sound Console output unless you understand the routing: some configurations can create a feedback loop. The UI marks it with `回传风险` (loopback risk).

## 9. Where are the logs?

Click **日志** in the footer, or open:

```text
%LOCALAPPDATA%\SoundConsole\logs\
```

Settings are in:

```text
%LOCALAPPDATA%\SoundConsole\settings.json
```

The app does not record audio. Logs contain endpoint names, routing state, and exception details.

## 10. Reporting a bug

Open an issue with:

- Windows version and build (`winver`).
- Sound Console version (footer).
- Audio interface and driver/firmware version.
- Selected input channels, output devices, buffer, and shared/exclusive mode.
- Steps to reproduce and expected vs. actual behaviour.
- Relevant log lines, with any personal information removed.

For security-sensitive reports, follow [SECURITY.md](../SECURITY.md) instead of opening a public issue.
