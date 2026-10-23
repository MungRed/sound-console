# Architecture

Sound Console is a Windows-only WPF application backed by a hardware-independent audio core. It captures one WASAPI input, keeps the stream in a shared ring buffer, and fans it out to one or more WASAPI render endpoints.

## High-level layout

```text
┌────────────────────────────────────────────────────────────┐
│ WPF application (src/SoundConsole)                         │
│  MainWindow · MainViewModel · OutputDeviceViewModel        │
│  LevelMeter · themes · settings persistence                │
└──────────────────────────────┬─────────────────────────────┘
                               │ RouteRequest / snapshots
┌──────────────────────────────▼─────────────────────────────┐
│ Audio core (src/SoundConsole.Core)                         │
│  AudioRouterEngine                                         │
│   ├─ AudioDeviceService  (MMDevice enumeration)            │
│   ├─ WasapiCapture       (selected input + channel pair)   │
│   ├─ SharedAudioBuffer   (stereo ring, lock protected)     │
│   └─ OutputSink[]                                           │
│       ├─ RouterSinkProvider (cubic interpolation, gain)    │
│       └─ WasapiOut (shared or exclusive)                   │
└────────────────────────────────────────────────────────────┘
```

The UI never touches NAudio or COM audio objects directly. It sends a `RouteRequest` to `AudioRouterEngine`, polls meter snapshots on a 50 ms dispatcher timer, and applies live gain/volume changes through the engine. This boundary is what makes the audio logic unit-testable without hardware.

## Audio pipeline

```text
capture packet
   │  PcmBlockReader: IEEE float / PCM 8/16/24/32 → stereo float
   │  selected channel pair; mono inputs are duplicated
   │  input gain
   ▼
SharedAudioBuffer (stereo interleaved, ~2 s capacity, power-of-two)
   │
   ├── Output 1: RouterSinkProvider → WasapiOut → speakers
   ├── Output 2: RouterSinkProvider → WasapiOut → USB DAC
   └── Output N: ...
```

### Capture

`WasapiCapture` is opened in shared mode with event-driven callbacks. It requests a small internal buffer based on the user's safety-buffer setting. The capture mix format (sample rate and channel count) is read at start and never changed during a route.

`PcmBlockReader` converts the incoming block into stereo float32. It supports IEEE float, PCM 8/16/24/32, mono duplication, and arbitrary channel pairs. Ampero II Stomp exposes eight capture channels; the UI exposes four pairs.

### Shared buffer

`SharedAudioBuffer` is a single-producer / multi-consumer ring:

- Capture writes interleaved stereo at the capture sample rate.
- Each output has its own read position; it does not consume data for other outputs.
- A short lock protects write/read consistency and the interpolation scratch buffers.
- Input gain and peak metering are applied on write.
- Non-finite samples are dropped and sample values are clamped to ±4 before entering the buffer.

### Per-output resampling and drift correction

`RouterSinkProvider` is an `ISampleProvider` at the output stream's sample rate. For every block it:

1. Compares the pending input frames with the requested safety target.
2. Derives a small correction factor (capped at ±0.4%) from the fill error.
3. Reads the ring with cubic interpolation at `captureRate / outputRate × correction`.
4. Applies a smooth output gain ramp (volume/mute/fade-out).
5. Reports post-gain peaks and feeds the WASAPI render client.

Capture and render hardware have independent clocks. Even a 20 ppm mismatch would accumulate about 70 ms per hour without correction; the adaptive ratio keeps the buffer near its target instead. A hard resync is reserved for pathological stalls, where skipping ahead is better than playing minutes behind.

### Output formats and fallback

- Shared output uses a 32-bit float stereo stream at the endpoint sample rate, normalized to 48/96 kHz for high-rate devices. Windows performs the final device conversion and mixes other applications.
- Exclusive output tries 44.1, 48, and 96 kHz stereo float, falling back to the next candidate. If none succeeds, the sink is opened in shared mode and the UI receives a note.
- Devices with more than two hardware channels (for example an 8-channel headset endpoint) are handled by opening a stereo stream and letting the Windows audio engine map it.

### Latency budget

`AudioLatencyPolicy` centralizes the software-side tradeoff:

| Safety buffer | Requested render buffer |
| --- | --- |
| 20 ms | 10 ms |
| 30 ms | 10 ms |
| 40 ms and above | 20 ms |

The UI estimates software-side latency as `capture period (≈10 ms) + safety buffer + render buffer`. It deliberately does not present this as a measured round trip: the instrument's A/D, DSP, USB transport, and the output device all add latency outside the application's control.

The 20 ms preset is intended for a wired output in exclusive mode. It is not enabled by default because small buffers are more sensitive to USB and scheduler jitter.

## Threading and lifetime

| Context | Responsibilities |
| --- | --- |
| WPF UI thread | Device enumeration, settings, view models, dispatcher timers |
| Capture thread (NAudio) | `OnCaptureData` → convert + write ring buffer |
| Render threads (one per sink, NAudio) | Read ring, interpolate, write WASAPI |
| Engine lifecycle | `StartAsync`/`StopAsync` serialized by a semaphore |

`AudioRouterEngine` exposes events (`StateChanged`, `Started`, `Faulted`) that are marshalled to the dispatcher by `MainViewModel`.

Startup sequence:

1. Validate the request and open the capture device.
2. Open every output sink and initialize WASAPI.
3. Start capture and wait until the ring has the target fill plus a small margin.
4. Start all render threads, wait briefly for fast failures, then enter `Running`.
5. Reset startup-only dropout counters.

Stop sequence:

1. Mark the shutdown as intentional and stop meter updates.
2. Stop capture and detach events.
3. Fade each sink, then stop and dispose its WASAPI client.
4. Dispose the capture device and clear active outputs.

A device invalidation or unexpected `PlaybackStopped` schedules a fault path. The engine stops every sink, returns to `Faulted`, and raises a message that the UI shows as an error toast.

## Error handling

- Common WASAPI HRESULTs (`0x88890004`, `0x88890008`, `0x8889000A`, etc.) are translated into actionable Chinese messages by `AudioError`.
- Start failures propagate as exceptions to the UI, which shows the message without leaving any half-open route.
- Settings writes are atomic (temp file + replace) and failures never affect audio.
- Logging is best-effort and runs in a lock-free path relative to audio callbacks.
- The application has a single-instance mutex and a WPF dispatcher exception handler.

## Tests

`tests/SoundConsole.Core.Tests` covers:

- PCM/float conversion and channel-pair selection.
- Ring-buffer wrap, overrun, oversized writes, gain, and clipping.
- Cubic resampling at integer and fractional ratios.
- Meter peak/clip semantics.
- Sink priming, output gain/mute, overflow resynchronization, and format metadata.
- Settings normalization and output-preference lookup.
- Software-latency policy for low-latency presets.
- Error translation.

The tests require no audio hardware and run in CI on `windows-latest`.

## Deliberate non-goals

- ASIO support (WASAPI shared/exclusive is sufficient here; ASIO needs a separate driver model).
- Bundling a virtual audio cable driver (requires a signed kernel driver and administrator installation).
- Recording, effects, EQ, or a full digital mixer.
- Sample-accurate synchronization between outputs at different clock rates.
- Remote control or network streaming.
