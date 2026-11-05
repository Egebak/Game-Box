using System;
using System.Collections.Generic;
using GameBox.Shared.Audio;
using GameBox.Shared.UI;
using Godot;

namespace GameBox.Games.SpaceDuel;

public enum SpaceDuelPhase { Menu, Playing, Finishing, Results }

public partial class SpaceDuel : Node2D
{
    private sealed class Ship
    {
        public int Index;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Angle;
        public int Health = SpaceRules.ShipHealth;
        public readonly EnergyReserve Energy = new();
        public float LaserCooldown;
        public float TorpedoCooldown;
        public float RespawnSeconds;
        public float ShieldSeconds;
        public bool Destroyed;
        public bool Thrusting;
        public int LasersFired;
        public int LasersHit;
        public int TorpedoesFired;
        public int TorpedoesHit;
        public float MaximumSpeed;
    }

    private sealed class Laser
    {
        public int Owner;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Seconds = 1.5f;
        public bool Alive = true;
    }

    private sealed class Torpedo
    {
        public int Owner;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Angle;
        public float Fuel = SpaceRules.TorpedoFuelSeconds;
        public float Coast = SpaceRules.TorpedoCoastSeconds;
        public bool Alive = true;
        public readonly List<Vector2> Trail = new();
    }

    private sealed class Burst
    {
        public Vector2 Position;
        public Color Color;
        public float Radius;
        public float Age;
        public float Lifetime;
    }

    private readonly Random _random = new();
    private readonly List<Vector2> _stars = new();
    private readonly List<float> _starSizes = new();
    private readonly List<Laser> _lasers = new();
    private readonly List<Torpedo> _torpedoes = new();
    private readonly List<Burst> _bursts = new();
    private Ship[] _ships = Array.Empty<Ship>();
    private DuelScore _score = new();
    private Vector2 _arenaSize;
    private SpaceDuelPhase _phase = SpaceDuelPhase.Menu;
    private bool _twoPlayers;
    private bool _paused;
    private int _aiStyle;
    private float _finishSeconds;
    private float _clock;
    private Control _menu = null!;
    private Control _hud = null!;
    private Control _pause = null!;
    private Control _results = null!;
    private Label[] _scoreLabels = null!;
    private Label[] _statusLabels = null!;
    private ProgressBar[] _healthBars = null!;
    private ProgressBar[] _energyBars = null!;
    private Label _resultTitle = null!;
    private Label _resultStats = null!;

