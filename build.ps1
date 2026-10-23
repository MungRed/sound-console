param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$distRoot = Join-Path $root "dist"
$publishDir = Join-Path $distRoot "声卡控制台-win-x64"

Write-Host "==> Restoring and building" -ForegroundColor Cyan
dotnet build (Join-Path $root "SoundConsole.sln") -c $Configuration

Write-Host "==> Publishing self-contained single file" -ForegroundColor Cyan
if (Test-Path $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

dotnet publish (Join-Path $root "src\SoundConsole\SoundConsole.csproj") `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $publishDir

$publishedExe = Join-Path $publishDir "SoundConsole.exe"
if (-not (Test-Path $publishedExe)) {
    throw "发布失败：没有找到 SoundConsole.exe"
}

$publishedDlls = Get-ChildItem -LiteralPath $publishDir -Filter "*.dll" -File
if ($publishedDlls) {
    throw "发布结果不是单文件，请检查 PublishSingleFile 设置。"
}

$friendlyExe = Join-Path $publishDir "声卡控制台.exe"
Move-Item -LiteralPath $publishedExe -Destination $friendlyExe -Force

# Keep only the single-file bundle; drop optional symbols or auxiliary files.
Get-ChildItem -LiteralPath $publishDir -File |
    Where-Object { $_.Name -ne "声卡控制台.exe" } |
    Remove-Item -Force

Copy-Item (Join-Path $root "README.md") (Join-Path $publishDir "README.md") -Force
Copy-Item (Join-Path $root "README.zh-CN.md") (Join-Path $publishDir "README.zh-CN.md") -Force
Copy-Item (Join-Path $root "README.zh-CN.md") (Join-Path $publishDir "使用说明.md") -Force
Copy-Item (Join-Path $root "LICENSE") (Join-Path $publishDir "LICENSE") -Force
Copy-Item (Join-Path $root "THIRD-PARTY-NOTICES.md") (Join-Path $publishDir "THIRD-PARTY-NOTICES.md") -Force
Copy-Item (Join-Path $root "CHANGELOG.md") (Join-Path $publishDir "CHANGELOG.md") -Force
New-Item -ItemType Directory -Force -Path (Join-Path $publishDir "docs") | Out-Null
Copy-Item (Join-Path $root "docs\*") (Join-Path $publishDir "docs") -Force

$exeInfo = Get-Item $friendlyExe
Write-Host ""
Write-Host "发布完成：" -ForegroundColor Green
Write-Host "  $($exeInfo.FullName)"
Write-Host "  大小: $([math]::Round($exeInfo.Length / 1MB, 1)) MB"
Write-Host "  中文说明: $(Join-Path $publishDir '使用说明.md')"
Write-Host "  English: $(Join-Path $publishDir 'README.md')"
