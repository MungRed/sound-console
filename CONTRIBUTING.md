# Contributing to Sound Console

Thanks for taking the time to improve Sound Console. Bug reports, hardware compatibility results, documentation fixes, and translations are all valuable.

## Before you start

- Read the [Code of Conduct](CODE_OF_CONDUCT.md).
- Search existing issues before opening a new one.
- For security-sensitive reports, follow [SECURITY.md](SECURITY.md) instead of filing a public issue.
- For a large feature, open an issue first so the approach can be discussed.

## Development setup

Requirements:

- Windows 10/11 x64.
- .NET 10 SDK.
- PowerShell 5.1+ (for `build.ps1`).

```powershell
git clone https://github.com/MungRed/sound-console.git
cd sound-console
dotnet restore
dotnet build SoundConsole.sln -c Release
dotnet test SoundConsole.sln -c Release
```

Run the app from source:

```powershell
dotnet run --project src/SoundConsole/SoundConsole.csproj
```

The test suite does not need an audio interface. Testing a real capture/render path requires Windows audio hardware and should be described in the pull request, not automated in CI.

## Repository rules

- Do not commit `bin/`, `obj/`, `dist/`, logs, settings, or recorded audio.
- Keep audio-thread code free of UI calls, blocking operations, and unnecessary allocations.
- Add or update unit tests for core logic changes. Tests live in `tests/SoundConsole.Core.Tests`.
- Keep the UI Chinese-first; if you add user-facing strings, place them consistently with existing Chinese labels. English localization is a separate, welcome contribution.
- Do not add telemetry, analytics, network calls, bundled audio content, or new runtime dependencies without discussion.
- Update `README.md`, `README.zh-CN.md`, and the relevant file in `docs/` when user-facing behaviour changes.
- Add an entry under `Unreleased` in `CHANGELOG.md` for notable changes.

## Pull request process

1. Fork the repository and create a topic branch from `main`.
2. Keep commits focused. Conventional Commit prefixes are appreciated (`feat:`, `fix:`, `docs:`, `test:`, `chore:`).
3. Run:
   ```powershell
   dotnet build SoundConsole.sln -c Release
   dotnet test SoundConsole.sln -c Release
   ```
4. If your change affects packaging or the UI, add a short description of the manual verification (device, mode, buffer, result).
5. Fill in the pull request template. Link the related issue if applicable.
6. Be prepared to adjust the change after review. Maintainers may ask for a narrower scope or additional tests.

CI must pass before merge. Releases are created from version tags by the maintainer.

## Reporting hardware issues

Please include:

- Windows version and build.
- Sound Console version from the footer.
- Interface model, connection path (direct USB/hub), driver/firmware version.
- Input channel pair, output devices, buffer size, and shared/exclusive mode.
- Whether the input meter moved and whether the output meter moved.
- Relevant logs from `%LOCALAPPDATA%\SoundConsole\logs\` with personal information removed.

## Localization

The WPF UI currently uses Chinese strings embedded in XAML and view models. A future localization change should move user-visible strings into resource dictionaries, preserve the current Chinese experience, and add a language selector. Please discuss the approach in an issue before implementing a large localization refactor.

## License

By contributing, you agree that your contributions are licensed under the project's [MIT License](LICENSE) (inbound = outbound).
