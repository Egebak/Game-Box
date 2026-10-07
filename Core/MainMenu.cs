using System;
using System.IO;
using GameBox.Shared.Audio;
using GameBox.Shared.UI;
using Godot;

namespace GameBox.Core;

public partial class MainMenu : Control
{
    private readonly MiniGameRegistry _registry = MiniGameRegistry.CreateDefault();
    private VBoxContainer _column = null!;
    private GameBoxState _state = null!;
    private string _savePath = string.Empty;

    // Lets smoke tests exercise first launch without touching the player's profile.
    public string? SavePathOverride { get; set; }

    public override void _Ready()
    {
        _savePath = SavePathOverride ?? ProjectSettings.GlobalizePath("user://game_box_state.json");
        _state = GameBoxState.Load(_savePath);

        AddChild(UiStyle.Backdrop(UiStyle.Night));
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        _column = new VBoxContainer { CustomMinimumSize = new Vector2(640, 0) };
        _column.AddThemeConstantOverride("separation", 28);
        center.AddChild(_column);

        if (_state.HasChildName)
            ShowGames();
        else
            ShowNamePrompt();
    }

    private void ShowNamePrompt()
    {
        _column.AddChild(UiStyle.Label("GAME BOX", 76, UiStyle.Gold));
        _column.AddChild(UiStyle.Label("Hvad hedder du?", 36, UiStyle.Sky));
        _column.AddChild(UiStyle.Label("Skriv dit navn. Det gemmes kun på denne computer.", 24));

        var nameInput = new LineEdit
        {
            PlaceholderText = "Dit navn",
            MaxLength = 24,
            CustomMinimumSize = new Vector2(420, 64),
            Alignment = HorizontalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        nameInput.AddThemeFontSizeOverride("font_size", 30);
        _column.AddChild(nameInput);

        var error = UiStyle.Label(string.Empty, 22, new Color("#ff9b88"));
        _column.AddChild(error);

        var continueButton = UiStyle.Button("Fortsæt", 27);
        continueButton.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        SoundEffects.BindHover(continueButton);
        _column.AddChild(continueButton);

        void SaveName()
        {
            if (!_state.TrySetChildName(nameInput.Text))
            {
                error.Text = "Skriv et navn for at fortsætte.";
                return;
            }

            try
            {
                _state.Save(_savePath);
            }
            catch (IOException)
            {
                error.Text = "Navnet kunne ikke gemmes. Prøv igen.";
                return;
            }
            catch (UnauthorizedAccessException)
            {
                error.Text = "Navnet kunne ikke gemmes. Prøv igen.";
                return;
            }

            SoundEffects.Play("ui_select", -11f);
            foreach (var child in _column.GetChildren())
            {
                _column.RemoveChild(child);
                child.QueueFree();
            }
            ShowGames();
        }

        nameInput.TextSubmitted += _ => SaveName();
        continueButton.Pressed += SaveName;
        nameInput.GrabFocus();
    }

    private void ShowGames()
    {
        _column.AddChild(UiStyle.Label($"Hej, {_state.ChildName}!", 34, UiStyle.Sky));
        _column.AddChild(UiStyle.Label("GAME BOX", 76, UiStyle.Gold));
        _column.AddChild(UiStyle.Label("Vælg et spil!", 27));

        var tiles = new HFlowContainer { Alignment = FlowContainer.AlignmentMode.Center };
        tiles.AddThemeConstantOverride("h_separation", 20);
        _column.AddChild(tiles);
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

        if (_state.TankArenaCompleted)
            _column.AddChild(UiStyle.Label("★ Tank Arena gennemført!", 24, UiStyle.Gold));
        if (_state.ConstructionSiteCompleted)
            _column.AddChild(UiStyle.Label("★ Gravemaskine-plads gennemført!", 24, UiStyle.Gold));

        var quit = UiStyle.Button("Afslut", 23);
        quit.CustomMinimumSize = new Vector2(210, 58);
        quit.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        SoundEffects.BindHover(quit);
        quit.Pressed += () => { SoundEffects.Play("ui_back", -12f); GetTree().Quit(); };
        _column.AddChild(quit);
    }
}
