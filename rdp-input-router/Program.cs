using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace RdpInputRouter;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new RouterForm());
    }
}

internal sealed class RouterForm : Form
{
    private readonly ListBox _keyboardList = new();
    private readonly ListBox _mouseList = new();
    private readonly TextBox _rdpTitle = new();
    private readonly Label _status = new();
    private readonly Button _refresh = new();

    private readonly Dictionary<IntPtr, string> _deviceNames = new();
    private IntPtr _selectedKeyboard = IntPtr.Zero;
    private IntPtr _selectedMouse = IntPtr.Zero;

    public RouterForm()
    {
        Text = "RDP Input Router (Prototype)";
        Width = 640;
        Height = 420;
        StartPosition = FormStartPosition.CenterScreen;

        var keyboardLabel = new Label { Text = "Keyboard for RDP", Left = 20, Top = 20, Width = 180 };
        _keyboardList.Left = 20;
        _keyboardList.Top = 45;
        _keyboardList.Width = 260;
        _keyboardList.Height = 220;
        _keyboardList.SelectedIndexChanged += (_, _) =>
        {
            _selectedKeyboard = _keyboardList.SelectedItem is DeviceItem item ? item.Handle : IntPtr.Zero;
        };

        var mouseLabel = new Label { Text = "Mouse for RDP", Left = 320, Top = 20, Width = 180 };
        _mouseList.Left = 320;
        _mouseList.Top = 45;
        _mouseList.Width = 260;
        _mouseList.Height = 220;
        _mouseList.SelectedIndexChanged += (_, _) =>
        {
            _selectedMouse = _mouseList.SelectedItem is DeviceItem item ? item.Handle : IntPtr.Zero;
        };

        var rdpLabel = new Label { Text = "RDP window title contains", Left = 20, Top = 280, Width = 220 };
        _rdpTitle.Left = 250;
        _rdpTitle.Top = 276;
        _rdpTitle.Width = 330;
        _rdpTitle.Text = "Remote Desktop";

        _refresh.Text = "Refresh devices";
        _refresh.Left = 20;
        _refresh.Top = 320;
        _refresh.Width = 140;
        _refresh.Click += (_, _) => RefreshDevices();

        _status.Left = 180;
        _status.Top = 324;
        _status.Width = 400;
        _status.Text = "Select devices to route into RDP window.";

        Controls.AddRange(new Control[]
        {
            keyboardLabel,
            _keyboardList,
            mouseLabel,
            _mouseList,
            rdpLabel,
            _rdpTitle,
            _refresh,
            _status
        });

        Load += (_, _) =>
        {
            RegisterRawInput();
            RefreshDevices();
        };
    }

