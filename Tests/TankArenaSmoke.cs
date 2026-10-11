using System;
using System.Collections.Generic;
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
            arena.PowerUpDropChance = 1f;
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

            arena._Input(new InputEventMouseButton { ButtonIndex = MouseButton.WheelDown, Pressed = true });
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Check(TankAudio.ZoomAttenuationDb < 0f, "zooming out lowers tank sound volume");
            arena._Input(new InputEventMouseButton { ButtonIndex = MouseButton.WheelUp, Pressed = true });
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Check(TankAudio.ZoomAttenuationDb == 0f, "zooming in restores tank sound volume");

            var player = arena.GetChildren().OfType<TankUnit>().Single(x => x.IsPlayer);
            var hull = player.GetChildren().OfType<MeshInstance3D>()
                .Select(mesh => mesh.Mesh).OfType<ArrayMesh>().Single();
            var vertices = hull.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var edges = new Dictionary<(Vector3 From, Vector3 To), int>();
            var facesOutward = vertices.Length > 0 && vertices.Length % 3 == 0;
            for (var i = 0; i < vertices.Length; i += 3)
            {
                var a = vertices[i];
                var b = vertices[i + 1];
                var c = vertices[i + 2];
                var godotFrontNormal = (c - a).Cross(b - a);
                facesOutward &= godotFrontNormal.Dot((a + b + c) / 3f) > 0f;
                CountEdge(a, b);
                CountEdge(b, c);
                CountEdge(c, a);
            }
            Check(facesOutward && edges.Values.All(count => count == 2),
                "angled tank hull is closed and visible from outside");

            void CountEdge(Vector3 from, Vector3 to)
            {
                if (edges.TryGetValue((to, from), out var count)) edges[(to, from)] = count + 1;
                else edges[(from, to)] = 1;
            }

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

            player.TakeDamage(2);
            var healthPickup = new PowerUpPickup
            {
                Kind = PowerUpKind.Health, Player = player, TryCollect = arena.TryCollectPowerUp,
                Position = new Vector3(player.GlobalPosition.X, 2.3f, player.GlobalPosition.Z)
            };
            arena.AddChild(healthPickup);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Check(player.Health.Current == 5, "health pickup restores two points on contact");

            target.TakeDamage(2);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(arena.GetChildren().OfType<TankExplosion>().Any(), "normal tank creates a 3D explosion");
            Check(target.GetChildren().OfType<TankSmoke>().Single().ActivePuffCount > 0,
                "normal tank wreck emits smoke");
            Check(arena.GetChildren().OfType<PowerUpPickup>().Any(), "destroyed tank drops a power-up when chance succeeds");

            var missileTarget = arena.GetChildren().OfType<TankUnit>()
                .First(x => !x.IsPlayer && !x.IsDestroyed);
            missileTarget.GetChildren().OfType<EnemyBrain>().Single().ProcessMode = ProcessModeEnum.Disabled;
            missileTarget.GlobalPosition = new Vector3(4, .75f, -6);
            var missilePickup = new PowerUpPickup
            {
                Kind = PowerUpKind.Missile, Player = player, TryCollect = arena.TryCollectPowerUp,
                Position = new Vector3(player.GlobalPosition.X, 2.3f, player.GlobalPosition.Z)
            };
            arena.AddChild(missilePickup);
            for (var frame = 0; frame < 4 && !arena.GetChildren().OfType<HomingMissile>().Any(); frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            var missile = arena.GetChildren().OfType<HomingMissile>().Single();
            Check(missile.Target == missileTarget && missile.Damage == 3,
                "missile pickup targets the nearest enemy with three damage");
            for (var frame = 0; frame < 80 && !missileTarget.IsDestroyed; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Check(missileTarget.IsDestroyed, "heat-seeking missile destroys a three-health enemy");

            for (var i = 2; i < 10; i++)
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
            lossArena.PowerUpDropChance = 1f;
            AddChild(lossArena);
            var lossPlayer = lossArena.GetChildren().OfType<TankUnit>().Single(x => x.IsPlayer);
            lossPlayer.TakeDamage(99);
            Check(GetTree().Paused, "defeat pauses arena");
            Check(lossArena.GetChildren().OfType<PowerUpPickup>().Any(), "player death can also drop a power-up");
            var playerSmoke = lossPlayer.GetChildren().OfType<TankSmoke>().Single();
            var initialPuffs = playerSmoke.ActivePuffCount;
            var defeat = lossArena.GetChildren().OfType<CanvasLayer>().Single().FindChildren("Title", "Label", true, false)
                .OfType<Label>().Any(x => x.Text == "Prøv igen!");
            Check(defeat, "defeat screen displays Danish title");
            await ToSignal(GetTree().CreateTimer(.4), SceneTreeTimer.SignalName.Timeout);
            Check(playerSmoke.ActivePuffCount > initialPuffs, "player wreck keeps smoking on defeat screen");
            await ToSignal(GetTree().CreateTimer(1.2), SceneTreeTimer.SignalName.Timeout);

            GetTree().Paused = false;
            lossArena.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var minigunArena = GD.Load<PackedScene>("res://Games/TankArena/Scenes/TankArena.tscn").Instantiate<TankArena>();
            minigunArena.PowerUpDropChance = 0f;
            AddChild(minigunArena);
            var minigunPlayer = minigunArena.GetChildren().OfType<TankUnit>().Single(x => x.IsPlayer);
            minigunArena.AddChild(new PowerUpPickup
            {
                Kind = PowerUpKind.Minigun, Player = minigunPlayer,
                TryCollect = minigunArena.TryCollectPowerUp,
                Position = new Vector3(minigunPlayer.GlobalPosition.X, 2.3f, minigunPlayer.GlobalPosition.Z)
            });
            for (var frame = 0; frame < 4 && !minigunPlayer.Weapon.IsMinigunActive; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Check(minigunPlayer.Weapon.IsMinigunActive && minigunPlayer.Weapon.Capacity == 50,
                "minigun pickup enables fifty-shot mode");
            Check(minigunPlayer.Weapon.MinigunSecondsRemaining > 14f,
                "minigun pickup starts a fifteen-second timer");

            var minigunVictim = minigunArena.GetChildren().OfType<TankUnit>().First(x => !x.IsPlayer);
            minigunVictim.GetChildren().OfType<EnemyBrain>().Single().ProcessMode = ProcessModeEnum.Disabled;
            minigunVictim.GlobalPosition = new Vector3(6, .75f, -10);
            for (var shot = 0; shot < 5; shot++)
            {
                var pellet = new ShellProjectile
                {
                    Shooter = minigunPlayer, Direction = Vector3.Forward,
                    Speed = 20f, MinigunShot = true
                };
                minigunArena.AddChild(pellet);
                pellet.GlobalPosition = new Vector3(6, 1.25f, -5);
            }
            for (var frame = 0; frame < 25; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Check(minigunVictim.Health.Current == 2,
                "five minigun projectiles cause one point of damage");

            minigunPlayer.Turret.GlobalRotation = Vector3.Zero;
            var firedFifty = true;
            for (var shot = 0; shot < 50; shot++)
            {
                firedFifty &= minigunPlayer.Weapon.TryFire();
                minigunPlayer.Weapon._PhysicsProcess(.025);
            }
            Check(firedFifty && minigunPlayer.Weapon.InFlightCount == 50 &&
                minigunArena.GetChildren().OfType<ShellProjectile>().Count(x => x.MinigunShot) >= 50,
                "fifty minigun shots can coexist in the arena");
            Check(!minigunPlayer.Weapon.TryFire(), "fifty-first minigun shot is blocked");
            minigunPlayer.Weapon._PhysicsProcess(15);
            Check(!minigunPlayer.Weapon.IsMinigunActive && minigunPlayer.Weapon.Capacity == 3,
                "normal three-shot mode returns after fifteen seconds");
            Check(minigunPlayer.Weapon.TryFire(), "normal shells can fire after minigun expires");
            minigunArena.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            var mineArena = GD.Load<PackedScene>("res://Games/TankArena/Scenes/TankArena.tscn").Instantiate<TankArena>();
            mineArena.PowerUpDropChance = 0f;
            AddChild(mineArena);
            var minePlayer = mineArena.GetChildren().OfType<TankUnit>().Single(x => x.IsPlayer);
            mineArena.AddChild(new PowerUpPickup { Kind = PowerUpKind.Mine, Player = minePlayer,
                TryCollect = mineArena.TryCollectPowerUp, Position = new Vector3(0, 2.3f, 0) });
            for (var frame = 0; frame < 4 && minePlayer.Weapon.MinesRemaining == 0; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Check(minePlayer.Weapon.MinesRemaining == 10 && minePlayer.Weapon.MineSecondsRemaining > 14f,
                "mine pickup grants ten mines for fifteen seconds");
            mineArena._Input(new InputEventKey { Keycode = Key.M, Pressed = true });
            var placedMine = mineArena.GetChildren().OfType<TankMine>().Single();
            Check(minePlayer.Weapon.MinesRemaining == 9, "M places one mine");
            var mineVictim = mineArena.GetChildren().OfType<TankUnit>().First(x => !x.IsPlayer);
            mineVictim.GetChildren().OfType<EnemyBrain>().Single().ProcessMode = ProcessModeEnum.Disabled;
            mineVictim.GlobalPosition = new Vector3(placedMine.GlobalPosition.X, .75f, placedMine.GlobalPosition.Z);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Check(mineVictim.IsDestroyed, "enemy driving over a mine takes three damage");
            mineArena._Input(new InputEventKey { Keycode = Key.M, Pressed = true });
            var playerMine = mineArena.GetChildren().OfType<TankMine>().Single();
            minePlayer.GlobalPosition = new Vector3(playerMine.GlobalPosition.X, .75f, playerMine.GlobalPosition.Z);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Check(minePlayer.Health.Current == 2, "player driving over a mine takes three damage");
            mineArena._Input(new InputEventKey { Keycode = Key.M, Pressed = true });
            Check(mineArena.GetChildren().OfType<TankMine>().Count() == 1, "a third mine remains on the ground");
            minePlayer.Weapon._PhysicsProcess(15);
            Check(minePlayer.Weapon.MinesRemaining == 0 && !minePlayer.Weapon.TryPlaceMine() &&
                mineArena.GetChildren().OfType<TankMine>().Count() == 1,
                "mine ability expires while placed mines remain");
            mineArena.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            var nukeArena = GD.Load<PackedScene>("res://Games/TankArena/Scenes/TankArena.tscn").Instantiate<TankArena>();
            nukeArena.PowerUpDropChance = 0f;
            AddChild(nukeArena);
            var nukePlayer = nukeArena.GetChildren().OfType<TankUnit>().Single(x => x.IsPlayer);
            foreach (var enemy in nukeArena.GetChildren().OfType<TankUnit>().Where(x => !x.IsPlayer))
                enemy.GetChildren().OfType<EnemyBrain>().Single().ProcessMode = ProcessModeEnum.Disabled;
            var nukeTargets = nukeArena.GetChildren().OfType<TankUnit>().Where(x => !x.IsPlayer).ToArray();
            nukeTargets[0].GlobalPosition = new Vector3(0, .75f, -8);
            nukeTargets[1].GlobalPosition = new Vector3(4, .75f, -12);
            nukeTargets[2].GlobalPosition = new Vector3(20, .75f, 20);
            nukeArena.AddChild(new PowerUpPickup { Kind = PowerUpKind.Nuke, Player = nukePlayer,
                TryCollect = nukeArena.TryCollectPowerUp, Position = new Vector3(0, 2.3f, 0) });
            for (var frame = 0; frame < 4 && nukePlayer.Weapon.NukesRemaining == 0; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Check(nukePlayer.Weapon.NukesRemaining == 2, "nuclear pickup grants two shots");
            nukePlayer.Turret.GlobalRotation = Vector3.Zero;
            nukeArena._Input(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true });
            var nuke = nukeArena.GetChildren().OfType<NukeProjectile>().Single();
            Check(nukePlayer.Weapon.NukesRemaining == 1 && nuke.Speed == nukeArena.PlayerShellSpeed * .5f,
                "right click launches one nuke at half shell speed");
            for (var frame = 0; frame < 90 && !nukeTargets[0].IsDestroyed; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Check(nukeTargets[0].IsDestroyed && nukeTargets[1].IsDestroyed && !nukeTargets[2].IsDestroyed,
                "nuclear blast destroys nearby tanks but spares distant tanks");
            Check(nukePlayer.IsDestroyed && nukeArena.GetChildren().OfType<NuclearMushroomCloud>().Any(),
                "nuclear blast destroys player and creates a mushroom cloud");
            var nukeResult = nukeArena.GetChildren().OfType<CanvasLayer>().Single().GetNode<Control>("ResultOverlay");
            Check(!nukeResult.Visible, "mushroom cloud is visible before defeat overlay");
            await ToSignal(GetTree().CreateTimer(3.2), SceneTreeTimer.SignalName.Timeout);
            Check(nukeResult.Visible && GetTree().Paused, "nuclear self-hit ends in defeat after cloud appears");
            GetTree().Paused = false;
            nukeArena.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
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
