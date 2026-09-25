using System.Text.Json;
using System.Text.Json.Serialization;

namespace PandaPlateWatch;

/// <summary>Remembers which game days we already alerted on, so re-runs stay quiet.</summary>
public interface IStateStore
{
    bool AlreadyNotified(string gameDate);
    void Record(string gameDate);
}

public sealed class NullStateStore : IStateStore
{
    public static readonly NullStateStore Instance = new();
    public bool AlreadyNotified(string gameDate) => false;
    public void Record(string gameDate) { }
}

public sealed class FileStateStore(string path, int keep = 30) : IStateStore
{
    public bool AlreadyNotified(string gameDate) => Read().Contains(gameDate);

    public void Record(string gameDate)
    {
        var notified = Read().Where(d => d != gameDate).ToList();
        notified.Add(gameDate);
        if (notified.Count > keep)
        {
            notified.RemoveRange(0, notified.Count - keep);
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(
            new StateFile { Notified = notified },
            StateJsonContext.Default.StateFile) + "\n");
    }

    private List<string> Read()
    {
        try
        {
            return JsonSerializer.Deserialize(
                       File.ReadAllText(path), StateJsonContext.Default.StateFile)
                   ?.Notified ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException
                                       or UnauthorizedAccessException)
        {
            // A missing or corrupt state file just means "nothing recorded yet";
            // never let it stop an alert going out.
            return [];
        }
    }
}

internal sealed class StateFile
{
    [JsonPropertyName("notified")]
    public List<string> Notified { get; set; } = [];
}

[JsonSerializable(typeof(StateFile))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal sealed partial class StateJsonContext : JsonSerializerContext;
