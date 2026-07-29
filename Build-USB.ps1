$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path $projectRoot "PhotoBooth\PhotoBooth.csproj"
$desktop = [Environment]::GetFolderPath("Desktop")
$outputRoot = Join-Path $desktop "PhotoBooth-USB"
$publishRoot = Join-Path $outputRoot "PhotoBooth"

Write-Host ""
Write-Host "PhotoBooth USB builder" -ForegroundColor Cyan
Write-Host "Project: $projectRoot"
Write-Host "Output:  $outputRoot"
Write-Host ""

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK is not installed. Install .NET 8 SDK or newer, then run BUILD_USB.bat again."
}

$sdkVersion = & dotnet --version
Write-Host ".NET SDK: $sdkVersion"

$sdkMajorVersion = [int]($sdkVersion.Split(".")[0])
if ($sdkMajorVersion -lt 8) {
    throw ".NET SDK 8 or newer is required. Installed version: $sdkVersion."
}

if (Test-Path $outputRoot) {
    Remove-Item $outputRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null

Write-Host ""
Write-Host "Publishing portable Windows x64 build..." -ForegroundColor Cyan
& dotnet publish $projectFile `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $publishRoot

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

New-Item -ItemType Directory -Path (Join-Path $publishRoot "Output") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $publishRoot "DemoPhotos") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $publishRoot "Templates") -Force | Out-Null

Copy-Item (Join-Path $projectRoot "USB_README.txt") (Join-Path $outputRoot "README.txt")

$launcher = @"
@echo off
cd /d "%~dp0PhotoBooth"
start "" "PhotoBooth.exe"
"@

Set-Content `
    -Path (Join-Path $outputRoot "START_PHOTOBOOTH.bat") `
    -Value $launcher `
    -Encoding Ascii

Write-Host ""
Write-Host "READY: $outputRoot" -ForegroundColor Green
Write-Host "Copy the entire PhotoBooth-USB folder to the USB drive."
Write-Host ""
Start-Process explorer.exe -ArgumentList "`"$outputRoot`""
