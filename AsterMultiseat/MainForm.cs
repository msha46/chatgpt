using System.Drawing;

namespace AsterMultiseat;

public sealed class MainForm : Form
{
    private readonly ConfigStore _store;
    private readonly ListBox _seatList;
    private readonly ListBox _deviceList;
    private readonly ListBox _assignedList;
    private readonly TextBox _seatName;
    private readonly Button _addSeat;
    private readonly Button _removeSeat;
    private readonly Button _assignDevice;
    private readonly Button _unassignDevice;
    private readonly Button _runSeats;

    public MainForm(ConfigStore store)
    {
        _store = store;
        Text = "Aster Multiseat";
        Size = new Size(1100, 700);
        StartPosition = FormStartPosition.CenterScreen;

        var leftPanel = new Panel { Dock = DockStyle.Left, Width = 260, Padding = new Padding(12) };
        var centerPanel = new Panel { Dock = DockStyle.Left, Width = 360, Padding = new Padding(12) };
        var rightPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };

        _seatName = new TextBox { PlaceholderText = "Seat name", Dock = DockStyle.Top };
        _addSeat = new Button { Text = "Add Seat", Dock = DockStyle.Top, Height = 36 };
        _removeSeat = new Button { Text = "Remove Seat", Dock = DockStyle.Top, Height = 36 };
        _seatList = new ListBox { Dock = DockStyle.Fill };

        _deviceList = new ListBox { Dock = DockStyle.Fill };
        var deviceLabel = new Label { Text = "Input Devices (Raw Input)", Dock = DockStyle.Top, Height = 24 };

        _assignedList = new ListBox { Dock = DockStyle.Fill };
        var assignedLabel = new Label { Text = "Assigned Devices", Dock = DockStyle.Top, Height = 24 };

        _assignDevice = new Button { Text = "Assign →", Dock = DockStyle.Top, Height = 36 };
        _unassignDevice = new Button { Text = "← Unassign", Dock = DockStyle.Top, Height = 36 };
        _runSeats = new Button { Text = "Run Seats", Dock = DockStyle.Bottom, Height = 44 };

        leftPanel.Controls.Add(_seatList);
        leftPanel.Controls.Add(_removeSeat);
        leftPanel.Controls.Add(_addSeat);
        leftPanel.Controls.Add(_seatName);

        centerPanel.Controls.Add(_deviceList);
        centerPanel.Controls.Add(deviceLabel);

        rightPanel.Controls.Add(_assignedList);
        rightPanel.Controls.Add(assignedLabel);
        rightPanel.Controls.Add(_unassignDevice);
        rightPanel.Controls.Add(_assignDevice);
        rightPanel.Controls.Add(_runSeats);

        Controls.Add(rightPanel);
        Controls.Add(centerPanel);
        Controls.Add(leftPanel);

        _addSeat.Click += (_, _) => AddSeat();
        _removeSeat.Click += (_, _) => RemoveSeat();
        _assignDevice.Click += (_, _) => AssignDevice();
        _unassignDevice.Click += (_, _) => UnassignDevice();
        _runSeats.Click += (_, _) => RunSeats();
        _seatList.SelectedIndexChanged += (_, _) => RefreshAssignedDevices();

        Load += (_, _) =>
        {
            RefreshSeats();
            RefreshDevices();
        };
    }

    private void RefreshSeats()
    {
        var config = _store.Load();
        _seatList.Items.Clear();
        foreach (var seat in config.Seats.Values.OrderBy(seat => seat.Name))
        {
            _seatList.Items.Add(seat.Name);
        }
    }

    private void RefreshDevices()
    {
        _deviceList.Items.Clear();
        foreach (var device in RawInputListener.ListDevices())
        {
            _deviceList.Items.Add($"{device.Type} | {device.DeviceName}");
        }
    }

    private void RefreshAssignedDevices()
    {
        _assignedList.Items.Clear();
        if (_seatList.SelectedItem is not string seatName)
        {
            return;
        }

        var config = _store.Load();
        if (!config.Seats.TryGetValue(seatName, out var seat))
        {
            return;
        }

        foreach (var device in seat.Devices)
        {
            _assignedList.Items.Add(device);
        }
    }

    private void AddSeat()
    {
        var seatName = _seatName.Text.Trim();
        if (string.IsNullOrWhiteSpace(seatName))
        {
            MessageBox.Show("Enter a seat name.", "Aster Multiseat");
            return;
        }

        var config = _store.Load();
        if (config.Seats.ContainsKey(seatName))
        {
            MessageBox.Show("Seat already exists.", "Aster Multiseat");
            return;
        }

        config.Seats[seatName] = new SeatConfig { Name = seatName };
        _store.Save(config);
        _seatName.Clear();
        RefreshSeats();
    }

    private void RemoveSeat()
    {
        if (_seatList.SelectedItem is not string seatName)
        {
            MessageBox.Show("Select a seat to remove.", "Aster Multiseat");
            return;
        }

        var config = _store.Load();
        if (!config.Seats.Remove(seatName))
        {
            return;
        }

        _store.Save(config);
        RefreshSeats();
        RefreshAssignedDevices();
    }

    private void AssignDevice()
    {
        if (_seatList.SelectedItem is not string seatName)
        {
            MessageBox.Show("Select a seat first.", "Aster Multiseat");
            return;
        }

        if (_deviceList.SelectedItem is not string deviceLine)
        {
            MessageBox.Show("Select a device to assign.", "Aster Multiseat");
            return;
        }

        var deviceName = deviceLine.Split("|", 2)[1].Trim();
        var config = _store.Load();
        if (!config.Seats.TryGetValue(seatName, out var seat))
        {
            return;
        }

        if (!seat.Devices.Contains(deviceName))
        {
            seat.Devices.Add(deviceName);
            _store.Save(config);
            RefreshAssignedDevices();
        }
    }

    private void UnassignDevice()
    {
        if (_seatList.SelectedItem is not string seatName)
        {
            MessageBox.Show("Select a seat first.", "Aster Multiseat");
            return;
        }

        if (_assignedList.SelectedItem is not string deviceName)
        {
            MessageBox.Show("Select a device to remove.", "Aster Multiseat");
            return;
        }

        var config = _store.Load();
        if (!config.Seats.TryGetValue(seatName, out var seat))
        {
            return;
        }

        if (seat.Devices.Remove(deviceName))
        {
            _store.Save(config);
            RefreshAssignedDevices();
        }
    }

    private void RunSeats()
    {
        Hide();
        using var seats = new SeatRuntime(_store);
        seats.Run();
        Show();
    }
}
