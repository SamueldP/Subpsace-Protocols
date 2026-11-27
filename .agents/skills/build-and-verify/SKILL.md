---
name: build-and-verify
description: >-
  Build, smoke-test, and package Subspace Protocols as a standalone native Windows x64 executable.
  Use when compiling Debug/Release, running --smoke-test, or executing build_exe.ps1.
---

# Subspace Protocols — Build, Smoke-Test & Release Packaging

Follow this runbook whenever building, verifying, or packaging `SubspaceProtocols.csproj` on this Windows machine.

## Environment Constraints

1. **Use the local `.NET 8` SDK binary**: System `C:\Program Files\dotnet\dotnet.exe` may lack the `.NET 8` SDK. Always invoke:
   ```powershell
   $d = "$env:USERPROFILE\.dotnet\dotnet.exe"
   ```
2. **Windows PowerShell 5.1 chaining**: Do NOT use `&&` (invalid in PS 5.1). Chain dependent commands with:
   ```powershell
   ; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE };
   ```
3. **Preserve user-dropped assets before `build_exe.ps1`**: `build_exe.ps1` deletes `release\SubspaceProtocols_win-x64\` before copying from repository `assets\` and `data\`. Before running `build_exe.ps1`, check whether `release\SubspaceProtocols_win-x64\assets\` contains newer user-installed files (such as `.ttf`/`.otf` fonts or comms audio) not yet in repo `assets\`, and copy them into repo `assets\` first so they are not wiped.

## Verification Steps

1. **Pre-Build Sync Checklist (when adding data catalogs or infractions):**
   - If you added a new `data/*.json` file, register it in `$RequiredReleaseFiles` inside `build_exe.ps1` so release packaging verifies its presence.
   - If you added new day-gated `InfractionType` values in `ShipGenerator.cs`, update the per-day infraction assertions inside `RunWorkShiftAndBranchingVerificationSuite` in `Program.cs` before compiling.

2. **Run Static Guardrails, Build Debug & Release (`0 Warning(s)`, `0 Error(s)`), and Execute `--smoke-test`:**
   ```powershell
   $rawHits = Get-ChildItem -Path Core,Data,Simulation,Procedural,Rendering,UI -Filter *.cs -Recurse | Where-Object { $_.Name -ne 'FontManager.cs' } | Select-String -Pattern 'Raylib\.DrawText\b|Raylib\.MeasureText\b'
   if ($rawHits) { Write-Error "Raw Raylib text call detected: $rawHits"; exit 1 }
   $simRaylib = Get-ChildItem -Path Data,Simulation -Filter *.cs -Recurse | Select-String -Pattern 'Raylib_cs'
   if ($simRaylib) { Write-Error "Raylib_cs leaked into Data/Simulation layer: $simRaylib"; exit 1 }
   $d = "$env:USERPROFILE\.dotnet\dotnet.exe"
   & $d build -c Debug --nologo; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
   & $d build -c Release --nologo; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
   & $d run -c Debug --no-build -- --smoke-test
   ```
   Confirm exit code `0`, `[SMOKE ...]` PASS lines, and zero leaked GPU/audio resources on shutdown (`PixelBuffer.ActiveProceduralTextureCount == 0`, `FontManager.ActiveGpuFontCount == 0`, `AudioHooks.ActiveSoundAliasCount == 0`, `AudioHooks.ActiveSoundCount == 0`).

3. **Package standalone `win-x64` release and smoke-test the published `.exe` (when packaging is requested):**
   ```powershell
   if (Test-Path 'release\SubspaceProtocols_win-x64\assets') {
       $relRoot = (Resolve-Path 'release\SubspaceProtocols_win-x64\assets').Path
       $srcRoot = (Resolve-Path 'assets').Path
       Get-ChildItem -Path $relRoot -Recurse -File | ForEach-Object {
           $relPath = $_.FullName.Substring($relRoot.Length).TrimStart('\','/')
           $srcPath = Join-Path $srcRoot $relPath
           if (-not (Test-Path $srcPath)) {
               $dir = Split-Path -Parent $srcPath
               if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
               Copy-Item -Path $_.FullName -Destination $srcPath -Force
           }
       }
   }
   powershell -ExecutionPolicy Bypass -File .\build_exe.ps1; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
   & '.\release\SubspaceProtocols_win-x64\SubspaceProtocols.exe' --smoke-test
   ```
   Confirm `release\SubspaceProtocols_win-x64\SubspaceProtocols.exe`, `release\SubspaceProtocols_win-x64\data\config.json`, and `release\SubspaceProtocols_win-x64\assets\fonts\PixelOperatorMono-Bold.ttf` exist after packaging.

4. **Confirm read-only `/design` untouched:**
   ```powershell
   git status --short design/
   ```
   Output must be empty.
