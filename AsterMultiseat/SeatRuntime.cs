using System.Drawing;

namespace AsterMultiseat;

public sealed class SeatRuntime : IDisposable
{
    private readonly ConfigStore _store;
    private readonly RawInputListener _listener;
    private readonly Dictionary<string, SeatWindow> _windows;
    private readonly Dictionary<string, string> _deviceMap;

    public SeatRuntime(ConfigStore store)
    {
        _store = store;
        _listener = new RawInputListener();
        _windows = new Dictionary<string, SeatWindow>();
        _deviceMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public void Run()
    {
        var config = _store.Load();
        if (config.Seats.Count == 0)
        {
            MessageBox.Show("No seats configured.", "Aster Multiseat");
            return;
        }

        var palette = new[]
        {
            Color.FromArgb(30, 136, 229),
            Color.FromArgb(216, 27, 96),
            Color.FromArgb(67, 160, 71),
            Color.FromArgb(255, 143, 0)
        };

        var index = 0;
        foreach (var seat in config.Seats.Values)
        {
            var window = new SeatWindow(seat.Name, palette[index % palette.Length]);
            _windows[seat.Name] = window;
            foreach (var device in seat.Devices)
            {
                _deviceMap[device] = seat.Name;
            }
            index++;
        }

        _listener.InputReceived += OnInput;
        foreach (var form in _windows.Values)
        {
            form.Show();
        }

        Application.Run();

        _listener.InputReceived -= OnInput;
    }

    private void OnInput(object? sender, RawInputEventArgs eventArgs)
    {
        if (!_deviceMap.TryGetValue(eventArgs.Device, out var seatName))
        {
            return;
        }

        if (!_windows.TryGetValue(seatName, out var window))
        {
            return;
        }

        var header = eventArgs.Raw.Header;
        if (header.dwType == (uint)RawInputDeviceType.Keyboard)
        {
            var key = eventArgs.Raw.Data.Keyboard.VKey;
            window.UpdateStatus($"{seatName}\nKeyboard: 0x{key:X}");
        }
        else if (header.dwType == (uint)RawInputDeviceType.Mouse)
        {
            var dx = eventArgs.Raw.Data.Mouse.lLastX;
            var dy = eventArgs.Raw.Data.Mouse.lLastY;
            window.UpdateStatus($"{seatName}\nMouse: Δ({dx}, {dy})");
        }
    }

    public void Dispose()
    {
        foreach (var window in _windows.Values)
        {
            window.Close();
        }

        _listener.Dispose();
    }
}
