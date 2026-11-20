# Subspace Protocols

A sci-fi bureaucratic customs inspection and management game set aboard **Void Gate 3**, built as a standalone native Windows desktop application in **C# (.NET 8)** and **Raylib-cs** with a procedural pixel-art renderer.

> **Status:** Early Playtest Prototype (Work in Progress)

---

## Prerequisites

- **OS:** Windows 10 / 11 (64-bit)
- **SDK:** [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (required to run the build script)

---

## Installation & Build Steps

1. **Download or clone** this repository to your machine (if downloading as a ZIP from GitHub, extract the `Subspace-Protocols-main` folder).
2. **Run the build script:**
   - Open PowerShell inside the root `Subspace-Protocols-main` folder and run:
     ```powershell
     .\build_exe.ps1
     ```
   - *(If Windows blocks the script due to execution policies, run it with:)*
     ```powershell
     powershell -ExecutionPolicy Bypass -File .\build_exe.ps1
     ```
3. **Navigate to the compiled release folder:**
   ```text
   Subspace-Protocols-main\release\SubspaceProtocols_win-x64
   ```
4. **Launch the game:**
   - Run `SubspaceProtocols.exe`.

---

## Controls & Testing Shortcuts

- **Mouse (Left Click / Drag):** Operate desk controls, toggle the Subspace Power-Grid Scanner (SPS), drag Data Slates into the Clearance Dock, and tear off printed strips.
- **`F11` / `Alt + Enter`:** Toggle borderless fullscreen.
- **`F1` (or `` ` ``):** Open the Dev Debug Overlay (time speed multiplier, day jump, force ship reseed, and testing flags).
- **Art Test Bench:** Run `SubspaceProtocols.exe --art-test` from a terminal to inspect procedural starships and species portraits.

---

## Notes

- Save files are automatically stored in `%APPDATA%\SubspaceProtocols\`.
- If the application encounters an unexpected error, check `crash.log` inside the release folder alongside `SubspaceProtocols.exe`.
