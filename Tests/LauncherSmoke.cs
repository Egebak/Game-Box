using System;
using System.Linq;
using ChristiansSpilBox.Core;
using ChristiansSpilBox.Games.ConstructionSite;
using ChristiansSpilBox.Games.TankArena;
using Godot;

public partial class LauncherSmoke : Node
{
    public override void _Ready() => CallDeferred(nameof(Run));

    private async void Run()
    {
        var tree = GetTree();
        try
        {
            var menu = GD.Load<PackedScene>("res://Core/MainMenu.tscn").Instantiate<MainMenu>();
            AddChild(menu);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            var tile = menu.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(x => x.Text.Contains("Tank Arena"));
            Check(tile.Icon != null, "launcher has tank icon");
            var constructionTile = menu.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(x => x.Text.Contains("Gravemaskine-plads"));
            Check(constructionTile.Icon != null, "launcher has excavator icon");

            var launches = 0;
            var constructionLaunches = 0;
            var returnedFromConstruction = false;
            tree.SceneChanged += () =>
            {
                try
                {
                    var scene = tree.CurrentScene;
                    if (scene is ConstructionSite construction)
                    {
                        constructionLaunches++;
                        Check(construction.Player.IsInsideTree(), constructionLaunches == 1
                            ? "construction tile launches excavator scene"
                            : "construction restart creates a fresh scene");
                        construction.FindChildren("*", "Button", true, false).OfType<Button>()
                            .Single(x => x.Text == "Opgave: Fyld lastbilen").EmitSignal(BaseButton.SignalName.Pressed);
                        tree.Root.GetViewport().PushInput(new InputEventKey { Keycode = Key.Escape, Pressed = true });
                        void AfterConstructionPause()
                        {
                            tree.ProcessFrame -= AfterConstructionPause;
                            try
                            {
                                Check(tree.Paused, "construction ESC opens pause menu");
                                var action = constructionLaunches == 1 ? "Spil igen" : "Tilbage til Spil Box";
                                if (constructionLaunches > 1) returnedFromConstruction = true;
                                construction.FindChildren("*", "Button", true, false).OfType<Button>()
                                    .Single(x => x.Text == action && x.IsVisibleInTree())
                                    .EmitSignal(BaseButton.SignalName.Pressed);
                            }
                            catch (Exception error)
                            {
                                GD.PushError($"LAUNCHER_SMOKE_FAIL: {error}");
                                tree.Quit(1);
                            }
                        }
                        tree.ProcessFrame += AfterConstructionPause;
                    }
                    else if (scene is TankArena arena)
                    {
                        launches++;
                        Check(arena.GetChildren().OfType<TankUnit>().Any(x => x.IsPlayer),
                            launches == 1 ? "tile launches Tank Arena" : "restart creates a fresh Tank Arena");
                        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = true });
                        void AfterPause()
                        {
                            tree.ProcessFrame -= AfterPause;
                            try
                            {
                                Check(tree.Paused, "pause opens from launched game");
                                var action = launches == 1 ? "Spil igen" : "Tilbage til Spil Box";
                                var button = arena.FindChildren("*", "Button", true, false).OfType<Button>()
                                    .Single(x => x.Text == action && x.IsVisibleInTree());
                                button.EmitSignal(BaseButton.SignalName.Pressed);
                            }
                            catch (Exception error)
                            {
                                GD.PushError($"LAUNCHER_SMOKE_FAIL: {error}");
                                tree.Quit(1);
                            }
                        }
                        tree.ProcessFrame += AfterPause;
                    }
                    else if (scene is MainMenu)
                    {
                        Check(!tree.Paused, "return clears pause and reopens Spil Box");
                        if (returnedFromConstruction && launches == 0)
                        {
                            var tankTile = scene.FindChildren("*", "Button", true, false).OfType<Button>()
                                .Single(x => x.Text.Contains("Tank Arena"));
                            tankTile.EmitSignal(BaseButton.SignalName.Pressed);
                        }
                        else
                        {
                            tree.CreateTimer(1.6).Timeout += () =>
                            {
                                GD.Print("LAUNCHER_SMOKE_PASS");
                                tree.Quit(0);
                            };
                        }
                    }
                }
                catch (Exception error)
                {
                    GD.PushError($"LAUNCHER_SMOKE_FAIL: {error}");
                    tree.Quit(1);
                }
            };
            constructionTile.EmitSignal(BaseButton.SignalName.Pressed);
        }
        catch (Exception error)
        {
            GD.PushError($"LAUNCHER_SMOKE_FAIL: {error}");
            tree.Quit(1);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        GD.Print($"PASS: {message}");
    }
}
