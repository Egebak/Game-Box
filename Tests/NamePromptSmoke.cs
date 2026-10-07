using System;
using System.IO;
using System.Linq;
using GameBox.Core;
using Godot;

public partial class NamePromptSmoke : Node
{
    public override void _Ready() => CallDeferred(nameof(Run));

    private async void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "game-box-name-smoke", Guid.NewGuid().ToString("N"));
        var savePath = Path.Combine(directory, "state.json");
        try
        {
            new GameBoxState { TankArenaCompleted = true }.Save(savePath);
            var scene = GD.Load<PackedScene>("res://Core/MainMenu.tscn");
            var first = scene.Instantiate<MainMenu>();
            first.SavePathOverride = savePath;
            AddChild(first);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            var input = first.FindChildren("*", "LineEdit", true, false).OfType<LineEdit>().Single();
            Check(!first.FindChildren("*", "Button", true, false).OfType<Button>()
                .Any(button => button.Text.Contains("Tank Arena")), "games wait for a name");
            first.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.Text == "Fortsæt").EmitSignal(BaseButton.SignalName.Pressed);
            Check(!GameBoxState.Load(savePath).HasChildName, "blank name is not saved");

            input.Text = "  Mia  ";
            input.EmitSignal(LineEdit.SignalName.TextSubmitted, input.Text);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(GameBoxState.Load(savePath).ChildName == "Mia", "submitted name is saved locally");
            Check(GameBoxState.Load(savePath).TankArenaCompleted, "naming preserves earlier progress");
            Check(first.FindChildren("*", "Button", true, false).OfType<Button>()
                .Any(button => button.Text.Contains("Tank Arena")), "games appear after naming");

            first.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var second = scene.Instantiate<MainMenu>();
            second.SavePathOverride = savePath;
            AddChild(second);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(!second.FindChildren("*", "LineEdit", true, false).OfType<LineEdit>().Any(),
                "returning child skips name prompt");
            Check(second.FindChildren("*", "Label", true, false).OfType<Label>()
                .Any(label => label.Text == "Hej, Mia!"), "saved name appears in greeting");
            second.QueueFree();
            await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
            GD.Print("NAME_PROMPT_SMOKE_PASS");
            GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PushError($"NAME_PROMPT_SMOKE_FAIL: {error}");
            GetTree().Quit(1);
        }
        finally
        {
            if (File.Exists(savePath)) File.Delete(savePath);
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        GD.Print($"PASS: {message}");
    }
}
