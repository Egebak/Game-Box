using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameBox.Core;

public sealed class GameBoxState
{
    public string ChildName { get; set; } = string.Empty;
    public bool TankArenaCompleted { get; set; }
    public bool ConstructionSiteCompleted { get; set; }

    [JsonIgnore]
    public bool HasChildName => !string.IsNullOrWhiteSpace(ChildName);

    public bool TrySetChildName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > 24)
            return false;

        ChildName = trimmed;
        return true;
    }

    public static GameBoxState FromJson(string json)
    {
        try { return JsonSerializer.Deserialize<GameBoxState>(json) ?? new GameBoxState(); }
        catch (JsonException) { return new GameBoxState(); }
    }

    public string ToJson() => JsonSerializer.Serialize(this);

    public static GameBoxState Load(string path)
    {
        try { return File.Exists(path) ? FromJson(File.ReadAllText(path)) : new GameBoxState(); }
        catch (IOException) { return new GameBoxState(); }
        catch (UnauthorizedAccessException) { return new GameBoxState(); }
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, ToJson());
    }
}
