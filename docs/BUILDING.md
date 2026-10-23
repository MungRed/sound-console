# Building Sound Console

## Prerequisites

- Windows 10 or Windows 11 x64.
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (the project uses `net10.0-windows`).
- PowerShell 5.1 or PowerShell 7+ for the packaging script.
- Visual Studio 2022/2026 or JetBrains Rider are optional; the `dotnet` CLI is sufficient.

The application targets Windows desktop only. The audio core cannot run on Linux/macOS because it uses `NAudio.Wasapi`.

## Clone and build

```powershell
git clone https://github.com/MungRed/sound-console.git
cd sound-console

dotnet restore
dotnet build SoundConsole.sln -c Release
```

The debug build can be started with:

```powershell
dotnet run --project src/SoundConsole/SoundConsole.csproj
```

## Tests

```powershell
dotnet test SoundConsole.sln -c Release
```

The test suite is hardware-independent. It must pass on a machine without an Ampero or any other audio interface. CI runs the same command on `windows-latest`.

## Publish a self-contained single file

```powershell
./build.ps1
```

`build.ps1` performs a Release build and a self-contained `win-x64` publish with:

- `PublishSingleFile=true`
- `IncludeNativeLibrariesForSelfExtract=true`
- `EnableCompressionInSingleFile=true`
- no PDB files

Output:

```text
dist/
 声卡控制台-win-x64/
   声卡控制台.exe          (single-file bundle, ~64 MB)
   README.md
   README.zh-CN.md / 使用说明.md
   CHANGELOG.md
   LICENSE
   THIRD-PARTY-NOTICES.md
   docs/                   (architecture, build, troubleshooting, preview)
```

The bundle is self-contained; users do not need a .NET runtime. Because the build is unsigned, Windows SmartScreen may show an “unknown publisher” warning on first launch.

You can also publish manually:

```powershell
dotnet publish src/SoundConsole/SoundConsole.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true
```

## Repository conventions

- The solution contains `src/SoundConsole` (WPF UI), `src/SoundConsole.Core` (audio core), and `tests/SoundConsole.Core.Tests`.
- Package versions are centralized in `Directory.Packages.props`; common MSBuild properties are in `Directory.Build.props`.
- Build output (`bin`, `obj`, `dist`, `artifacts`) is ignored. Release binaries are attached to GitHub Releases rather than committed.
- Do not commit generated logs, personal settings, or audio files.
- Keep audio-thread code allocation-light and free of UI calls.

## Continuous integration

`.github/workflows/ci.yml` runs on every push to `main` and every pull request:

1. Restore.
2. Build in Release.
3. Run all tests.
4. Upload TRX test results.

`.github/workflows/release.yml` runs on tags matching `v*`:

1. Build and publish the self-contained bundle.
2. Zip the distribution folder.
3. Create a SHA-256 checksum.
4. Create/update the GitHub Release and attach the zip and checksum.

## Versioning

The version is stored in `Directory.Build.props` as `VersionPrefix`. For a release:

1. Update `VersionPrefix` and `CHANGELOG.md`.
2. Commit the changes.
3. Create and push an annotated tag, for example `git tag -a v1.0.0 -m "v1.0.0"` and `git push origin v1.0.0`.
4. Confirm the release workflow succeeded and the zip is attached.

## Code signing

The project ships unsigned open-source binaries. If you maintain a fork and distribute broadly, consider signing with a code-signing certificate and documenting the fingerprint in your release notes.

## Troubleshooting the build

- **NETSDK1100 / Windows targeting** — build on Windows, or use a Windows runner in CI.
- **Package restore blocked** — ensure NuGet can reach `https://api.nuget.org/v3/index.json`.
- **PowerShell parse errors in `build.ps1`** — keep the file UTF-8 with BOM; Windows PowerShell 5.1 needs it for the Chinese output path.
- **Publish is not a single file** — check that `PublishSingleFile` and `--self-contained true` are both present; the script aborts if DLLs remain beside the executable.
