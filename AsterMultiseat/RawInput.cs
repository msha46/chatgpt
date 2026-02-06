using System.Runtime.InteropServices;

namespace AsterMultiseat;

public enum RawInputDeviceType
{
    Mouse = 0,
    Keyboard = 1,
    Hid = 2
}

public readonly record struct RawInputDeviceInfo(string DeviceName, RawInputDeviceType Type);

public sealed class RawInputListener : NativeWindow, IDisposable
{
    public event EventHandler<RawInputEventArgs>? InputReceived;

    private bool _disposed;

    public RawInputListener()
    {
        CreateHandle(new CreateParams());
        RegisterDevices();
    }

    public static IReadOnlyList<RawInputDeviceInfo> ListDevices()
    {
        uint deviceCount = 0;
        if (GetRawInputDeviceList(IntPtr.Zero, ref deviceCount, (uint)Marshal.SizeOf<RAWINPUTDEVICELIST>()) != 0)
        {
            throw new InvalidOperationException("Failed to query raw input device list.");
        }

        var list = new RAWINPUTDEVICELIST[deviceCount];
        if (GetRawInputDeviceList(list, ref deviceCount, (uint)Marshal.SizeOf<RAWINPUTDEVICELIST>()) == uint.MaxValue)
        {
            throw new InvalidOperationException("Failed to enumerate raw input devices.");
        }

        var devices = new List<RawInputDeviceInfo>();
        foreach (var entry in list)
        {
            var deviceName = GetDeviceName(entry.hDevice);
            devices.Add(new RawInputDeviceInfo(deviceName, (RawInputDeviceType)entry.dwType));
        }

        return devices;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_INPUT)
        {
            uint size = 0;
            GetRawInputData(m.LParam, RID_INPUT, IntPtr.Zero, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>());
            if (size > 0)
            {
                var buffer = Marshal.AllocHGlobal((int)size);
                try
                {
                    if (GetRawInputData(m.LParam, RID_INPUT, buffer, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>()) == size)
                    {
                        var raw = Marshal.PtrToStructure<RAWINPUT>(buffer);
                        var device = GetDeviceName(raw.Header.hDevice);
                        InputReceived?.Invoke(this, new RawInputEventArgs(device, raw));
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }

        base.WndProc(ref m);
    }

    private void RegisterDevices()
    {
        var devices = new[]
        {
            new RAWINPUTDEVICE
            {
                usUsagePage = 0x01,
                usUsage = 0x02,
                dwFlags = RIDEV_INPUTSINK,
                hwndTarget = Handle
            },
            new RAWINPUTDEVICE
            {
                usUsagePage = 0x01,
                usUsage = 0x06,
                dwFlags = RIDEV_INPUTSINK,
                hwndTarget = Handle
            }
        };

        if (!RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
        {
            throw new InvalidOperationException("Failed to register raw input devices.");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DestroyHandle();
        GC.SuppressFinalize(this);
    }

    private static string GetDeviceName(IntPtr device)
    {
        uint size = 0;
        GetRawInputDeviceInfo(device, RIDI_DEVICENAME, IntPtr.Zero, ref size);
        if (size == 0)
        {
            return "unknown";
        }

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            GetRawInputDeviceInfo(device, RIDI_DEVICENAME, buffer, ref size);
            return Marshal.PtrToStringAnsi(buffer) ?? "unknown";
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private const int WM_INPUT = 0x00FF;
    private const int RID_INPUT = 0x10000003;
    private const int RIDI_DEVICENAME = 0x20000007;
    private const int RIDEV_INPUTSINK = 0x00000100;

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICELIST
    {
        public IntPtr hDevice;
        public uint dwType;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICE
    {
        public ushort usUsagePage;
        public ushort usUsage;
        public int dwFlags;
        public IntPtr hwndTarget;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RAWINPUTHEADER
    {
        public uint dwType;
        public uint dwSize;
        public IntPtr hDevice;
        public IntPtr wParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RAWMOUSE
    {
        public ushort usFlags;
        public uint ulButtons;
        public ushort usButtonFlags;
        public ushort usButtonData;
        public uint ulRawButtons;
        public int lLastX;
        public int lLastY;
        public uint ulExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RAWKEYBOARD
    {
        public ushort MakeCode;
        public ushort Flags;
        public ushort Reserved;
        public ushort VKey;
        public uint Message;
        public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct RAWINPUTDATA
    {
        [FieldOffset(0)]
        public RAWMOUSE Mouse;
        [FieldOffset(0)]
        public RAWKEYBOARD Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RAWINPUT
    {
        public RAWINPUTHEADER Header;
        public RAWINPUTDATA Data;
    }

    [DllImport("User32.dll", SetLastError = true)]
    private static extern uint GetRawInputDeviceList(
        [Out] RAWINPUTDEVICELIST[]? pRawInputDeviceList,
        ref uint puiNumDevices,
        uint cbSize);

    [DllImport("User32.dll", SetLastError = true)]
    private static extern uint GetRawInputDeviceInfo(
        IntPtr hDevice,
        uint uiCommand,
        IntPtr pData,
        ref uint pcbSize);

    [DllImport("User32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices(
        RAWINPUTDEVICE[] pRawInputDevices,
        uint uiNumDevices,
        uint cbSize);

    [DllImport("User32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(
        IntPtr hRawInput,
        uint uiCommand,
        IntPtr pData,
        ref uint pcbSize,
        uint cbSizeHeader);
}

public sealed class RawInputEventArgs : EventArgs
{
    public RawInputEventArgs(string device, RawInputListener.RAWINPUT raw)
    {
        Device = device;
        Raw = raw;
    }

    public string Device { get; }
    public RawInputListener.RAWINPUT Raw { get; }
}
