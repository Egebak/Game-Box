using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameBox.Core;
using GameBox.Games.WordMission;
using GameBox.Games.WordMission.Learning;
using Godot;

public partial class WordMissionSmoke : Node
{
    public override void _Ready() => CallDeferred(nameof(Run));

    private async void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "game-box-word-smoke", Guid.NewGuid().ToString("N"));
        var savePath = Path.Combine(directory, "state.json");
        try
        {
            var scene = GD.Load<PackedScene>("res://Games/WordMission/Scenes/WordMission.tscn");
            var game = scene.Instantiate<WordMission>();
            game.SavePathOverride = savePath;
            AddChild(game);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(game.FindChildren("*", "Button", true, false).OfType<Button>()
                .Any(button => button.Text == "Start mission"), "Ordmission start menu opens");
            game.StartSession(42);
            var seen = new HashSet<ActivityKind>();
            for (var step = 0; step < game.Content.SessionLength; step++)
            {
                var view = game.CurrentActivityView ?? throw new Exception("Activity view missing");
                var challenge = view.Challenge;
                seen.Add(challenge.Activity);
                if (step == 0)
                {
                    var wrong = challenge.Choices.Where(id => id != challenge.ItemId).Take(2).ToArray();
                    view.SubmitChoice(wrong[0]);
                    view.SubmitChoice(wrong[0]);
                    view.SubmitChoice(wrong[1]);
                    Check(view.WrongAttempts == 3, "three mistakes trigger assisted completion");
                }
                else if (challenge.Activity is ActivityKind.BuildWord or ActivityKind.WordGate)
                {
                    foreach (var id in game.Content.Word(challenge.ItemId).LetterIds)
                        view.SubmitChoice(id);
                }
                else view.SubmitChoice(challenge.ItemId);

                for (var frame = 0; frame < 300 && game.SessionActive && game.ChallengeIndex == step; frame++)
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                Check(!game.SessionActive || game.ChallengeIndex > step, $"challenge {step + 1} advances");
            }
            Check(seen.Count == 5, "full session visits all five activities");
            Check(!game.SessionActive && game.CompletedSessions == 1, "session completes");
            var saved = GameBoxState.Load(savePath);
            Check(saved.WordMission.CompletedSessions == 1 && saved.WordMission.AssistedAnswers >= 1,
                "session and assisted learning persist locally");
            game.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var reopened = scene.Instantiate<WordMission>();
            reopened.SavePathOverride = savePath;
            AddChild(reopened);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(reopened.CompletedSessions == 1, "progress remains after reopening Ordmission");
            reopened.QueueFree();
            await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
            GD.Print("WORD_MISSION_SMOKE_PASS");
            GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PushError($"WORD_MISSION_SMOKE_FAIL: {error}");
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
