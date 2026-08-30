# FakeWake Copilot Instructions

## Build & Runtime Environment

**Target Framework**: .NET 4.5 (Windows Forms)

**Prerequisites**: .NET 6.0 SDK or later required for building

### Build Commands

```bash
# Standard build (requires .NET runtime)
build.bat
# Output: bin\Release\net6.0-windows\FakeWake.exe (148 KB)

# Portable single-file executable (self-contained, ~146 MB)
build-portable.bat
# Output: bin\Release\net6.0-windows\win-x64\publish\FakeWake.exe

# Manual dotnet commands
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

**Note**: No automated tests or linting setup exists in the repository.

## Architecture Overview

FakeWake is a Windows system tray application with a **service-based architecture**:

### Core Components

1. **FakeWakeApplication** (`FakeWakeApplication.cs`)
   - Main ApplicationContext that orchestrates all services
   - Manages three critical timers: keep-alive (60s), stats display (10s), activity check (5s)
   - State machine: `Active` → `AutoPaused` (when user active) → resumes after 2 min idle
   - Handles icon/menu updates and user interactions

2. **Services** (folder: `Services/`)
   - **StatsManager**: Tracks cumulative active time, persists to `%AppData%\FakeWake\stats.txt`, calculates achievement levels
   - **ActivityMonitor**: Polls Windows API for user activity (keyboard/mouse), detects active (<5s idle) vs idle (≥120s)
   - **KeepAliveService**: Toggles Scroll Lock invisibly to signal activity to Teams; manages power state via SetThreadExecutionState

3. **UI** (folder: `UI/`)
   - **TrayMenuManager**: Context menu strip with status, stats (clickable 5x for reset), toggle, and exit options
   - **IconFactory**: Generates bed+coffee icon (green, active) and bed+ZZZ icon (gray, paused)

4. **Models** (folder: `Models/`)
   - **AppState** enum: `Active`, `AutoPaused`, `ManuallyPaused`

5. **Native** (folder: `Native/`)
   - **Win32Api**: PInvoke wrapper for kernel32/user32 (SetThreadExecutionState, keybd_event, GetLastInputInfo, GetTickCount)
   - Centralized location for all Windows API interop

### Event Flow

```
Timer Tick (60s) → SimulateActivity (toggle Scroll Lock) → Teams sees activity
Timer Tick (5s)  → ActivityMonitor.Check() → Detect user activity → Auto-pause/resume
Timer Tick (10s) → Update stats display & achievement text
```

## Key Conventions

### State Management
- **State transitions** are handled in `SetState()` method of FakeWakeApplication
- Auto-pause is triggered when `ActivityMonitor.IsUserActive == true`
- Auto-resume occurs after 2 minutes of `IdleTimeSeconds >= 120`
- Manual pause blocks auto-resume until user manually resumes

### Windows API Usage
- **All Win32 interop is in `Native/Win32Api.cs`** — add new PInvoke declarations there
- Activity simulation uses **Scroll Lock** (invisible, leaves no console output)
- Idle time detection uses `GetLastInputInfo` + `GetTickCount`
- Power management uses `SetThreadExecutionState` with flags: ES_CONTINUOUS, ES_SYSTEM_REQUIRED, ES_DISPLAY_REQUIRED

### Data Persistence
- Stats are **automatically loaded** when StatsManager is instantiated (checked once)
- Stats are **saved on**:
  - Pause/Resume events (via `PauseSession()` / `ResumeSession()`)
  - Reset (secret 5-click on stats item)
  - Every minute while active (implied by timer logic)
- File location: `%AppData%\FakeWake\stats.txt` (plain text, ticks in TimeSpan)
- Reset is triggered by clicking stats item 5 times within 2 seconds

### Achievement Tiers
- Achievement text is calculated by `StatsManager.GetAchievement()` based on cumulative hours
- 13 tiers from "Rookie numbers" (< 0.5h) to "Eternal presence achieved" (1000+ hours)
- Funny milestone messages are hardcoded in FakeWakeApplication (e.g., reset confirmation dialogs)

### Icon & UI Updates
- Active icon: green background with bed + coffee cup
- Paused icon: gray background with bed + ZZZ
- Tray icon updates whenever state changes
- Menu status text reflects current state: "Status: Active", "Status: Auto-paused 💼", or "Status: Paused"
- Toggle button text changes to "Resume" when paused, "Pause" when active

## Extending the Codebase

### Adding a New Feature
1. Create service in `Services/` if it manages state or interacts with external systems
2. Add model/enum to `Models/` if it represents domain logic
3. Hook events from services into FakeWakeApplication main orchestrator
4. Add menu items or tray interactions in `UI/TrayMenuManager`

### Adding Windows API Calls
1. Add PInvoke declarations to `Native/Win32Api.cs`
2. Add helper methods (e.g., `GetIdleTimeSeconds()`) that abstract the raw API
3. Call helpers from services, not raw API

### Testing State Changes
- Manual testing: Run FakeWake.exe, hover/right-click tray icon, check state transitions
- Debug logging: Add `Console.WriteLine` in timer ticks (note: console is hidden in WinExe)
- Use Debugger: Attach debugger to running process, set breakpoints in SetState/timer handlers

## File Structure
```
FakeWake/
├── FakeWakeApplication.cs          # Main orchestrator & state machine
├── Program.cs                       # Entry point (STAThread)
├── FakeWake.csproj                 # Project file (.NET 4.5 -> net6.0-windows)
├── Models/
│   └── AppState.cs                 # Enum: Active, AutoPaused, ManuallyPaused
├── Services/
│   ├── StatsManager.cs             # Time tracking & persistence
│   ├── ActivityMonitor.cs          # User activity detection
│   └── KeepAliveService.cs         # Scroll Lock simulation & power state
├── UI/
│   ├── TrayMenuManager.cs          # Context menu + icon management
│   └── IconFactory.cs              # Icon generation
├── Native/
│   └── Win32Api.cs                 # Win32 PInvoke declarations & helpers
├── Standalone/                     # (Optional, check contents if needed)
├── build.bat                        # Standard build script
└── build-portable.bat              # Portable single-file build script
```

## Notes for Contributors

- **No unit tests**: This is a simple system tray app with direct OS integration
- **No linting**: Apply .NET conventions (PascalCase for public, camelCase for private)
- **Minimal dependencies**: Only System.Windows.Forms, no NuGet packages
- **Thread safety**: Timer callbacks run on UI thread; no multi-threaded state access
- **Settings files**: `.claude/` contains local Claude.ai settings (version-controlled as reference)
