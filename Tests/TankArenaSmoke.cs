using System;
using System.IO;
using System.Linq;
using GameBox.Games.TankArena;
using GameBox.Shared.Audio;
using Godot;

public partial class TankArenaSmoke : Node
{
    public override void _Ready() => CallDeferred(nameof(Run));

    private async void Run()
    {
        var savePath = ProjectSettings.GlobalizePath("user://game_box_state.json");
        var previousSave = File.Exists(savePath) ? File.ReadAllText(savePath) : null;
        try
        {
            ProcessMode = ProcessModeEnum.Always;
            var arena = GD.Load<PackedScene>("res://Games/TankArena/Scenes/TankArena.tscn").Instantiate<TankArena>();
            AddChild(arena);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(SoundEffects.AllIds.All(id => SoundEffects.Get(id) is not null), "all sound effects load");
            Check(arena.GetChildren().OfType<TankUnit>().Count(x => !x.IsPlayer) == 3, "three enemies spawn");

            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = true });
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(GetTree().Paused, "Esc pauses arena");
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = true });
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(!GetTree().Paused, "Esc resumes arena");

            var player = arena.GetChildren().OfType<TankUnit>().Single(x => x.IsPlayer);
            for (var shot = 0; shot < 3; shot++)
            {
                player.Turret.GlobalRotation = Vector3.Zero;
                Check(player.Weapon.TryFire(), $"player fires shell {shot + 1}");
                if (shot < 2)
                    for (var frame = 0; frame < 24; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            Check(player.Weapon.InFlightCount == 3 && arena.GetChildren().OfType<ShellProjectile>().Count() >= 3,
                "three player shells coexist in the arena");
            Check(!player.Weapon.TryFire(), "fourth player shell is blocked");

            var target = arena.GetChildren().OfType<TankUnit>().First(x => !x.IsPlayer);
            target.GetChildren().OfType<EnemyBrain>().Single().ProcessMode = ProcessModeEnum.Disabled;
            target.GlobalPosition = new Vector3(6, .75f, -10);
            var shell = new ShellProjectile { Shooter = player, Direction = Vector3.Forward, Speed = 17f, Damage = 1 };
            arena.AddChild(shell);
            shell.GlobalPosition = new Vector3(6, 1.25f, -5);
            for (var i = 0; i < 35; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Check(target.Health.Current == 2, "shell hits and damages enemy");

            target.TakeDamage(2);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(arena.GetChildren().OfType<TankExplosion>().Any(), "normal tank creates a 3D explosion");
            Check(target.GetChildren().OfType<TankSmoke>().Single().ActivePuffCount > 0,
                "normal tank wreck emits smoke");
            for (var i = 1; i < 10; i++)
            {
                var victim = arena.GetChildren().OfType<TankUnit>().First(x => !x.IsPlayer && !x.IsBoss && !x.IsDestroyed);
                victim.TakeDamage(99);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            var boss = arena.GetChildren().OfType<TankUnit>().Single(x => x.IsBoss);
            Check(boss.Health.Maximum == 7, "boss appears after ten normal tanks");
            Check(boss.Weapon.BarrelCount == 2, "boss has two cannons");
            var shellCount = arena.GetChildren().OfType<ShellProjectile>().Count();
            Check(boss.Weapon.TryFire(), "boss fires a volley");
            Check(arena.GetChildren().OfType<ShellProjectile>().Count() == shellCount + 2,
                "boss volley launches two shells");
            Check(boss.Weapon.InFlightCount == 1, "boss volley uses one reload charge");
            boss.TakeDamage(99);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(arena.GetChildren().OfType<TankExplosion>().Any(x => x.RadiusScale > 1.5f),
                "boss creates a larger explosion");
            var smoke = boss.GetChildren().OfType<TankSmoke>().Single();
            var resultOverlay = arena.GetChildren().OfType<CanvasLayer>().Single().GetNode<Control>("ResultOverlay");
            Check(smoke.ActivePuffCount > 0, "boss wreck emits smoke");
            Check(!GetTree().Paused && !resultOverlay.Visible, "arena continues after boss defeat");
            await ToSignal(GetTree().CreateTimer(4.5), SceneTreeTimer.SignalName.Timeout);
            Check(boss.IsInsideTree() && smoke.ActivePuffCount > 0, "boss wreck keeps smoking during the delay");
            Check(!GetTree().Paused && !resultOverlay.Visible, "victory waits at least 4.5 seconds");
            await ToSignal(GetTree().CreateTimer(.8), SceneTreeTimer.SignalName.Timeout);
            Check(GetTree().Paused && resultOverlay.Visible, "victory screen appears after five seconds");
            var victory = arena.GetChildren().OfType<CanvasLayer>().Single().FindChildren("Title", "Label", true, false)
                .OfType<Label>().Any(x => x.Text == "Du vandt!");
            Check(victory, "victory screen displays Danish title");
            for (var i = 0; i < 5; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GetTree().Paused = false;
            arena.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var lossArena = GD.Load<PackedScene>("res://Games/TankArena/Scenes/TankArena.tscn").Instantiate<TankArena>();
            AddChild(lossArena);
            var lossPlayer = lossArena.GetChildren().OfType<TankUnit>().Single(x => x.IsPlayer);
            lossPlayer.TakeDamage(99);
            Check(GetTree().Paused, "defeat pauses arena");
            var playerSmoke = lossPlayer.GetChildren().OfType<TankSmoke>().Single();
            var initialPuffs = playerSmoke.ActivePuffCount;
            var defeat = lossArena.GetChildren().OfType<CanvasLayer>().Single().FindChildren("Title", "Label", true, false)
                .OfType<Label>().Any(x => x.Text == "Prøv igen!");
            Check(defeat, "defeat screen displays Danish title");
            await ToSignal(GetTree().CreateTimer(.4), SceneTreeTimer.SignalName.Timeout);
            Check(playerSmoke.ActivePuffCount > initialPuffs, "player wreck keeps smoking on defeat screen");
            await ToSignal(GetTree().CreateTimer(1.2), SceneTreeTimer.SignalName.Timeout);
            GD.Print("TANK_ARENA_SMOKE_PASS");
            GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PushError($"TANK_ARENA_SMOKE_FAIL: {error}");
            GetTree().Quit(1);
        }
        finally
        {
            if (previousSave is null) File.Delete(savePath);
            else File.WriteAllText(savePath, previousSave);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        GD.Print($"PASS: {message}");
    }
}
