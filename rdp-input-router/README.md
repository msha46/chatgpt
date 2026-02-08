# RDP Input Router (Windows 11 22H2 Prototype)

This is a **prototype** Windows desktop app that lets you pick one keyboard and one mouse to route into a specific RDP session window while your other devices remain local. It uses Windows Raw Input to identify per-device events and then forwards those events into the selected RDP window.

> ⚠️ **Limitations:** This is not a kernel-level filter. It forwards input by focusing the RDP window and replaying events with `SendInput`, so it is best-effort only. Some keys (secure attention sequences, media keys, etc.) will not route correctly. A production-grade solution would require a signed driver (e.g., Interception/HIDGuardian) to fully block local processing.

## Requirements

- Windows 11 22H2
- .NET 6 Desktop Runtime / SDK

If you see an error like `No .NET SDKs were found`, install the .NET 6 SDK from https://aka.ms/dotnet/download and reopen your terminal so `dotnet --info` works.

## Build

```powershell
cd rdp-input-router

dotnet build
```

## Run

```powershell
cd rdp-input-router

dotnet run
```

## Usage

1. Launch the app.
2. Pick the keyboard and mouse that should be routed into the RDP session.
3. Set the RDP window title fragment (default: `Remote Desktop`).
4. Start or connect to your RDP session in `mstsc.exe`.
5. The selected keyboard/mouse will be forwarded to that RDP window.

## Notes on security & focus

- The router sets the RDP window to the foreground before forwarding each event.
- You can pin the RDP window on the second monitor or keep it in focus to reduce flicker.
- If you need strict isolation (device 1 never affects the local OS), you will need a driver-based solution.

## Next steps (if you want a production build)

- Implement a kernel-level filter (e.g., Interception driver) to suppress local events.
- Add a tray icon + hotkey to toggle routing.
- Add per-window auto-detection for multiple RDP sessions.
