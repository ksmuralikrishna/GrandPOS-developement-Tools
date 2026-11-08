# Grand POS Development Tool (GUI Application)

A standalone Windows desktop application designed to build, stage, deploy, and diagnose the legacy C++ Grand POS executable and DLLs with a single click.

## Features
- **Path Selection**: Browse or change the Source Code Root, POS Installation Directory, and MSBuild executable path.
- **Auto-Save Settings**: Saves your chosen paths and theme to `deployer_config.json` next to the `.exe` so you only configure it once.
- **Build Configurations**: Select between `Release` and `Debug`, and platforms `Win32` or `x64`.
- **Theme Modes**: Retro UI with on-the-fly toggle between **Retro Dark** (midnight terminal with neon cyan accents) and **Classic Retro** (vintage 95/98/2000 industrial gray with green CRT console).
- **One-Click Build & Deploy Actions**:
  - `⚡ Build + Deploy DLLs`
  - `⚡ Build + deploy GrandPOS.exe`
  - `🚀 Build + Deploy Everything`
  - `🔄 Rollback (Last Backup)`
- **Grand POS Process Control & Diagnostics**:
  - `▶ Start GrandPOS`: Starts GrandPOS.exe in its install folder.
  - `⏹ Stop GrandPOS`: Gracefully stops the running GrandPOS instance.
  - `🔄 Restart GrandPOS`: Safely stops and relaunches GrandPOS.exe.
  - `📄 Open Output.log`: Opens the GrandPOS runtime application log (`<InstallDir>\Output.log`) directly in Notepad.
  - **Live Status Indicator**: Auto-refreshes every 2 seconds, displaying `● POS: Running (PID: xxxx)` or `○ POS: Stopped`.
- **Live Output Log & Persistent File Logging**:
  - Real-time streaming log with color-coded steps, warnings, and errors.
  - Automatically writes all timestamped console and MSBuild activity to `GrandPOS_Deployer.log` in the application folder.
  - Convenient **"Open Log File"** button opens the persistent log in Notepad.
- **Safety Pipeline**:
  - Verifies build output before touching any running process.
  - Gracefully terminates `GrandPOS.exe`.
  - Creates a timestamped backup in `<InstallDir>\Backups\YYYYMMDD_HHMMSS\`.
  - Performs verified file copy with automatic rollback on error.
  - Automatically restarts `GrandPOS.exe`.

## How to Run
Simply double-click:
`GrandPOS_Deployer.exe`

## How to Recompile
If you ever edit `Program.cs`, double-click:
`build_gui.bat`

