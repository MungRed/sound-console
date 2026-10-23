# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

Nothing yet.

## [1.1.0] - 2026-10-23

### Added

- **20 ms low-latency safety-buffer preset** for wired, dedicated outputs.
- Dynamic render buffering: 20/30 ms safety buffers now request a 10 ms WASAPI
  render buffer instead of 20 ms.
- A latency breakdown in the buffer tooltip, showing application, render, and
  capture contributions without claiming a hardware round-trip measurement.

### Changed

- Updated NAudio to 2.4.0, Microsoft.NET.Test.Sdk to 18.11.0, and
  xunit.runner.visualstudio to 4.0.1 after build, test, and live-route
  validation.
- Added unit coverage for the new latency policy.

## [1.0.0] - 2026-10-23

### Added

- Initial Windows/WASAPI audio routing console for the Ampero II Stomp and other
  USB audio interfaces.
- Automatic device discovery, Ampero-aware defaults, and persisted settings.
- Multi-channel input selection and up to eight simultaneous outputs with
  independent volume, mute, and peak meters.
- Input gain, clip indication, and selectable safety buffers.
- Shared and exclusive WASAPI output with automatic shared-mode fallback.
- Clock-drift-tolerant ring buffer with per-output cubic resampling.
- Hot-plug detection, single-instance guard, error toasts, and local logs.
- Self-contained single-file Windows x64 build.
- Hardware-free xUnit coverage, CI/release automation, bilingual documentation,
  and open-source project scaffolding.

### Fixed

- Oversized capture blocks now advance the ring-buffer write position by the
  original frame count, preserving correct absolute sample alignment.
- Non-finite samples no longer trigger the clipping indicator.

[Unreleased]: https://github.com/MungRed/sound-console/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/MungRed/sound-console/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/MungRed/sound-console/releases/tag/v1.0.0
