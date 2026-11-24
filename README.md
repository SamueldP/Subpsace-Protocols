# Subspace Protocols

A sci-fi bureaucratic customs inspection and management game set aboard **Void Gate 3**, built as a standalone native Windows desktop application in **C# (.NET 8)** and **Raylib-cs** with a procedural pixel-art renderer.

> **Status:** Early Playtest Prototype (Work in Progress)

---

## How to Play (Recommended)

No coding tools or .NET installation required—everything needed to run the game is bundled in the release download.

1. Go to the **[Releases](https://github.com/SamueldP/Subspace-Protocols/releases)** page on the right-hand side of this repository.
2. Download the latest `SubspaceProtocols-vX.X.zip` file under **Assets**.
3. Extract the `.zip` folder anywhere on your PC (Windows 10 / 11, 64-bit).
4. Open the extracted folder and run **`SubspaceProtocols.exe`**.

*(Note: Because this is an unsigned indie prototype, Windows SmartScreen may show a "Windows protected your PC" pop-up on first launch. Click **More info** -> **Run anyway**.)*

---

## Controls & Testing Shortcuts

- **Mouse (Left Click / Drag):** Operate desk controls, toggle the Subspace Power-Grid Scanner (SPS), drag Data Slates into the Clearance Dock, and tear off printed strips.
- **`F11` / `Alt + Enter`:** Toggle borderless fullscreen.
- **`F1` (or `` ` ``):** Open the Dev Debug Overlay (time speed multiplier, day jump, force ship reseed, and testing flags).
- **Art Test Bench:** Run `SubspaceProtocols.exe --art-test` from a terminal to inspect procedural starships and species portraits.

---

## Building from Source (Optional)

Only needed if you want to modify or compile the C# source code yourself.

### Prerequisites
- **OS:** Windows 10 / 11 (64-bit)
- **SDK:** [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

### Build Steps
1. Clone or download this repository and open PowerShell inside the root project folder.
2. Run the build script:
   ```powershell
   .\build_exe.ps1
   ```
   *(If Windows blocks the script due to execution policies, run:)*
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\build_exe.ps1
   ```
3. Launch the compiled executable at:
   ```text
   release\SubspaceProtocols_win-x64\SubspaceProtocols.exe
   ```

---

## Notes

- Save files are automatically stored in `%APPDATA%\SubspaceProtocols\`.
- If the application encounters an unexpected error, check `crash.log` in the same folder as `SubspaceProtocols.exe`.