    private void RegisterRawInput()
    {
        var rid = new RAWINPUTDEVICE[2];
        rid[0] = new RAWINPUTDEVICE
        {
            usUsagePage = 0x01,
            usUsage = 0x06,
            dwFlags = RawInputDeviceFlags.InputSink,
            hwndTarget = Handle
        };
        rid[1] = new RAWINPUTDEVICE
        {
            usUsagePage = 0x01,
            usUsage = 0x02,
            dwFlags = RawInputDeviceFlags.InputSink,
            hwndTarget = Handle
        };

        if (!RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
        {
            _status.Text = "Failed to register raw input devices.";
        }
    }

    private void RefreshDevices()
    {
        _keyboardList.Items.Clear();
        _mouseList.Items.Clear();
        _deviceNames.Clear();

        uint deviceCount = 0;
        if (GetRawInputDeviceList(IntPtr.Zero, ref deviceCount, (uint)Marshal.SizeOf<RAWINPUTDEVICELIST>()) != 0)
        {
            _status.Text = "Unable to enumerate raw input devices.";
            return;
        }

        var list = new RAWINPUTDEVICELIST[deviceCount];
        if (GetRawInputDeviceList(list, ref deviceCount, (uint)Marshal.SizeOf<RAWINPUTDEVICELIST>()) == unchecked((uint)-1))
        {
            _status.Text = "Unable to enumerate raw input devices.";
            return;
        }

        foreach (var device in list)
        {
            var name = GetDeviceName(device.hDevice);
            if (device.dwType == RawInputDeviceType.Keyboard)
            {
                _keyboardList.Items.Add(new DeviceItem(device.hDevice, name));
            }
            else if (device.dwType == RawInputDeviceType.Mouse)
            {
                _mouseList.Items.Add(new DeviceItem(device.hDevice, name));
            }
        }

        _status.Text = "Devices refreshed.";
    }

    private string GetDeviceName(IntPtr deviceHandle)
    {
        uint size = 0;
        GetRawInputDeviceInfo(deviceHandle, RawInputDeviceInfoCommand.DeviceName, IntPtr.Zero, ref size);
        if (size == 0)
        {
            return "Unknown device";
        }

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetRawInputDeviceInfo(deviceHandle, RawInputDeviceInfoCommand.DeviceName, buffer, ref size) > 0)
            {
                var name = Marshal.PtrToStringAnsi(buffer);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    _deviceNames[deviceHandle] = name;
                    return name;
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return "Unknown device";
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_INPUT = 0x00FF;
        if (m.Msg == WM_INPUT)
        {
            HandleRawInput(m.LParam);
        }

        base.WndProc(ref m);
    }

    private void HandleRawInput(IntPtr lParam)
    {
        uint size = 0;
        GetRawInputData(lParam, RawInputDataCommand.Input, IntPtr.Zero, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>());
        if (size == 0)
        {
            return;
        }

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetRawInputData(lParam, RawInputDataCommand.Input, buffer, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>()) != size)
            {
                return;
            }

            var raw = Marshal.PtrToStructure<RAWINPUT>(buffer);
            if (raw.header.dwType == RawInputDeviceType.Keyboard && raw.header.hDevice == _selectedKeyboard)
            {
                RouteKeyboard(raw);
            }
            else if (raw.header.dwType == RawInputDeviceType.Mouse && raw.header.hDevice == _selectedMouse)
            {
                RouteMouse(raw);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private void RouteKeyboard(RAWINPUT raw)
    {
        var target = FindRdpWindow();
        if (target == IntPtr.Zero)
        {
            _status.Text = "RDP window not found.";
            return;
        }

        SetForegroundWindow(target);
        var input = new INPUT
        {
            type = InputType.Keyboard,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = raw.keyboard.MakeCode,
                    dwFlags = BuildKeyboardFlags(raw.keyboard.Flags)
                }
            }
        };

        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    private void RouteMouse(RAWINPUT raw)
    {
        var target = FindRdpWindow();
        if (target == IntPtr.Zero)
        {
            _status.Text = "RDP window not found.";
            return;
        }

        SetForegroundWindow(target);
        var mouseFlags = raw.mouse.usButtonFlags;
        var input = new INPUT
        {
            type = InputType.Mouse,
            U = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    dx = raw.mouse.lLastX,
                    dy = raw.mouse.lLastY,
                    mouseData = raw.mouse.usButtonData,
                    dwFlags = MapMouseFlags(mouseFlags, raw.mouse.lLastX, raw.mouse.lLastY)
                }
            }
        };

        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    private static MOUSEEVENTF MapMouseFlags(ushort flags, int deltaX, int deltaY)
    {
        MOUSEEVENTF result = 0;
        if ((flags & RawMouseButtons.LeftDown) != 0) result |= MOUSEEVENTF.LEFTDOWN;
        if ((flags & RawMouseButtons.LeftUp) != 0) result |= MOUSEEVENTF.LEFTUP;
        if ((flags & RawMouseButtons.RightDown) != 0) result |= MOUSEEVENTF.RIGHTDOWN;
        if ((flags & RawMouseButtons.RightUp) != 0) result |= MOUSEEVENTF.RIGHTUP;
        if ((flags & RawMouseButtons.MiddleDown) != 0) result |= MOUSEEVENTF.MIDDLEDOWN;
        if ((flags & RawMouseButtons.MiddleUp) != 0) result |= MOUSEEVENTF.MIDDLEUP;
        if ((flags & RawMouseButtons.MouseWheel) != 0) result |= MOUSEEVENTF.WHEEL;
        if ((flags & RawMouseButtons.MouseHWheel) != 0) result |= MOUSEEVENTF.HWHEEL;
        if (deltaX != 0 || deltaY != 0) result |= MOUSEEVENTF.MOVE;
        return result;
    }

    private static KEYEVENTF BuildKeyboardFlags(RawKeyboardFlags flags)
    {
        var result = KEYEVENTF.SCANCODE;
        if (flags.HasFlag(RawKeyboardFlags.Break))
        {
            result |= KEYEVENTF.KEYUP;
        }

        if (flags.HasFlag(RawKeyboardFlags.Extended))
        {
            result |= KEYEVENTF.EXTENDEDKEY;
        }

        return result;
    }

