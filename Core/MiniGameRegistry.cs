using System;
using System.Collections.Generic;
using System.Linq;

namespace GameBox.Core;

public sealed class MiniGameRegistry
{
    private readonly Dictionary<string, MiniGameDefinition> _games = new(StringComparer.Ordinal);

    public IReadOnlyCollection<MiniGameDefinition> Games => _games.Values;

    public void Register(MiniGameDefinition game)
    {
        if (string.IsNullOrWhiteSpace(game.Id) || string.IsNullOrWhiteSpace(game.ScenePath))
            throw new ArgumentException("A minigame needs an ID and entry scene.");
        if (!_games.TryAdd(game.Id, game))
            throw new ArgumentException($"Minigame '{game.Id}' is already registered.");
    }

    public MiniGameDefinition Get(string id) => _games.TryGetValue(id, out var game)
        ? game : throw new KeyNotFoundException($"Unknown minigame '{id}'.");

    public static MiniGameRegistry CreateDefault()
    {
        var registry = new MiniGameRegistry();
        registry.Register(new MiniGameDefinition("tank-arena", "Tank Arena",
            "res://Games/TankArena/Scenes/TankArena.tscn", "res://Shared/Assets/tank.svg", "Kør, sigt og undvig!"));
        registry.Register(new MiniGameDefinition("construction-site", "Gravemaskine-plads",
            "res://Games/ConstructionSite/Scenes/ConstructionSite.tscn",
            "res://Shared/Assets/excavator.svg", "Grav jord og fyld lastbilen!"));
        registry.Register(new MiniGameDefinition("word-mission", "Ordmission",
            "res://Games/WordMission/Scenes/WordMission.tscn",
            "res://Shared/Assets/wordmission.svg", "Find lyde og byg ord!"));
        return registry;
    }
}
