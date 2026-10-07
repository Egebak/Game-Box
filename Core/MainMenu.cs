using System;
using ChristiansSpilBox.Shared.Audio;
using ChristiansSpilBox.Shared.UI;
using Godot;

namespace ChristiansSpilBox.Core;

public partial class MainMenu : Control
{
    private readonly MiniGameRegistry _registry = MiniGameRegistry.CreateDefault();

    public override void _Ready()
    {
        AddChild(UiStyle.Backdrop(UiStyle.Night));
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(640, 0) };
        column.AddThemeConstantOverride("separation", 28);
        center.AddChild(column);
        column.AddChild(UiStyle.Label("CHRISTIANS", 34, UiStyle.Sky));
        column.AddChild(UiStyle.Label("SPIL BOX", 76, UiStyle.Gold));
        column.AddChild(UiStyle.Label("Vælg et spil!", 27));

        var tiles = new HFlowContainer { Alignment = FlowContainer.AlignmentMode.Center };
        tiles.AddThemeConstantOverride("h_separation", 20);
        column.AddChild(tiles);
        foreach (var game in _registry.Games)
        {
            var tile = UiStyle.Button($"{game.Title}\n{game.Description}", 35);
            tile.CustomMinimumSize = new Vector2(480, 170);
            tile.Icon = GD.Load<Texture2D>(game.IconPath);
            tile.IconAlignment = HorizontalAlignment.Left;
            SoundEffects.BindHover(tile);
            tile.Pressed += () =>
            {
                SoundEffects.Play("ui_select", -11f);
                GetTree().ChangeSceneToFile(game.ScenePath);
            };
            tiles.AddChild(tile);
        }

        var state = GameBoxState.Load(ProjectSettings.GlobalizePath("user://game_box_state.json"));
        if (state.TankArenaCompleted)
            column.AddChild(UiStyle.Label("★ Tank Arena gennemført!", 24, UiStyle.Gold));
        if (state.ConstructionSiteCompleted)
            column.AddChild(UiStyle.Label("★ Gravemaskine-plads gennemført!", 24, UiStyle.Gold));

        var quit = UiStyle.Button("Afslut", 23);
        quit.CustomMinimumSize = new Vector2(210, 58);
        quit.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        SoundEffects.BindHover(quit);
        quit.Pressed += () => { SoundEffects.Play("ui_back", -12f); GetTree().Quit(); };
        column.AddChild(quit);
    }
}