    private IntPtr FindRdpWindow()
    {
        var text = _rdpTitle.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return IntPtr.Zero;
        }

        var candidates = Process.GetProcessesByName("mstsc");
        foreach (var process in candidates)
        {
            if (process.MainWindowHandle != IntPtr.Zero && process.MainWindowTitle.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                return process.MainWindowHandle;
            }
        }

        return IntPtr.Zero;
    }

    private sealed record DeviceItem(IntPtr Handle, string Name)
    {
        public override string ToString() => Name;
    }

    private enum RawInputDeviceType : uint
    {
        Mouse = 0,
        Keyboard = 1,
        HID = 2
    }

    [Flags]
    private enum RawKeyboardFlags : ushort
    {
        None = 0,
        Extended = 0x01,
        Break = 0x02,
        E0 = 0x02,
        E1 = 0x04
    }

    [Flags]
    private enum RawInputDeviceFlags : uint
    {
        InputSink = 0x00000100
    }

    private enum RawInputDeviceInfoCommand : uint
    {
        DeviceName = 0x20000007
    }

    private enum RawInputDataCommand : uint
    {
        Input = 0x10000003
    }

    [Flags]
    private enum RawMouseButtons : ushort
    {
        LeftDown = 0x0001,
        LeftUp = 0x0002,
        RightDown = 0x0004,
        RightUp = 0x0008,
        MiddleDown = 0x0010,
        MiddleUp = 0x0020,
        MouseWheel = 0x0400,
        MouseHWheel = 0x0800
    }

    [Flags]
    private enum MOUSEEVENTF : uint
    {
        MOVE = 0x0001,
        LEFTDOWN = 0x0002,
        LEFTUP = 0x0004,
        RIGHTDOWN = 0x0008,
        RIGHTUP = 0x0010,
        MIDDLEDOWN = 0x0020,
        MIDDLEUP = 0x0040,
        WHEEL = 0x0800,
        HWHEEL = 0x01000
    }

    private enum InputType : uint
    {
        Mouse = 0,
        Keyboard = 1
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICE
    {
        public ushort usUsagePage;
        public ushort usUsage;
        public RawInputDeviceFlags dwFlags;
        public IntPtr hwndTarget;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICELIST
    {
        public IntPtr hDevice;
        public RawInputDeviceType dwType;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTHEADER
    {
        public RawInputDeviceType dwType;
        public uint dwSize;
        public IntPtr hDevice;
        public IntPtr wParam;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct RAWINPUT
    {
        [FieldOffset(0)]
        public RAWINPUTHEADER header;
        [FieldOffset(24)]
        public RAWMOUSE mouse;
        [FieldOffset(24)]
        public RAWKEYBOARD keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWKEYBOARD
    {
        public ushort MakeCode;
        public RawKeyboardFlags Flags;
        public ushort Reserved;
        public ushort VKey;
        public uint Message;
        public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWMOUSE
    {
        public ushort usFlags;
        public ushort usButtonFlags;
        public ushort usButtonData;
        public uint ulRawButtons;
        public int lLastX;
        public int lLastY;
        public uint ulExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public InputType type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public MOUSEEVENTF dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public KEYEVENTF dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [Flags]
    private enum KEYEVENTF : uint
    {
        EXTENDEDKEY = 0x0001,
        KEYUP = 0x0002,
        SCANCODE = 0x0008
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

    [DllImport("user32.dll")]
    private static extern uint GetRawInputDeviceList([Out] RAWINPUTDEVICELIST[] pRawInputDeviceList, ref uint uiNumDevices, uint cbSize);

    [DllImport("user32.dll")]
    private static extern uint GetRawInputDeviceList(IntPtr pRawInputDeviceList, ref uint uiNumDevices, uint cbSize);

    [DllImport("user32.dll")]
    private static extern uint GetRawInputDeviceInfo(IntPtr hDevice, RawInputDeviceInfoCommand uiCommand, IntPtr pData, ref uint pcbSize);

    [DllImport("user32.dll")]
    private static extern uint GetRawInputData(IntPtr hRawInput, RawInputDataCommand uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

    [DllImport("user32.dll")]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
