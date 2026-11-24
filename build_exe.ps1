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
Copy-Item -Path (Join-Path $ProjectRoot "data\*.json") -Destination $TargetDataDir -Force
Copy-Item -Path (Join-Path $ProjectRoot "assets") -Destination $TargetAssetsDir -Recurse -Force

$ExePath = Join-Path $ReleaseFolder "SubspaceProtocols.exe"
if (-not (Test-Path $ExePath)) {
    throw "Expected executable not found at: $ExePath"
}

$SectorMapPath = Join-Path $TargetDataDir "sector_map.json"
if (-not (Test-Path $SectorMapPath)) {
    throw "Expected sector_map.json not found at: $SectorMapPath"
}

Write-Host "[3/3] Portable release package ready at: $ReleaseFolder" -ForegroundColor Green
Get-ChildItem -Path $ReleaseFolder | Select-Object Name, Mode, Length | Format-Table -AutoSize
