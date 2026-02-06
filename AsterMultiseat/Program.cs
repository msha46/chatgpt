using AsterMultiseat;

ApplicationConfiguration.Initialize();

var store = new ConfigStore();

if (args.Length == 0 || args[0] == "ui")
{
    Application.Run(new MainForm(store));
    return;
}

var command = args[0];

switch (command)
{
    case "list-devices":
        foreach (var device in RawInputListener.ListDevices())
        {
            Console.WriteLine($"{device.Type}: {device.DeviceName}");
        }
        break;
    case "add-seat":
        RequireArgs(args, 2);
        AddSeat(store, args[1]);
        break;
    case "remove-seat":
        RequireArgs(args, 2);
        RemoveSeat(store, args[1]);
        break;
    case "assign-device":
        RequireArgs(args, 3);
        AssignDevice(store, args[1], args[2]);
        break;
    case "unassign-device":
        RequireArgs(args, 3);
        UnassignDevice(store, args[1], args[2]);
        break;
    case "run":
        using (var seats = new SeatRuntime(store))
        {
            seats.Run();
        }
        break;
    default:
        Console.WriteLine($"Unknown command: {command}");
        break;
}

static void RequireArgs(string[] args, int count)
{
    if (args.Length < count)
    {
        throw new ArgumentException("Not enough arguments.");
    }
}

static void AddSeat(ConfigStore store, string name)
{
    var config = store.Load();
    if (config.Seats.ContainsKey(name))
    {
        Console.WriteLine($"Seat '{name}' already exists.");
        return;
    }

    config.Seats[name] = new SeatConfig { Name = name };
    store.Save(config);
    Console.WriteLine($"Added seat '{name}'.");
}

static void RemoveSeat(ConfigStore store, string name)
{
    var config = store.Load();
    if (!config.Seats.Remove(name))
    {
        Console.WriteLine($"Seat '{name}' not found.");
        return;
    }

    store.Save(config);
    Console.WriteLine($"Removed seat '{name}'.");
}

static void AssignDevice(ConfigStore store, string seatName, string device)
{
    var config = store.Load();
    if (!config.Seats.TryGetValue(seatName, out var seat))
    {
        Console.WriteLine($"Seat '{seatName}' not found.");
        return;
    }

    if (seat.Devices.Contains(device))
    {
        Console.WriteLine($"Device already assigned to '{seatName}'.");
        return;
    }

    seat.Devices.Add(device);
    store.Save(config);
    Console.WriteLine($"Assigned device to '{seatName}'.");
}

static void UnassignDevice(ConfigStore store, string seatName, string device)
{
    var config = store.Load();
    if (!config.Seats.TryGetValue(seatName, out var seat))
    {
        Console.WriteLine($"Seat '{seatName}' not found.");
        return;
    }

    if (!seat.Devices.Remove(device))
    {
        Console.WriteLine($"Device not assigned to '{seatName}'.");
        return;
    }

    store.Save(config);
    Console.WriteLine($"Unassigned device from '{seatName}'.");
}
