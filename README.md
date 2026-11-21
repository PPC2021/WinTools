# WinTools

Simple Windows productivity toolset. It keeps your PC awake and "active" during your working
hours, so it doesn't sleep, turn off the display or lock while you're reading, presenting or
remoted into another machine. Outside working hours, or while you're on a break, it steps aside
and Windows behaves normally. (macOS port: [MacTools](https://github.com/PPC2021/MacTools).)

## Requirements

- Windows 10 / 11, or Windows Server 2016 and later (.NET Framework 4.6.2, built in)
- To build: the .NET SDK (`dotnet`)

## Install

```powershell
git clone https://github.com/PPC2021/WinTools.git
cd WinTools
.\install.ps1                 # installs to %LOCALAPPDATA%\Programs\WinTools, starts at sign-in, runs in the tray
.\install.ps1 -Portable       # same, plus a standalone WinTools.exe on the Desktop
.\install.ps1 -PortableOnly   # only the standalone exe on the Desktop
.\install.ps1 -NoStartup      # install without starting at sign-in
.\install.ps1 -Uninstall
```

No admin rights are needed. The installer also adds a Start menu shortcut.

### Portable

`WinTools.exe` is a single file with nothing to install: copy it to any machine (Desktop, USB
stick, network share, RDP clipboard) and double-click it. A copy that isn't installed shows
**WinTools (portable)** in its title bar.

- **Install on this PC...** (button in the window, or in the tray menu) installs that copy for
  the current user: it goes to `%LOCALAPPDATA%\Programs\WinTools`, gets a Start menu shortcut and
  starts in the tray at every sign-in. No admin rights. Running it again on a newer exe updates
  the install.
- From a script or remote shell: `WinTools.exe --install` (installs and starts it in the tray)
  and `WinTools.exe --uninstall`.
- **Settings that travel:** put a `WinTools.ini` next to the exe and that copy uses it instead
  of the machine's `%APPDATA%\WinTools\settings.ini` (changes are saved back to it, and it's
  copied along on install). Any keys you leave out use the defaults, for example:

  ```ini
  AlwaysActive=True
  StartOnLaunch=True
  TimeZone=Eastern Standard Time
  Use24Hour=False
  BreakMinutes=30
  ```

  Also: `ScheduleEnabled`, `Days=Monday,Tuesday,...`, `StartTime=08:00`, `EndTime=17:00`.
  Time zone names are Windows ids (`Get-TimeZone -ListAvailable` lists them).
- The exe isn't code-signed, so if it was downloaded from the internet Windows SmartScreen may
  warn the first time: choose **More info → Run anyway**, or right-click the file →
  **Properties → Unblock**. Copies from a network share or USB stick usually aren't flagged.
- If you tick **Open WinTools at sign-in** from a portable copy, Windows starts *that* copy at
  sign-in, so tick it again after moving it (or use **Install on this PC** instead).

### Start automatically at sign-in

WinTools isn't a Windows service on purpose: a service runs in a separate, invisible session and
can't keep *your* desktop awake or read your idle time. Like MacTools, it starts at **sign-in**
instead and lives in the system tray:

- **App:** tick **Open WinTools at sign-in**, plus **Start keep-alive when WinTools launches**
  (or leave **Always active** on). It shows up in Task Manager → Startup apps.
- **Script:** `WinTools.exe --login-item on|off|status`.

It uses the per-user Run key (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`) and starts
with `--tray`, so no window appears at sign-in.

## Using it

The tray icon and the window/taskbar icon show the state:
🌙 snooze: stopped · ☕ outline cup: running, but waiting for working hours or on a break ·
☕ filled, steaming cup: actively keeping the PC awake. The .exe icon is the steaming cup;
`tools\make-icon.ps1` regenerates it from the same drawing code (`WinTools\TrayIcons.cs`).

Left-click the tray icon to show or hide the window. Right-click it for **Start/Stop**,
**Always active**, **Take a Break**, **End Day** and **Quit**. Closing the window (X) keeps WinTools running
in the tray; use **Quit** to exit.

- **Start / Stop**: turns keep-alive on or off.
- **Always active (ignore schedule)**: an override that keeps the PC awake whatever the
  schedule says (the schedule is greyed out while it's on). Turning it on also starts keep-alive,
  and **Stop** turns it off. Breaks still apply.
- **Working Hours Schedule**: pick the days and start/end times (default Mon–Fri 08:00–17:00).
  An end time before the start time spans midnight (for example 22:00–06:00), and start = end
  means all day.
- **Time zone**: the zone the schedule follows (default: the Windows time zone). For example,
  keep 08:00–17:00 Eastern hours while the PC is set to Central. The window shows the current
  time in that zone. This doesn't change the Windows clock.
- **Use 24-hour clock**: switches all times between 13:30 and 1:30 PM.
- **Take a Break**: pauses keep-alive for 1–240 minutes (default 15) with a live countdown
  ("Break ends in 14:32", also on the button and the tray tooltip). **End Break** resumes early.
- **End Day**: like a break, but until your next working-hours start (e.g. "Day ended - resumes
  Mon 8:00 AM"), or midnight if the schedule is off or has no days. **Resume** undoes it.
- **Open WinTools at sign-in** / **Start keep-alive when WinTools launches**: see above.
- All settings are remembered between launches.

Only one copy runs at a time; launching it again brings the running copy forward.

## How it works

- `SetThreadExecutionState(CONTINUOUS | SYSTEM_REQUIRED | DISPLAY_REQUIRED)` stops sleep and display-off.
- A zero-distance `SendInput` mouse move every 10 s resets the lock-screen idle timer (chat apps
  such as Teams and Slack usually count it as activity too).
- `GetLastInputInfo` gives the idle time.
