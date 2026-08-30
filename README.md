# FakeWake - Stay Active! ☕

A Windows system tray application that keeps your session active and prevents Teams from marking you as Away. Features smart auto-pause when you're working, an activity tracker that counts your "productivity" hours, and hilarious milestone messages!

## Features

- **Prevents Idle Status**: Simulates keyboard activity every 60 seconds
- **Smart Auto-Pause**: Automatically pauses when you're typing or moving the mouse
- **Auto-Resume**: Automatically resumes after 2 minutes of inactivity
- **Activity Time Counter**: Tracks total time keeping you active across all sessions
- **Fun Milestone Messages**: Hilarious messages that evolve as you rack up hours
- **Dynamic System Tray Icons**: Bed with coffee when active, bed with ZZZ when paused
- **Pause/Resume**: Easy toggle to pause the keep-alive functionality
- **No Screen Interference**: Toggles Scroll Lock invisibly - completely silent, no console output
- **Prevents Sleep**: Keeps your display and system awake
- **Persistent Stats**: Your counter survives restarts and saves automatically
- **Secret Reset**: Click the time counter 5 times to reset (with a funny confirmation)

## How It Works

1. Keeps your PC active, as-if you're working
2. Toggles Scroll Lock invisibly every 60 seconds (completely silent, no output)
3. This activity prevents Teams and other social apps from marking you as idle/away
4. **Intelligent AI powered auto-pause** when you're actively working (typing/mouse movement)
5. **Auto-resumes** after you've been idle for 2 minutes. (Also AI powered... probably)
6. Tracks and displays your total active time ⌚
7. Earn achievements 🏆

## Smart Auto-Pause Feature

FakeWake is smart enough to get out of your way when you're actually working:

- **Detects Real Activity**: Monitors your keyboard and mouse activity
- **Auto-Pause**: When you're typing or moving the mouse, FakeWake automatically pauses
  - Status changes to "Auto-paused 💼" (you're working!)
  - Your counter stops tracking (only counts time when you're truly idle)
- **Auto-Resume**: After 2 minutes of no keyboard/mouse activity, FakeWake automatically resumes
  - Starts keeping you active again
  - Your counter starts tracking again
- **Manual Override**: You can still manually pause/resume anytime
  - Manual pause prevents auto-resume (stays paused until you manually resume)
  - Works seamlessly with the auto-pause feature

**Why is this useful?**
- Saves system resources when you're actually working
- Only simulates activity when you're truly idle
- Prevents double-activity (your real work + simulated activity)
- Makes the counter more accurate (only tracks true idle time)

## Achievements & Leaderboard

The counter displays hilarious messages that evolve as you accumulate active time:

| Time Range | Message |
|------------|---------|
| 0-30 min | "Rookie numbers" |
| 30min-1h | "Getting started" |
| 1-2h | "Productive vibes" |
| 2-4h | "Going strong" |
| 4-8h | "Full workday dodged" |
| 8-12h | "Dedication level: High" |
| 12-24h | "You're a legend" |
| 24-48h | "Superhuman detected" |
| 48-100h | "Absolute animal" |
| 100-200h | "Coffee addicted" |
| 200-500h | "Professional procrastinator" |
| 500-1000h | "Time wizard" |
| 1000+ hours | "Eternal presence achieved" |
| 1500+ hours | "Black hole time dilation" |

## Usage

1. Run `FakeWake.exe`
2. A bed with coffee icon will appear in your system tray (green = active)
3. **Start working?** FakeWake automatically pauses itself - icon turns gray with ZZZ
4. **Stepped away?** After 2 minutes of idle time, FakeWake automatically resumes
5. **Hover** over the icon to see your current stats
6. **Right-click** the icon for options:
   - View your current achievement and time counter
   - View current status (Active, Auto-paused, or Paused)
   - Pause/Resume manually
   - About information
   - Exit the application
7. **Double-click** the icon to quickly pause/resume
8. **Secret reset**: Click the time counter 5 times rapidly to reset your stats

## Where Your Stats Are Displayed

1. **Tray Icon Tooltip** - Hover to see your current time
2. **Context Menu** - Right-click shows your achievement and time counter
3. **Auto-Updates** - Stats refresh every 10 seconds while active

## Data Storage

- Stats are automatically saved to: `%AppData%\FakeWake\stats.txt`
- Saves every minute while active
- Saves when you pause or exit
- Survives restarts - your counter keeps growing!

## Building from Source

### Prerequisites
- .NET 6.0 SDK or later ([Download here](https://dotnet.microsoft.com/download/dotnet/6.0))

### Quick Build (Recommended)

**Option 1: Use the build script**
```bash
build-portable.bat
```
Creates a standalone executable at: `bin\Release\net6.0-windows\win-x64\publish\FakeWake.exe`

**Option 2: Regular build**
```bash
build.bat
```
Creates executable at: `bin\Release\net6.0-windows\FakeWake.exe` (requires .NET runtime)

### Manual Build Instructions

**Standard Build:**
```bash
dotnet build -c Release
```
Output: `bin\Release\net6.0-windows\FakeWake.exe` (148 KB, requires .NET runtime)

**Portable Single-File Executable:**
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```
Output: `bin\Release\net6.0-windows\win-x64\publish\FakeWake.exe` (146 MB, fully standalone)

## Icon

The application features dual-state icons:

- **Active Icon** - A bed with a coffee cup on a green background
  - Represents "faking" being awake while cozy
  - Displayed when FakeWake is actively keeping you active

- **Inactive/Paused Icon** - A bed with ZZZ on a gray background
  - Displayed when paused (manual or auto-pause)
  - Clearly shows the app is resting

The icons change automatically based on state, giving you instant visual feedback!

## Technical Details

- **Language**: C# (.NET 6.0)
- **Framework**: Windows Forms
- **Target**: Windows (net6.0-windows)
- **Activity Method**: Scroll Lock toggle (completely invisible, no console output)
- **Activity Detection**: Windows GetLastInputInfo API (detects real keyboard/mouse activity)
- **Power Management**: Uses Windows SetThreadExecutionState API
- **Update Frequency**:
  - Keep-alive: Every 60 seconds
  - Stats display: Every 10 seconds
  - Activity check: Every 5 seconds
  - Auto-save: Every minute
- **Auto-Pause Threshold**: 5 seconds (if user activity detected in last 5 seconds)
- **Auto-Resume Threshold**: 2 minutes (120 seconds of no user activity)

## Tips

- **First Time**: Your counter starts at "Rookie numbers" - everyone starts somewhere!
- **Long Sessions**: The messages get funnier the longer you go
- **Icon Changes**: Green bed+coffee when active, gray bed+ZZZ when paused
- **Just Started Working?**: Wait ~5 seconds and FakeWake will automatically pause itself
- **Stepping Away?**: After 2 minutes of no activity, FakeWake automatically resumes
- **Manual Control**: Click Pause anytime - it won't auto-resume until you manually Resume
- **Reset Anytime**: Click the time counter 5 times to start fresh
- **Counter Accuracy**: Counter only tracks when FakeWake is actually active (not when you're working)
- **Multiple PCs**: Stats are local to each machine

## Disclaimer

Use this tool responsibly and in accordance with your organization's policies. This tool is for legitimate use cases where you need to prevent your system from going idle during active work sessions.

## Version

**FakeWake v1.5** - Now with achievements, smart auto-pause, and secret reset!

---

Made with ☕ and a sense of humor
