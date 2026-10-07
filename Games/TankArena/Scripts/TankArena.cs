using System;
using System.Linq;
using ChristiansSpilBox.Core;
using ChristiansSpilBox.Shared.Audio;
using ChristiansSpilBox.Shared.UI;
using Godot;

namespace ChristiansSpilBox.Games.TankArena;

public partial class TankArena : Node3D
{
    [Export] public int TargetKills = 10;
    [Export] public float PlayerShellSpeed = 17f;
    [Export] public float EnemyShellSpeed = 13f;

    private readonly Vector3[] _spawns =
    {
        new(-21, .75f, -18), new(21, .75f, -19), new(23, .75f, 17),
        new(-25, .75f, 17), new(0, .75f, -28), new(29, .75f, 0),
        new(-29, .75f, 0), new(0, .75f, 27), new(16, .75f, 29),
        new(-17, .75f, -28)
    };

    private ArenaProgress _progress = null!;
    private TankUnit _player = null!;
    private Camera3D _camera = null!;
    private Label _healthText = null!;
    private Label _reloadText = null!;
    private Label _progressText = null!;
    private Control _pauseOverlay = null!;
    private Control _resultOverlay = null!;
    private int _spawned;
    private bool _ended;
    private bool _victoryPending;
    private float _victorySecondsRemaining;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _progress = new ArenaProgress(TargetKills);
        BuildWorld();
        _player = CreateTank(Vector3.Up * .75f, true, false);
        _camera = new Camera3D { Current = true, Fov = 44f, Far = 160f };
        AddChild(_camera);
        _camera.GlobalPosition = _player.GlobalPosition + new Vector3(0, 25, 25);
        _camera.LookAt(_player.GlobalPosition);
        BuildHud();
        for (var i = 0; i < 3; i++) SpawnNormal();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_ended || GetTree().Paused) return;
        if (_victoryPending)
        {
            _victorySecondsRemaining -= (float)delta;
            if (_victorySecondsRemaining <= 0f) { EndGame(true); return; }
        }
        var throttle = (Input.IsKeyPressed(Key.W) ? 1f : 0f) - (Input.IsKeyPressed(Key.S) ? 1f : 0f);
        var turn = (Input.IsKeyPressed(Key.A) ? 1f : 0f) - (Input.IsKeyPressed(Key.D) ? 1f : 0f);
        _player.Drive(throttle, turn, delta);
        _player.AimAt(MousePoint(), delta);
        if (Input.IsMouseButtonPressed(MouseButton.Left)) _player.Weapon.TryFire();
        var weapon = _player.Weapon;
        _reloadText.Text = weapon.ReadyCount == weapon.Capacity
            ? $"SKUD  {weapon.ReadyCount}/{weapon.Capacity} ●"
            : $"SKUD  {weapon.ReadyCount}/{weapon.Capacity}  {Mathf.RoundToInt(weapon.ReloadFraction * 100)}%";
        var desiredCamera = _player.GlobalPosition + new Vector3(0, 25, 25);
        _camera.GlobalPosition = _camera.GlobalPosition.Lerp(desiredCamera, Mathf.Clamp((float)delta * 3.8f, 0, 1));
        _camera.LookAt(_player.GlobalPosition + new Vector3(0, .5f, 0));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape && !_ended)
        {
            if (_pauseOverlay.Visible) Resume(); else Pause();
            GetViewport().SetInputAsHandled();
        }
    }

    private Vector3 MousePoint()
    {
        var origin = _camera.ProjectRayOrigin(GetViewport().GetMousePosition());
        var direction = _camera.ProjectRayNormal(GetViewport().GetMousePosition());
        if (Mathf.Abs(direction.Y) < .001f) return _player.GlobalPosition - _player.GlobalBasis.Z * 10;
        var t = (.75f - origin.Y) / direction.Y;
        return t > 0 ? origin + direction * t : _player.GlobalPosition - _player.GlobalBasis.Z * 10;
    }

    private TankUnit CreateTank(Vector3 position, bool player, bool boss)
    {
        var tune = player ? new TankTuning { Health = 5, ShellSpeed = PlayerShellSpeed } : boss
            ? new TankTuning { Health = 7, ForwardSpeed = 6f, TurnSpeed = 1.3f, TurretSpeed = 2.5f,
                ReloadSeconds = 2.15f, ShellSpeed = EnemyShellSpeed + 1f, Scale = 1.55f }
            : new TankTuning { Health = 3, ForwardSpeed = 7.5f, TurnSpeed = 1.75f,
                TurretSpeed = 2.5f, ReloadSeconds = 3f, ShellSpeed = EnemyShellSpeed };
        var tank = new TankUnit { Position = position, ProcessMode = ProcessModeEnum.Pausable };
        tank.Configure(tune, player, boss);
        AddChild(tank);
        tank.Weapon.Fired += FireShell;
        tank.Destroyed += OnTankDestroyed;
        if (!player)
        {
            var brain = new EnemyBrain { Tank = tank, Target = _player, PreferredDistance = boss ? 17f : 13f };
            tank.AddChild(brain);
        }
        return tank;
    }

    private void FireShell(Vector3 position, Vector3 direction, TankUnit shooter, int volleyId, int barrelIndex)
    {
        var shell = new ShellProjectile { Direction = direction, Speed = shooter.Tuning.ShellSpeed,
            Damage = shooter.Tuning.ShellDamage, Shooter = shooter, ProcessMode = ProcessModeEnum.Pausable };
        AddChild(shell);
        shell.GlobalPosition = position;
        shooter.Weapon.Track(shell, volleyId);
        if (barrelIndex == 0)
            SoundEffects.Play(shooter.IsPlayer ? "player_fire" : shooter.IsBoss ? "boss_fire" : "enemy_fire",
                shooter.IsPlayer ? -8f : -13f);
    }

    private void OnTankDestroyed(TankUnit tank)
    {
        if (tank.IsPlayer) { EndGame(false); return; }
        var explosion = new TankExplosion { RadiusScale = tank.IsBoss ? 1.7f : 1f };
        AddChild(explosion);
        explosion.GlobalPosition = tank.GlobalPosition + Vector3.Up * (tank.IsBoss ? 1.1f : .55f);
        SoundEffects.Play(tank.IsBoss ? "boss_explosion" : "tank_explosion", tank.IsBoss ? -5f : -8f);
        if (tank.IsBoss)
        {
            _progress.RecordBossKill();
            foreach (var shell in GetChildren().OfType<ShellProjectile>())
                if (!shell.Shooter.IsPlayer) shell.QueueFree();
            _progressText.Text = "BOSS BESEJRET!";
            _victoryPending = true;
            _victorySecondsRemaining = 5f;
            return;
        }
        var bossReady = _progress.RecordNormalKill();
        _progressText.Text = $"TANKE  {_progress.Kills} / {_progress.TargetKills}";
        if (bossReady)
        {
            _progressText.Text = "BOSS TANK!";
            CallDeferred(nameof(SpawnBoss));
        }
        else if (_spawned < TargetKills) CallDeferred(nameof(SpawnNormal));
    }

    private void SpawnNormal()
    {
        if (_spawned >= TargetKills || _ended) return;
        var position = _spawns[_spawned % _spawns.Length];
        _spawned++;
        CreateTank(position, false, false);
    }

    private void SpawnBoss()
    {
        if (_ended) return;
        CreateTank(new Vector3(0, 1.15f, -29), false, true);
        SoundEffects.Play("boss_arrive", -7f);
    }

    private void Pause()
    {
        SoundEffects.Play("pause", -12f);
        _pauseOverlay.Visible = true;
        GetTree().Paused = true;
    }

    private void Resume()
    {
        SoundEffects.Play("resume", -12f);
        GetTree().Paused = false;
        _pauseOverlay.Visible = false;
    }

    private void Restart()
    {
        SoundEffects.Play("ui_select", -11f);
        GetTree().Paused = false;
        GetTree().ChangeSceneToFile("res://Games/TankArena/Scenes/TankArena.tscn");
    }

    private void ReturnToBox()
    {
        SoundEffects.Play("ui_back", -11f);
        GetTree().Paused = false;
        GetTree().ChangeSceneToFile("res://Core/MainMenu.tscn");
    }

    private void EndGame(bool won)
    {
        if (_ended) return;
        _ended = true;
        SoundEffects.Play(won ? "victory" : "defeat", -6f);
        if (won)
        {
            var path = ProjectSettings.GlobalizePath("user://game_box_state.json");
            var state = GameBoxState.Load(path);
            state.TankArenaCompleted = true;
            try { state.Save(path); } catch (Exception error) { GD.PushWarning($"Could not save completion: {error.Message}"); }
        }
        _resultOverlay.GetNode<Label>("Center/Dialog/Content/Title").Text = won ? "Du vandt!" : "Prøv igen!";
        _resultOverlay.Visible = true;
        GetTree().Paused = true;
    }

    private void BuildHud()
    {
        var canvas = new CanvasLayer();
        AddChild(canvas);
        var hud = new MarginContainer { AnchorRight = 1f, OffsetLeft = 30, OffsetTop = 24, OffsetRight = -30,
            MouseFilter = Control.MouseFilterEnum.Ignore };
        hud.AddThemeConstantOverride("margin_left", 12);
        hud.AddThemeConstantOverride("margin_right", 12);
        canvas.AddChild(hud);
        var hudPanel = new PanelContainer();
        hudPanel.AddThemeStyleboxOverride("panel", UiStyle.Box(new Color(0.05f, .1f, .16f, .84f), 13));
        hud.AddChild(hudPanel);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 34);
        hudPanel.AddChild(row);
        _healthText = UiStyle.Label("", 31, UiStyle.Gold);
        _reloadText = UiStyle.Label("", 31, UiStyle.Sky);
        _progressText = UiStyle.Label($"TANKE  0 / {TargetKills}", 31);
        _progressText.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(_healthText);
        row.AddChild(_reloadText);
        row.AddChild(_progressText);
        _healthText.Text = $"LIV  {new string('♥', _player.Health.Current)}";
        _player.Damaged += health => _healthText.Text = $"LIV  {new string('♥', health)}";

        var controlsPanel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        controlsPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
        controlsPanel.OffsetBottom = -10;
        controlsPanel.OffsetTop = -55;
        controlsPanel.OffsetLeft = 220;
        controlsPanel.OffsetRight = -220;
        controlsPanel.AddThemeStyleboxOverride("panel", UiStyle.Box(new Color(0.05f, .1f, .16f, .84f), 12));
        controlsPanel.AddChild(UiStyle.Label("W/S KØR   A/D DREJ   MUS SIGT   KLIK SKYD   ESC PAUSE", 18));
        canvas.AddChild(controlsPanel);

        _pauseOverlay = MakeOverlay(canvas, "Pause", true);
        _resultOverlay = MakeOverlay(canvas, "", false);
        _resultOverlay.Name = "ResultOverlay";
    }

    private Control MakeOverlay(CanvasLayer canvas, string title, bool pause)
    {
        var overlay = new Control { ProcessMode = ProcessModeEnum.Always, Visible = false };
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        canvas.AddChild(overlay);
        overlay.AddChild(UiStyle.Backdrop(new Color(0.04f, 0.08f, 0.14f, .88f)));
        var center = new CenterContainer { Name = "Center" };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(center);
        var dialog = new PanelContainer { Name = "Dialog", CustomMinimumSize = new Vector2(550, 360) };
        dialog.AddThemeStyleboxOverride("panel", UiStyle.Box(UiStyle.Panel, 24));
        center.AddChild(dialog);
        var content = new VBoxContainer { Name = "Content", Alignment = BoxContainer.AlignmentMode.Center };
        content.AddThemeConstantOverride("separation", 22);
        dialog.AddChild(content);
        var heading = UiStyle.Label(title, 55, UiStyle.Gold);
        heading.Name = "Title";
        content.AddChild(heading);
        if (pause)
        {
            var resume = UiStyle.Button("Fortsæt");
            SoundEffects.BindHover(resume);
            resume.Pressed += Resume;
            content.AddChild(resume);
        }
        var again = UiStyle.Button("Spil igen");
        SoundEffects.BindHover(again);
        again.Pressed += Restart;
        content.AddChild(again);
        var back = UiStyle.Button("Tilbage til Spil Box", 25);
        SoundEffects.BindHover(back);
        back.Pressed += ReturnToBox;
        content.AddChild(back);
        return overlay;
    }

    private void BuildWorld()
    {
        var environment = new WorldEnvironment { Environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("#95bfd1"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color("#e8eff0"),
            AmbientLightEnergy = .35f
        } };
        AddChild(environment);
        AddChild(new DirectionalLight3D { Rotation = new Vector3(-.85f, -.65f, 0), LightEnergy = .85f,
            ShadowEnabled = true });
        Block(new Vector3(0, -.35f, 0), new Vector3(78, .7f, 78), new Color("#7baf77"));
        Road(new Vector3(0, .025f, 0), new Vector3(7, .05f, 76));
        Road(new Vector3(0, .032f, 0), new Vector3(75, .05f, 5));
        Block(new Vector3(-39, 1.5f, 0), new Vector3(1, 3, 78), new Color("#777e79"));
        Block(new Vector3(39, 1.5f, 0), new Vector3(1, 3, 78), new Color("#777e79"));
        Block(new Vector3(0, 1.5f, -39), new Vector3(78, 3, 1), new Color("#777e79"));
        Block(new Vector3(0, 1.5f, 39), new Vector3(78, 3, 1), new Color("#777e79"));
        // Low village roofs keep the tank visible from the fixed camera.
        foreach (var p in new[] { new Vector3(-14, 1.35f, -18), new Vector3(-23, 1.35f, -8), new Vector3(-14, 1.35f, -7) })
            Block(p, new Vector3(5, 2.7f, 5), new Color("#d6bc91"));
        foreach (var p in new[] { new Vector3(13, 0, -17), new Vector3(19, 0, -10), new Vector3(27, 0, -23), new Vector3(12, 0, -27), new Vector3(28, 0, -9) })
            Tree(p);
        foreach (var p in new[] { new Vector3(-18, .7f, 16), new Vector3(-26, .7f, 26), new Vector3(19, .7f, 11), new Vector3(27, .7f, 24) })
            Block(p, new Vector3(3.5f, 1.4f, 2.8f), new Color("#8d9892"));
        Block(new Vector3(-12, .85f, 9), new Vector3(5, 1.7f, 1.5f), new Color("#9c8a71"));
        Block(new Vector3(13, .85f, 22), new Vector3(1.5f, 1.7f, 6), new Color("#9c8a71"));
    }

    private void Road(Vector3 pos, Vector3 size)
    {
        AddChild(new MeshInstance3D { Position = pos, Mesh = new BoxMesh { Size = size },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#726f65") } });
    }

    private void Block(Vector3 pos, Vector3 size, Color color)
    {
        var block = new StaticBody3D { Position = pos, CollisionLayer = 1, CollisionMask = 0 };
        AddChild(block);
        block.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        block.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 1f } });
    }

    private void Tree(Vector3 pos)
    {
        var tree = new StaticBody3D { Position = pos, CollisionLayer = 1, CollisionMask = 0 };
        AddChild(tree);
        tree.AddChild(new CollisionShape3D { Position = new Vector3(0, 1.3f, 0), Shape = new CylinderShape3D { Radius = .8f, Height = 2.6f } });
        tree.AddChild(new MeshInstance3D { Position = new Vector3(0, 1.1f, 0), Mesh = new CylinderMesh { TopRadius = .25f, BottomRadius = .35f, Height = 2.2f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#68513b") } });
        tree.AddChild(new MeshInstance3D { Position = new Vector3(0, 2.65f, 0), Mesh = new CylinderMesh { TopRadius = .1f, BottomRadius = 1.7f, Height = 3.2f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#368265") } });
    }
}
