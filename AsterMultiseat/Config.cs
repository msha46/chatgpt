using System.Text.Json;

namespace AsterMultiseat;

public sealed class SeatConfig
{
    public string Name { get; set; } = string.Empty;
    public List<string> Devices { get; set; } = new();
}

public sealed class MultiSeatConfig
{
    public Dictionary<string, SeatConfig> Seats { get; set; } = new();
}

public sealed class ConfigStore
{
    public string Path { get; }

    public ConfigStore(string? path = null)
    {
        Path = path ?? "multiseat_config.json";
    }

    public MultiSeatConfig Load()
    {
        if (!File.Exists(Path))
        {
            return new MultiSeatConfig();
        }

        var json = File.ReadAllText(Path);
        return JsonSerializer.Deserialize<MultiSeatConfig>(json) ?? new MultiSeatConfig();
    }

    public void Save(MultiSeatConfig config)
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(Path, json);
    }
}
