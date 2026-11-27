#Requires -Version 5.1
$ErrorActionPreference = "Stop"

$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ProjectRoot

# Resolve dotnet executable (system PATH or local user .dotnet SDK)
$DotnetExe = "dotnet"
if (Test-Path "$env:USERPROFILE\.dotnet\dotnet.exe") {
    $DotnetExe = "$env:USERPROFILE\.dotnet\dotnet.exe"
}

$ReleaseFolder = Join-Path $ProjectRoot "release\SubspaceProtocols_win-x64"
if (Test-Path $ReleaseFolder) {
    Remove-Item $ReleaseFolder -Recurse -Force
}

# Ensure crisp lossless 64x64 PNG companion exists for assets/images/app_icon.jpg (Raylib rtextures uses PNG natively)
$AppIconJpg = Join-Path $ProjectRoot "assets\images\app_icon.jpg"
$AppIconPng = Join-Path $ProjectRoot "assets\images\app_icon.png"
if ((Test-Path $AppIconJpg) -and ((-not (Test-Path $AppIconPng)) -or ((Get-Item $AppIconJpg).LastWriteTimeUtc -gt (Get-Item $AppIconPng).LastWriteTimeUtc))) {
    Add-Type -AssemblyName System.Drawing
    $srcBmp = New-Object System.Drawing.Bitmap($AppIconJpg)
    $dstBmp = New-Object System.Drawing.Bitmap(64, 64, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for ($y = 0; $y -lt 64; $y++) {
        $sy = [int](($y * $srcBmp.Height + [int]($srcBmp.Height / 2)) / 64)
        for ($x = 0; $x -lt 64; $x++) {
            $sx = [int](($x * $srcBmp.Width + [int]($srcBmp.Width / 2)) / 64)
            $c = $srcBmp.GetPixel($sx, $sy)
            $dstBmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $c.R, $c.G, $c.B))
        }
    }
    $dstBmp.Save($AppIconPng, [System.Drawing.Imaging.ImageFormat]::Png)
    $dstBmp.Dispose()
    $srcBmp.Dispose()
}

Write-Host "[1/3] Publishing standalone win-x64 single-file executable..." -ForegroundColor Cyan
& $DotnetExe publish "$ProjectRoot\SubspaceProtocols.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -o $ReleaseFolder

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Write-Host "[2/3] Ensuring clean portable /data and /assets directories in release folder..." -ForegroundColor Cyan
$TargetDataDir = Join-Path $ReleaseFolder "data"
$TargetAssetsDir = Join-Path $ReleaseFolder "assets"

if (Test-Path $TargetDataDir) { Remove-Item $TargetDataDir -Recurse -Force }
if (Test-Path $TargetAssetsDir) { Remove-Item $TargetAssetsDir -Recurse -Force }

New-Item -ItemType Directory -Path $TargetDataDir -Force | Out-Null
$SourceDataDir = Join-Path $ProjectRoot "data"
Get-ChildItem -Path $SourceDataDir -Filter "*.json" -Recurse -File | ForEach-Object {
    $relativePath = $_.FullName.Substring($SourceDataDir.Length).TrimStart('\', '/')
    $destPath = Join-Path $TargetDataDir $relativePath
    $destDir = Split-Path -Parent $destPath
    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
    }
    Copy-Item -Path $_.FullName -Destination $destPath -Force
}

$SourceAssetsDir = Join-Path $ProjectRoot "assets"
Copy-Item -Path $SourceAssetsDir -Destination $TargetAssetsDir -Recurse -Force

$CommsSubDirs = @(
    "audio\comms\system",
    "audio\comms\voices\Human_GU",
    "audio\comms\voices\Kordani",
    "audio\comms\voices\Brengop",
    "audio\comms\voices\Krushvak",
    "audio\comms\voices\HunBorian",
    "audio\comms\voices\Oruun",
    "audio\comms\voices\Prunjun",
    "audio\comms\voices\Tolvak",
    "audio\comms\voices\GU_Alien"
)
foreach ($subDir in $CommsSubDirs) {
    $fullSubDir = Join-Path $TargetAssetsDir $subDir
    if (-not (Test-Path $fullSubDir)) {
        New-Item -ItemType Directory -Path $fullSubDir -Force | Out-Null
    }
}

$ExePath = Join-Path $ReleaseFolder "SubspaceProtocols.exe"
if (-not (Test-Path $ExePath)) {
    throw "Expected executable not found at: $ExePath"
}

$RequiredReleaseFiles = @(
    (Join-Path $TargetDataDir "config.json"),
    (Join-Path $TargetDataDir "cargo_catalog.json"),
    (Join-Path $TargetDataDir "sector_map.json"),
    (Join-Path $TargetDataDir "voice_profiles.json"),
    (Join-Path $TargetDataDir "dialogue\faction_pools.json"),
    (Join-Path $TargetAssetsDir "images\app_icon.jpg"),
    (Join-Path $TargetAssetsDir "images\app_icon.png"),
    (Join-Path $TargetAssetsDir "fonts\README.txt"),
    (Join-Path $TargetAssetsDir "audio\comms\README.txt")
)
foreach ($reqFile in $RequiredReleaseFiles) {
    if (-not (Test-Path $reqFile)) {
        throw "Expected release file not found at: $reqFile"
    }
}

Write-Host "[3/3] Portable release package ready at: $ReleaseFolder" -ForegroundColor Green
Get-ChildItem -Path $ReleaseFolder | Select-Object Name, Mode, Length | Format-Table -AutoSize
