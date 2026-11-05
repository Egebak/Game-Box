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
            duel.ReturnToMenu();
            duel.StartMatch(true);
            duel._PhysicsProcess(1.3); // Opening shields wear off.
            duel.PlaceShipForTest(0, new Vector2(300f, 300f), new Vector2(120f, 0f));
            duel.PlaceShipForTest(1, new Vector2(330f, 300f), new Vector2(-120f, 0f));
            duel._PhysicsProcess(1f / 60f);
            Check(duel.ShipVelocity(0).X < 0f && duel.ShipVelocity(1).X > 0f &&
                duel.ShipSpin(0) * duel.ShipSpin(1) < 0f && duel.ActiveBursts > 0,
                "colliding ships burst, recoil, and spin in opposite directions");
            Check(duel.ShipHealth(0) == SpaceRules.ShipHealth &&
                duel.ShipHealth(1) == SpaceRules.ShipHealth,
                "ship collision creates an impact without weapon damage");

            duel.ReturnToMenu();
            duel.StartMatch(true);
            duel._PhysicsProcess(1.3);
            duel.PlaceShipForTest(0, new Vector2(200f, 300f), Vector2.Zero);
            duel.PlaceShipForTest(1, new Vector2(300f, 300f), Vector2.Zero);
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Space, Pressed = true });
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.KpAdd, Pressed = true });
            for (var i = 0; i < 5 &&
                (duel.LasersFired(0) == 0 || duel.TorpedoesFired(1) == 0); i++)
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Space, Pressed = false });
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.KpAdd, Pressed = false });
            for (var i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Check(duel.LasersFired(0) > 0 && duel.TorpedoesFired(1) > 0 &&
                duel.ActiveLasers == 0 && duel.ActiveTorpedoes == 0 && duel.ActiveBursts > 0,
                "laser intercepts and explodes an opposing torpedo");
            Check(duel.ShipHealth(0) == SpaceRules.ShipHealth &&
                duel.ShipHealth(1) == SpaceRules.ShipHealth,
                "laser torpedo interception does not damage either ship");

            duel.ReturnToMenu();
            duel.StartMatch(true);
            duel.PlaceShipForTest(0, new Vector2(200f, 300f), Vector2.Zero);
            duel.PlaceShipForTest(1, new Vector2(800f, 700f), Vector2.Zero);
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Space, Pressed = true });
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Shift, Pressed = true });
            for (var i = 0; i < 5 &&
                (duel.LasersFired(0) == 0 || duel.TorpedoesFired(0) == 0); i++)
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Space, Pressed = false });
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Shift, Pressed = false });
            for (var i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Check(duel.LasersFired(0) > 0 && duel.TorpedoesFired(0) > 0 &&
                duel.ActiveLasers == 0 && duel.ActiveTorpedoes == 0 && duel.ActiveBursts > 0,
                "laser can also detonate its owner's torpedo");

            duel.ReturnToMenu();
            duel.StartMatch(true);
            duel._PhysicsProcess(1.3);
            duel.PlaceShipForTest(0, new Vector2(200f, 300f), Vector2.Zero);
            duel.PlaceShipForTest(1, new Vector2(800f, 700f), Vector2.Zero);
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Space, Pressed = true });
            for (var i = 0; i < 4 && duel.LasersFired(0) == 0; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Space, Pressed = false });
            duel.PlaceShipForTest(0, new Vector2(260f, 300f), Vector2.Zero);
            for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Check(duel.ShipHealth(0) == SpaceRules.ShipHealth - SpaceRules.LaserDamage,
                "launched laser can hit its owner");

            duel.ReturnToMenu();
            duel.StartMatch(true);
            duel._PhysicsProcess(1.3);
            duel.PlaceShipForTest(0, new Vector2(200f, 300f), Vector2.Zero);
            duel.PlaceShipForTest(1, new Vector2(300f, 300f), Vector2.Zero);
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Space, Pressed = true });
            for (var i = 0; i < 4 && duel.LasersFired(0) == 0; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Space, Pressed = false });
            for (var i = 0; i < 8; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Check(duel.ShipHealth(1) == SpaceRules.ShipHealth - SpaceRules.LaserDamage,
                "launched laser still hits the opposing ship");

            duel.ReturnToMenu();
            duel.StartMatch(true);
            duel._PhysicsProcess(1.3);
            duel.PlaceShipForTest(0, new Vector2(200f, 300f), Vector2.Zero);
            duel.PlaceShipForTest(1, new Vector2(800f, 700f), Vector2.Zero);
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Shift, Pressed = true });
            for (var i = 0; i < 4 && duel.TorpedoesFired(0) == 0; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Shift, Pressed = false });
            duel.PlaceShipForTest(0, new Vector2(260f, 300f), Vector2.Zero);
            for (var i = 0; i < 10; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Check(duel.ShipHealth(0) == SpaceRules.ShipHealth - SpaceRules.TorpedoDamage,
                "launched torpedo can hit its owner");
            duel.DamageShip(0, SpaceRules.ShipHealth, 0);
            Check(duel.Kills(1) == 1 && duel.Kills(0) == 0,
                "self-destruction awards the point to the opposing ship");

            duel.ReturnToMenu();
            duel.StartMatch(true);
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
