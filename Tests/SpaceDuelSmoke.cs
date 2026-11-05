using System;
using System.Linq;
using GameBox.Games.SpaceDuel;
using Godot;

public partial class SpaceDuelSmoke : Node
{
    public override void _Ready() => CallDeferred(nameof(Run));

    private async void Run()
    {
        try
        {
            ProcessMode = ProcessModeEnum.Always;
            var duel = GD.Load<PackedScene>("res://Games/SpaceDuel/Scenes/SpaceDuel.tscn")
                .Instantiate<SpaceDuel>();
            AddChild(duel);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(duel.Phase == SpaceDuelPhase.Menu &&
                duel.FindChildren("*", "Button", true, false).OfType<Button>()
                    .Any(button => button.Text == "Solo mod computer"),
                "Space Duel opens its own solo and two-player menu");

            duel.StartMatch(true);
            Check(duel.Phase == SpaceDuelPhase.Playing && duel.TwoPlayerMode,
                "two-player match starts from the Space Duel menu");
            var start = duel.ShipPosition(0);
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.W, Pressed = true });
            for (var i = 0; i < 25; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.W, Pressed = false });
            Check(duel.ShipPosition(0).X > start.X && duel.ShipEnergy(0) < SpaceRules.MaximumEnergy,
                "W accelerates player one and spends energy");
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            var coastStart = duel.ShipPosition(0);
            var coastVelocity = duel.ShipVelocity(0);
            for (var i = 0; i < 15; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Check(duel.ShipPosition(0).X > coastStart.X &&
                duel.ShipVelocity(0).DistanceTo(coastVelocity) < 5f,
                "ship keeps coasting without acceleration input");

            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Up, Pressed = true });
            for (var i = 0; i < 25; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Up, Pressed = false });
            var secondSpeed = duel.ShipVelocity(1).Length();
            Check(secondSpeed > 0f && duel.ShipEnergy(1) < SpaceRules.MaximumEnergy,
                "up arrow accelerates player two and spends energy");
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Down, Pressed = true });
            for (var i = 0; i < 10; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Down, Pressed = false });
            Check(duel.ShipVelocity(1).Length() < secondSpeed,
                "down arrow brakes player two");
            var heading = duel.ShipAngle(1);
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Left, Pressed = true });
            for (var i = 0; i < 10; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Left, Pressed = false });
            Check(Mathf.Abs(Mathf.AngleDifference(heading, duel.ShipAngle(1))) > .1f,
                "side arrow rotates player two");

            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Enter, Pressed = true });
            for (var i = 0; i < 4 && duel.LasersFired(1) == 0; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Enter, Pressed = false });
            Check(duel.LasersFired(1) > 0, "Enter fires player two's laser");
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.KpAdd, Pressed = true });
            for (var i = 0; i < 4 && duel.TorpedoesFired(1) == 0; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.KpAdd, Pressed = false });
            Check(duel.TorpedoesFired(1) > 0, "numpad plus launches player two's torpedo");

            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Space, Pressed = true });
            for (var i = 0; i < 4 && duel.LasersFired(0) == 0; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Space, Pressed = false });
            Check(duel.LasersFired(0) > 0, "spacebar fires a laser");
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Shift, Pressed = true });
            for (var i = 0; i < 4 && duel.TorpedoesFired(0) == 0; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Shift, Pressed = false });
            Check(duel.TorpedoesFired(0) > 0, "shift launches a torpedo with exhaust");
            duel._Input(new InputEventKey { Keycode = Key.Escape, Pressed = true });
            Check(duel.GetNode<Control>("DuelUi/PauseOverlay").Visible,
                "Escape pauses the space match");
            duel._Input(new InputEventKey { Keycode = Key.Escape, Pressed = true });
            Check(!duel.GetNode<Control>("DuelUi/PauseOverlay").Visible,
                "Escape resumes the space match");

            duel.ReturnToMenu();
            duel.StartMatch(false);
            for (var i = 0; i < 65; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Check(!duel.TwoPlayerMode && duel.ShipVelocity(1).Length() > 0f,
                "solo match uses an active AI opponent");

            duel.ReturnToMenu();
            duel.StartMatch(true);
            duel._PhysicsProcess(1.3); // Opening shield wears off.
            for (var kill = 0; kill < 10; kill++)
            {
                duel.DamageShip(1, SpaceRules.ShipHealth, 0);
                if (kill == 9) break;
                duel._PhysicsProcess(1.2); // Respawn.
                duel._PhysicsProcess(1.2); // Spawn protection expires.
            }
            Check(duel.Kills(0) == 10 && duel.Winner == 0 &&
                duel.Phase == SpaceDuelPhase.Finishing,
                "ten kills start the delayed victory sequence");
            duel._PhysicsProcess(3.1);
            Check(duel.Phase == SpaceDuelPhase.Results &&
                duel.GetNode<Control>("DuelUi/VictoryOverlay").Visible,
                "victory screen shows after a short final sequence");
            duel._Input(new InputEventKey { Keycode = Key.Enter, Pressed = true });
            Check(duel.Phase == SpaceDuelPhase.Menu &&
                duel.GetNode<Control>("DuelUi/SpaceMenu").Visible,
                "Enter returns to the Space Duel menu");
            duel.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.Print("SPACE_DUEL_SMOKE_PASS");
            GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PushError($"SPACE_DUEL_SMOKE_FAIL: {error}");
            GetTree().Quit(1);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        GD.Print($"PASS: {message}");
    }
}
