using System;
using System.IO;
using System.Text.Json;

namespace ChristiansSpilBox.Core;

public sealed class GameBoxState
{
    public bool TankArenaCompleted { get; set; }
    public bool ConstructionSiteCompleted { get; set; }

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