    public SpaceDuelPhase Phase => _phase;
    public bool TwoPlayerMode => _twoPlayers;
    public int Winner => _score.Winner;
    public int ActiveTorpedoes
    {
        get
        {
            var count = 0;
            foreach (var torpedo in _torpedoes) if (torpedo.Alive) count++;
            return count;
        }
    }
    public int ActiveLasers => _lasers.Count;
    public int Kills(int player) => _score.Kills(player);
    public Vector2 ShipPosition(int player) => _ships[player].Position;
    public Vector2 ShipVelocity(int player) => _ships[player].Velocity;
    public float ShipAngle(int player) => _ships[player].Angle;
    public int ShipHealth(int player) => _ships[player].Health;
    public float ShipEnergy(int player) => _ships[player].Energy.Current;
    public int LasersFired(int player) => _ships[player].LasersFired;
    public int TorpedoesFired(int player) => _ships[player].TorpedoesFired;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _arenaSize = GetViewportRect().Size;
        for (var i = 0; i < 150; i++)
        {
            _stars.Add(new Vector2((float)_random.NextDouble() * _arenaSize.X,
                (float)_random.NextDouble() * _arenaSize.Y));
            _starSizes.Add(.8f + (float)_random.NextDouble() * 2.1f);
        }
        BuildUi();
        ShowPhase();
    }

    public void StartMatch(bool twoPlayers)
    {
        _twoPlayers = twoPlayers;
        _aiStyle = _random.Next(3);
        _score = new DuelScore();
        _ships = new[] { NewShip(0), NewShip(1) };
        _lasers.Clear();
        _torpedoes.Clear();
        _bursts.Clear();
        _paused = false;
        _phase = SpaceDuelPhase.Playing;
        ShowPhase();
        UpdateHud();
        SoundEffects.Play("ui_select", -11f);
    }

    public void ReturnToMenu()
    {
        _phase = SpaceDuelPhase.Menu;
        _paused = false;
        _lasers.Clear();
        _torpedoes.Clear();
        _bursts.Clear();
        ShowPhase();
        SoundEffects.Play("ui_back", -12f);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;
        if (_phase == SpaceDuelPhase.Results && key.Keycode is Key.Enter or Key.KpEnter)
            ReturnToMenu();
        else if (_phase == SpaceDuelPhase.Menu && key.Keycode == Key.Escape)
            ReturnToGameBox();
        else if (_phase == SpaceDuelPhase.Playing && key.Keycode == Key.Escape)
        {
            _paused = !_paused;
            _pause.Visible = _paused;
            SoundEffects.Play(_paused ? "pause" : "resume", -12f);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        var step = (float)delta;
        _clock += step;
        for (var i = _bursts.Count - 1; i >= 0; i--)
        {
            _bursts[i].Age += step;
            if (_bursts[i].Age >= _bursts[i].Lifetime) _bursts.RemoveAt(i);
        }
        if (_phase == SpaceDuelPhase.Finishing)
        {
            foreach (var ship in _ships)
            {
                if (ship.Destroyed) continue;
                ship.Position = Wrap(ship.Position + ship.Velocity * step * .32f);
                ship.Velocity *= Mathf.Exp(-1.5f * step);
            }
            _finishSeconds -= step;
            if (_finishSeconds <= 0f)
            {
                _phase = SpaceDuelPhase.Results;
                ShowResults();
                ShowPhase();
            }
            return;
        }
        if (_phase != SpaceDuelPhase.Playing || _paused) return;

        foreach (var ship in _ships)
        {
            if (ship.Destroyed)
            {
                ship.RespawnSeconds -= step;
                if (ship.RespawnSeconds <= 0f) Respawn(ship);
                continue;
            }
            ship.Energy.Recharge(step);
            ship.LaserCooldown = Mathf.Max(0f, ship.LaserCooldown - step);
            ship.TorpedoCooldown = Mathf.Max(0f, ship.TorpedoCooldown - step);
            ship.ShieldSeconds = Mathf.Max(0f, ship.ShieldSeconds - step);
            var controls = ship.Index == 0 ? LeftControls() : _twoPlayers ? RightControls() : AiControls(ship);
            UpdateShip(ship, controls, step);
        }
        UpdateLasers(step);
        UpdateTorpedoes(step);
        UpdateHud();
    }

    public override void _Process(double delta) => QueueRedraw();

    private Ship NewShip(int index) => new()
    {
        Index = index,
        Position = new Vector2(_arenaSize.X * (index == 0 ? .24f : .76f), _arenaSize.Y * .5f),
        Angle = index == 0 ? 0f : Mathf.Pi,
        ShieldSeconds = 1.2f
    };

    private void Respawn(Ship ship)
    {
        var fresh = NewShip(ship.Index);
        ship.Position = fresh.Position;
        ship.Velocity = Vector2.Zero;
        ship.Angle = fresh.Angle;
        ship.Health = SpaceRules.ShipHealth;
        ship.Energy.Refill();
        ship.ShieldSeconds = 1.2f;
        ship.Destroyed = false;
        ship.Thrusting = false;
    }

    private readonly record struct Controls(float Thrust, float Brake, float Turn, bool Laser, bool Torpedo);

    private static Controls LeftControls() => new(
        Input.IsKeyPressed(Key.W) ? 1f : 0f,
        Input.IsKeyPressed(Key.S) ? 1f : 0f,
        (Input.IsKeyPressed(Key.D) ? 1f : 0f) - (Input.IsKeyPressed(Key.A) ? 1f : 0f),
        Input.IsKeyPressed(Key.Space), Input.IsKeyPressed(Key.Shift));

    private static Controls RightControls() => new(
        Input.IsKeyPressed(Key.Up) ? 1f : 0f,
        Input.IsKeyPressed(Key.Down) ? 1f : 0f,
        (Input.IsKeyPressed(Key.Right) ? 1f : 0f) - (Input.IsKeyPressed(Key.Left) ? 1f : 0f),
        Input.IsKeyPressed(Key.Enter) || Input.IsKeyPressed(Key.Kp0),
        Input.IsKeyPressed(Key.Plus) || Input.IsKeyPressed(Key.KpAdd));

    private Controls AiControls(Ship ship)
    {
        var target = _ships[0];
        if (target.Destroyed) return new Controls(1f, 0f, 0f, false, false);
        var offset = ShortestOffset(ship.Position, target.Position);
        var distance = offset.Length();
        var targetAngle = offset.Angle();
        if (_aiStyle == 1) targetAngle += .55f; // Circle the opponent.
        if (_aiStyle == 2 && distance < 300f) targetAngle += Mathf.Pi; // Keep range.
        var difference = Mathf.AngleDifference(ship.Angle, targetAngle);
        var turn = Mathf.Abs(difference) < .08f ? 0f : Mathf.Sign(difference);
        var aligned = Mathf.Abs(Mathf.AngleDifference(ship.Angle, offset.Angle()));
        var thrust = _aiStyle switch
        {
            0 => distance > 115f,
            1 => distance > 185f || ship.Velocity.Length() < 135f,
            _ => distance > 360f
        };
        var brake = _aiStyle == 2 && distance < 240f && ship.Velocity.Length() > 100f;
        return new Controls(thrust ? 1f : 0f, brake ? 1f : 0f, turn,
            aligned < .24f && distance < 850f,
            aligned < .32f && distance is > 160f and < 900f && ship.Energy.Current > 45f);
    }

    private void UpdateShip(Ship ship, Controls controls, float delta)
    {
        ship.Angle = Mathf.Wrap(ship.Angle + controls.Turn * SpaceRules.TurnSpeed * delta,
            -Mathf.Pi, Mathf.Pi);
        var forward = new Vector2(Mathf.Cos(ship.Angle), Mathf.Sin(ship.Angle));
        var thrustFraction = controls.Thrust > 0f
            ? ship.Energy.SpendContinuous(SpaceRules.ThrustEnergyPerSecond, delta) : 0f;
        ship.Thrusting = thrustFraction > 0f;
        ship.Velocity += forward * SpaceRules.ThrustAcceleration * thrustFraction * delta;
        if (controls.Brake > 0f && ship.Velocity.Length() > 0f)
        {
            var brakeFraction = ship.Energy.SpendContinuous(SpaceRules.BrakeEnergyPerSecond, delta);
            ship.Velocity = ship.Velocity.MoveToward(Vector2.Zero,
                SpaceRules.BrakeAcceleration * brakeFraction * delta);
        }
        ship.Velocity = ship.Velocity.LimitLength(SpaceRules.MaximumShipSpeed);
        ship.MaximumSpeed = Mathf.Max(ship.MaximumSpeed, ship.Velocity.Length());
        ship.Position = Wrap(ship.Position + ship.Velocity * delta);
        if (controls.Laser && ship.LaserCooldown <= 0f && ship.Energy.TrySpend(SpaceRules.LaserEnergyCost))
        {
            _lasers.Add(new Laser { Owner = ship.Index,
                Position = Wrap(ship.Position + forward * 30f),
                Velocity = ship.Velocity + forward * 760f });
            ship.LasersFired++;
            ship.LaserCooldown = .17f;
            SoundEffects.Play("player_fire", -18f, 1.9f);
        }
        if (controls.Torpedo && ship.TorpedoCooldown <= 0f && ship.Energy.TrySpend(SpaceRules.TorpedoEnergyCost))
        {
            _torpedoes.Add(new Torpedo { Owner = ship.Index,
                Position = Wrap(ship.Position + forward * 33f),
                Velocity = ship.Velocity + forward * 185f,
                Angle = ship.Angle });
            ship.TorpedoesFired++;
            ship.TorpedoCooldown = .8f;
            SoundEffects.Play("missile_launch", -9f);
        }
    }

    private void UpdateLasers(float delta)
    {
        foreach (var laser in _lasers)
        {
            if (!laser.Alive) continue;
            laser.Position = Wrap(laser.Position + laser.Velocity * delta);
            laser.Seconds -= delta;
            if (laser.Seconds <= 0f) { laser.Alive = false; continue; }
            var victim = _ships[1 - laser.Owner];
            if (victim.Destroyed || victim.ShieldSeconds > 0f ||
                ShortestOffset(laser.Position, victim.Position).LengthSquared() >
                Mathf.Pow(SpaceRules.ShipRadius + 5f, 2f)) continue;
            laser.Alive = false;
            _ships[laser.Owner].LasersHit++;
            AddBurst(laser.Position, new Color("#ff685e"), 22f, .32f);
            SoundEffects.Play("impact_metal", -17f, 1.5f);
            DamageShip(victim.Index, SpaceRules.LaserDamage, laser.Owner);
        }
        _lasers.RemoveAll(laser => !laser.Alive);
    }

    private void UpdateTorpedoes(float delta)
    {
        foreach (var torpedo in _torpedoes)
        {
            if (!torpedo.Alive) continue;
            if (torpedo.Fuel > 0f)
            {
                var target = _ships[1 - torpedo.Owner];
                if (!target.Destroyed)
                {
                    var desired = ShortestOffset(torpedo.Position, target.Position).Angle();
                    var correction = Mathf.Clamp(Mathf.AngleDifference(torpedo.Angle, desired),
                        -5.2f * delta, 5.2f * delta);
                    torpedo.Angle += correction;
                }
                var forward = new Vector2(Mathf.Cos(torpedo.Angle), Mathf.Sin(torpedo.Angle));
                torpedo.Velocity = (torpedo.Velocity + forward * 155f * delta).LimitLength(410f);
                torpedo.Fuel -= delta;
            }
            else
            {
                torpedo.Coast -= delta;
                if (torpedo.Coast <= 0f) { ExplodeTorpedo(torpedo); continue; }
            }
            torpedo.Position = Wrap(torpedo.Position + torpedo.Velocity * delta);
            torpedo.Trail.Add(torpedo.Position);
            if (torpedo.Trail.Count > 15) torpedo.Trail.RemoveAt(0);
            var victim = _ships[1 - torpedo.Owner];
            if (victim.Destroyed || victim.ShieldSeconds > 0f ||
                ShortestOffset(torpedo.Position, victim.Position).LengthSquared() >
                Mathf.Pow(SpaceRules.ShipRadius + 10f, 2f)) continue;
            _ships[torpedo.Owner].TorpedoesHit++;
            ExplodeTorpedo(torpedo);
            DamageShip(victim.Index, SpaceRules.TorpedoDamage, torpedo.Owner);
        }
        _torpedoes.RemoveAll(torpedo => !torpedo.Alive);
    }

    private void ExplodeTorpedo(Torpedo torpedo)
    {
        if (!torpedo.Alive) return;
        torpedo.Alive = false;
        AddBurst(torpedo.Position, new Color("#ffb75e"), 65f, .7f);
        SoundEffects.Play("missile_impact", -11f);
    }

    internal void DamageShip(int victimIndex, int amount, int attackerIndex)
    {
        var victim = _ships[victimIndex];
        if (_phase != SpaceDuelPhase.Playing || victim.Destroyed || victim.ShieldSeconds > 0f) return;
        victim.Health = Math.Max(0, victim.Health - amount);
        AddBurst(victim.Position, new Color("#ff8b65"), 33f, .4f);
        if (victim.Health > 0) return;
        victim.Destroyed = true;
        victim.RespawnSeconds = 1.15f;
        victim.Thrusting = false;
        AddBurst(victim.Position, new Color("#ffcf81"), 110f, 1.1f);
        SoundEffects.Play("tank_explosion", -8f);
        if (!_score.RecordKill(attackerIndex)) return;
        _phase = SpaceDuelPhase.Finishing;
        _finishSeconds = 3f;
        foreach (var torpedo in _torpedoes) ExplodeTorpedo(torpedo);
        foreach (var laser in _lasers) laser.Alive = false;
        UpdateHud();
    }

    private Vector2 Wrap(Vector2 position) => new(
        SpaceRules.Wrap(position.X, _arenaSize.X), SpaceRules.Wrap(position.Y, _arenaSize.Y));

    private Vector2 ShortestOffset(Vector2 from, Vector2 to) => new(
        SpaceRules.WrappedDelta(from.X, to.X, _arenaSize.X),
        SpaceRules.WrappedDelta(from.Y, to.Y, _arenaSize.Y));

    private void AddBurst(Vector2 position, Color color, float radius, float lifetime)
        => _bursts.Add(new Burst { Position = position, Color = color, Radius = radius, Lifetime = lifetime });

    private void ReturnToGameBox()
    {
        SoundEffects.Play("ui_back", -12f);
        GetTree().ChangeSceneToFile("res://Core/MainMenu.tscn");
    }

    private void ShowPhase()
    {
        _menu.Visible = _phase == SpaceDuelPhase.Menu;
        _hud.Visible = _phase is SpaceDuelPhase.Playing or SpaceDuelPhase.Finishing;
        _pause.Visible = _phase == SpaceDuelPhase.Playing && _paused;
        _results.Visible = _phase == SpaceDuelPhase.Results;
    }

    private void ShowResults()
    {
        var winner = _ships[_score.Winner];
        _resultTitle.Text = $"SEJR!  SPILLER {winner.Index + 1} VINDER";
        _resultTitle.AddThemeColorOverride("font_color", ShipColor(winner.Index));
        _resultStats.Text = $"Lasere affyret: {winner.LasersFired}     Lasere ramt: {winner.LasersHit}     " +
            $"Torpedoer affyret: {winner.TorpedoesFired}     Torpedoer ramt: {winner.TorpedoesHit}\n" +
            $"Topfart: {Mathf.RoundToInt(winner.MaximumSpeed)}     Energi brugt: {Mathf.RoundToInt(winner.Energy.TotalSpent)}";
    }

    private void UpdateHud()
    {
        for (var i = 0; i < 2; i++)
        {
            var ship = _ships[i];
            _scoreLabels[i].Text = $"{(_twoPlayers || i == 0 ? $"SPILLER {i + 1}" : "COMPUTER")}   {_score.Kills(i)} / {SpaceRules.WinningKills}";
            _statusLabels[i].Text = i == 1 && !_twoPlayers
                ? $"AI: {new[] { "Angrib", "Kreds", "Hold afstand" }[_aiStyle]}"
                : $"Fart: {Mathf.RoundToInt(ship.Velocity.Length())}";
            _healthBars[i].Value = ship.Health;
            _energyBars[i].Value = ship.Energy.Current;
        }
    }

    private void BuildUi()
    {
        var layer = new CanvasLayer { Name = "DuelUi" };
        AddChild(layer);
        BuildMenu(layer);
        BuildHud(layer);
        BuildPause(layer);
        BuildResults(layer);
    }

    private static Control FullScreen(CanvasLayer layer, string name)
    {
        var root = new Control { Name = name, MouseFilter = Control.MouseFilterEnum.Ignore };
        layer.AddChild(root);
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return root;
    }

    private void BuildMenu(CanvasLayer layer)
    {
        _menu = FullScreen(layer, "SpaceMenu");
        _menu.AddChild(UiStyle.Backdrop(new Color(0f, .02f, .08f, .65f)));
        var center = new CenterContainer();
        _menu.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(900f, 0f) };
        panel.AddThemeStyleboxOverride("panel", UiStyle.Box(new Color(.04f, .1f, .2f, .92f), 28));
        center.AddChild(panel);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 18);
        panel.AddChild(column);
        column.AddChild(UiStyle.Label("SPACE DUEL", 74, UiStyle.Gold));
        column.AddChild(UiStyle.Label("Først til 10 sejre · Brug energi med omtanke", 28, UiStyle.Sky));
        var solo = UiStyle.Button("Solo mod computer", 32);
        var versus = UiStyle.Button("To spillere", 32);
        var back = UiStyle.Button("Tilbage til Game Box", 25);
        solo.Pressed += () => StartMatch(false);
        versus.Pressed += () => StartMatch(true);
        back.Pressed += ReturnToGameBox;
        foreach (var button in new[] { solo, versus, back })
        {
            button.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            SoundEffects.BindHover(button);
            column.AddChild(button);
        }
        column.AddChild(UiStyle.Label("P1: WASD · Space laser · Shift torpedo", 23));
        column.AddChild(UiStyle.Label("P2: Pile · Enter/Num0 laser · + torpedo", 23));
    }

    private void BuildHud(CanvasLayer layer)
    {
        _hud = FullScreen(layer, "MatchHud");
        _scoreLabels = new Label[2];
        _statusLabels = new Label[2];
        _healthBars = new ProgressBar[2];
        _energyBars = new ProgressBar[2];
        for (var i = 0; i < 2; i++)
        {
            var panel = new PanelContainer { CustomMinimumSize = new Vector2(395f, 0f) };
            panel.Position = new Vector2(i == 0 ? 28f : _arenaSize.X - 423f, 25f);
            panel.AddThemeStyleboxOverride("panel", UiStyle.Box(new Color(.04f, .09f, .18f, .83f), 15));
            _hud.AddChild(panel);
            var column = new VBoxContainer();
            column.AddThemeConstantOverride("separation", 5);
            panel.AddChild(column);
            _scoreLabels[i] = UiStyle.Label("", 27, ShipColor(i));
            _statusLabels[i] = UiStyle.Label("", 19);
            _healthBars[i] = MakeBar(SpaceRules.ShipHealth, new Color("#f77368"));
            _energyBars[i] = MakeBar(SpaceRules.MaximumEnergy, new Color("#6de5df"));
            column.AddChild(_scoreLabels[i]);
            column.AddChild(UiStyle.Label("LIV", 17));
            column.AddChild(_healthBars[i]);
            column.AddChild(UiStyle.Label("ENERGI", 17));
            column.AddChild(_energyBars[i]);
            column.AddChild(_statusLabels[i]);
        }
        var goal = UiStyle.Label("FØRST TIL 10", 27, UiStyle.Gold);
        goal.Position = new Vector2(_arenaSize.X * .5f - 190f, 32f);
        goal.CustomMinimumSize = new Vector2(380f, 42f);
        _hud.AddChild(goal);
        var controls = UiStyle.Label("WASD / PILE: STYR   ·   SPACE / ENTER: LASER   ·   SHIFT / +: TORPEDO   ·   ESC: PAUSE", 22);
        controls.Position = new Vector2(170f, _arenaSize.Y - 55f);
        controls.CustomMinimumSize = new Vector2(_arenaSize.X - 340f, 40f);
        _hud.AddChild(controls);
    }

    private static ProgressBar MakeBar(float maximum, Color color)
    {
        var bar = new ProgressBar { MaxValue = maximum, Value = maximum,
            CustomMinimumSize = new Vector2(345f, 20f), ShowPercentage = false };
        bar.AddThemeStyleboxOverride("background", UiStyle.Box(new Color("#27374e"), 6));
        bar.AddThemeStyleboxOverride("fill", UiStyle.Box(color, 6));
        return bar;
    }

    private void BuildPause(CanvasLayer layer)
    {
        _pause = FullScreen(layer, "PauseOverlay");
        _pause.AddChild(UiStyle.Backdrop(new Color(0f, .02f, .08f, .76f)));
        var center = new CenterContainer();
        _pause.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 19);
        center.AddChild(column);
        column.AddChild(UiStyle.Label("PAUSE", 64, UiStyle.Gold));
        var resume = UiStyle.Button("Fortsæt", 29);
        var restart = UiStyle.Button("Start forfra", 29);
        var menu = UiStyle.Button("Space Duel-menu", 29);
        var gameBox = UiStyle.Button("Tilbage til Game Box", 25);
        resume.Pressed += () => { _paused = false; ShowPhase(); };
        restart.Pressed += () => StartMatch(_twoPlayers);
        menu.Pressed += ReturnToMenu;
        gameBox.Pressed += ReturnToGameBox;
        foreach (var button in new[] { resume, restart, menu, gameBox })
        {
            button.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            SoundEffects.BindHover(button);
            column.AddChild(button);
        }
    }

    private void BuildResults(CanvasLayer layer)
    {
        _results = FullScreen(layer, "VictoryOverlay");
        _results.AddChild(UiStyle.Backdrop(new Color(0f, .02f, .08f, .58f)));
        _resultTitle = UiStyle.Label("", 64, UiStyle.Gold);
        _resultTitle.Position = new Vector2(180f, 105f);
        _resultTitle.CustomMinimumSize = new Vector2(_arenaSize.X - 360f, 95f);
        _results.AddChild(_resultTitle);
        _resultStats = UiStyle.Label("", 27);
        _resultStats.Position = new Vector2(150f, _arenaSize.Y - 280f);
        _resultStats.CustomMinimumSize = new Vector2(_arenaSize.X - 300f, 110f);
        _results.AddChild(_resultStats);
        var prompt = UiStyle.Label("TRYK ENTER FOR SPACE DUEL-MENU", 32, UiStyle.Sky);
        prompt.Position = new Vector2(250f, _arenaSize.Y - 105f);
        prompt.CustomMinimumSize = new Vector2(_arenaSize.X - 500f, 65f);
        _results.AddChild(prompt);
    }

    private static Color ShipColor(int index) => index == 0 ? new Color("#6ee0f1") : new Color("#ff8c79");

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, _arenaSize), new Color("#061225"));
        DrawCircle(_arenaSize * new Vector2(.27f, .28f), 295f, new Color(.18f, .16f, .35f, .12f));
        DrawCircle(_arenaSize * new Vector2(.78f, .72f), 360f, new Color(.11f, .31f, .42f, .11f));
        for (var i = 0; i < _stars.Count; i++)
        {
            var twinkle = .4f + .35f * Mathf.Sin(_clock * (i % 5 + 1) * .48f + i);
            DrawCircle(_stars[i], _starSizes[i], new Color(.75f, .9f, 1f, twinkle));
        }
        if (_phase == SpaceDuelPhase.Menu)
        {
            DrawShip(new Vector2(_arenaSize.X * .19f, _arenaSize.Y * .49f), -.2f, 2.4f, 0, true);
            DrawShip(new Vector2(_arenaSize.X * .81f, _arenaSize.Y * .51f), Mathf.Pi + .2f, 2.4f, 1, true);
            return;
        }
        if (_phase == SpaceDuelPhase.Results)
        {
            DrawShip(new Vector2(_arenaSize.X * .5f, _arenaSize.Y * .43f), -.5f, 5.2f, _score.Winner, true);
            return;
        }
        foreach (var burst in _bursts) DrawWrapped(burst.Position, point => DrawBurst(point, burst));
        foreach (var laser in _lasers)
            if (laser.Alive) DrawWrapped(laser.Position, point =>
            {
                var direction = laser.Velocity.Normalized();
                DrawLine(point - direction * 22f, point + direction * 7f, new Color("#e32430"), 10f);
                DrawLine(point - direction * 18f, point + direction * 8f, new Color("#ffb0a0"), 4f);
            });
        foreach (var torpedo in _torpedoes)
        {
            if (!torpedo.Alive) continue;
            for (var i = 1; i < torpedo.Trail.Count; i++)
            {
                if (torpedo.Trail[i].DistanceTo(torpedo.Trail[i - 1]) > 70f) continue;
                var alpha = (float)i / torpedo.Trail.Count * .55f;
                DrawLine(torpedo.Trail[i - 1], torpedo.Trail[i], new Color(1f, .42f, .17f, alpha), 9f);
            }
            DrawWrapped(torpedo.Position, point => DrawTorpedo(point, torpedo));
        }
        foreach (var ship in _ships)
        {
            if (ship.Destroyed) continue;
            DrawWrapped(ship.Position, point => DrawShip(point, ship.Angle, 1f, ship.Index, ship.Thrusting));
        }
    }

    private void DrawWrapped(Vector2 point, Action<Vector2> draw)
    {
        for (var x = -1; x <= 1; x++)
            for (var y = -1; y <= 1; y++)
                draw(point + new Vector2(x * _arenaSize.X, y * _arenaSize.Y));
    }

    private void DrawBurst(Vector2 point, Burst burst)
    {
        var progress = burst.Age / burst.Lifetime;
        var alpha = 1f - progress;
        DrawCircle(point, burst.Radius * (.2f + progress),
            new Color(burst.Color.R, burst.Color.G, burst.Color.B, .18f * alpha));
        DrawArc(point, burst.Radius * (.3f + progress), 0f, Mathf.Tau, 28,
            new Color(burst.Color.R, burst.Color.G, burst.Color.B, .85f * alpha), 5f);
    }

    private void DrawTorpedo(Vector2 position, Torpedo torpedo)
    {
        DrawSetTransform(position, torpedo.Angle, Vector2.One);
        DrawColoredPolygon(new[] { new Vector2(16f, 0f), new Vector2(-10f, -8f),
            new Vector2(-13f, 8f) }, new Color("#d8e8ec"));
        DrawLine(new Vector2(-8f, 0f), new Vector2(7f, 0f), new Color("#ff8a69"), 3f);
        if (torpedo.Fuel > 0f)
        {
            var length = 15f + 9f * Mathf.Sin(_clock * 29f);
            DrawColoredPolygon(new[] { new Vector2(-12f, -5f), new Vector2(-12f - length, 0f),
                new Vector2(-12f, 5f) }, new Color(1f, .43f, .13f, .85f));
        }
        DrawSetTransform(Vector2.Zero);
    }

    private void DrawShip(Vector2 position, float angle, float scale, int index, bool thrusting)
    {
        var color = ShipColor(index);
        DrawSetTransform(position, angle, Vector2.One * scale);
        if (thrusting)
        {
            var flame = 17f + 8f * Mathf.Sin(_clock * 24f + index);
            DrawColoredPolygon(new[] { new Vector2(-24f, -9f), new Vector2(-24f - flame, 0f),
                new Vector2(-24f, 9f) }, new Color(1f, .52f, .2f, .82f));
        }
        DrawColoredPolygon(new[] { new Vector2(-25f, -16f), new Vector2(4f, -15f),
            new Vector2(29f, 0f), new Vector2(4f, 15f), new Vector2(-25f, 16f),
            new Vector2(-15f, 0f) }, color);
        DrawColoredPolygon(new[] { new Vector2(-17f, -10f), new Vector2(4f, -10f),
            new Vector2(20f, 0f), new Vector2(4f, 10f), new Vector2(-17f, 10f) },
            new Color("#162b48"));
        DrawLine(new Vector2(-18f, -17f), new Vector2(7f, -17f), color.Lightened(.3f), 3f);
        DrawLine(new Vector2(-18f, 17f), new Vector2(7f, 17f), color.Lightened(.3f), 3f);
        DrawCircle(new Vector2(2f, 0f), 6f, new Color("#b6f7ff"));
        DrawSetTransform(Vector2.Zero);
    }
}
