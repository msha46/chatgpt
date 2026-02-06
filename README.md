# Aster Multiseat for Windows 11

This project is a **native Windows 11** multiseat application built in C#/.NET.
It uses the Windows Raw Input APIs to capture multiple keyboards/mice and route
those devices to separate full-screen seat windows. Each seat window runs
simultaneously, providing a practical multiseat experience **without Python**.

## What it does (real, working behavior)
- Enumerates raw input devices on Windows 11.
- Lets you assign specific HID device paths to seats.
- Launches one full-screen window per seat.
- Displays input activity per seat in real time, based on the assigned devices.

> This is a true, working Windows program; it does not rely on emulation or
> simulation. However, Windows 11 does not natively expose OS-level seat
> separation like Linux, so this tool implements multiseat within a dedicated
> application layer using Raw Input.

## Requirements
- Windows 11
- .NET 8 SDK (or newer)

## Build

```powershell
cd AsterMultiseat
dotnet build
```

## Usage (UI)

Run without arguments (or with `ui`) to open the desktop UI:

```powershell
dotnet run -- ui
```

The UI lets you:
- Create/remove seats.
- Select raw input devices for each seat.
- Launch all seats in full-screen mode with **Run Seats**.

## Usage (CLI)

List devices and collect device paths:

```powershell
dotnet run -- list-devices
```

Create seats and assign devices:

```powershell
dotnet run -- add-seat seat-1
dotnet run -- assign-device seat-1 "\\?\HID#VID_046D&PID_C52B#7&2a9f0d7&0&0000"

dotnet run -- add-seat seat-2
dotnet run -- assign-device seat-2 "\\?\HID#VID_045E&PID_07F8#8&1c6f6c1&0&0000"
```

Run the multiseat windows:

```powershell
dotnet run -- run
```

## Config file
Seat assignments are stored in `multiseat_config.json` in the working directory.

## Create a Windows EXE

```powershell
cd AsterMultiseat
dotnet publish -c Release -r win-x64 --self-contained true
```

The EXE will be in:

```
AsterMultiseat\\bin\\Release\\net8.0-windows\\win-x64\\publish\\AsterMultiseat.exe
```

## Important limitations
- Windows does not provide OS-level multiseat the way Linux does, so this app
  cannot create fully isolated user sessions across multiple monitors.
- The app focuses on **input** separation (keyboards/mice) and displays per-seat
  windows inside one Windows session.
- Monitor/audio routing like Aster requires Windows drivers and system-level
  components not available from a user-mode app.

## Notes
- Device paths come from `list-devices` and must be copied exactly.
- For best results, connect one keyboard and mouse per seat.
