using System;
using System.Collections.Generic;
using ChristiansSpilBox.Core;
using ChristiansSpilBox.Shared.Audio;
using ChristiansSpilBox.Shared.UI;
using Godot;

namespace ChristiansSpilBox.Games.ConstructionSite;

public partial class ConstructionSite : Node3D
{
    [Export] public int RequiredLoads = 8;
    [Export] public int SoilPileCapacity = 12;
    [Export] public float CameraFollowSpeed = 3.2f;
    [Export] public float DigReach = 6.3f;
    [Export] public float TruckReach = 7f;

    private static bool? _restartMode;
    private readonly List<Node3D> _groundDrops = new();
    private ConstructionMission _mission = null!;
    private Excavator _excavator = null!;
    private SoilPile _pile = null!;
    private DumpTruck _truck = null!;
    private Camera3D _camera = null!;
    private Label _prompt = null!;
    private Label _progress = null!;
    private Label _help = null!;
    private Control _modeOverlay = null!;
    private Control _pauseOverlay = null!;
    private Control _successOverlay = null!;
    private bool _active;
    private bool _freePlay;
    private bool _finished;
    private float _departDelay = -1f;
    private float _promptSeconds;

    public Excavator Player => _excavator;
    public SoilPile Pile => _pile;
    public DumpTruck Truck => _truck;
    public ConstructionMission Mission => _mission;
    public bool SuccessVisible => _successOverlay.Visible;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        BuildWorld();
        _pile = new SoilPile { Position = new Vector3(-10, 0, -4), ProcessMode = ProcessModeEnum.Pausable };
        _pile.Configure(SoilPileCapacity);
        AddChild(_pile);
        _truck = new DumpTruck { Position = new Vector3(10, 0, -4) };
        _truck.Configure(RequiredLoads);
        _truck.DepartureFinished += FinishMission;
        AddChild(_truck);
        _excavator = new Excavator { Position = new Vector3(0, 0, 8) };
        AddChild(_excavator);
        _camera = new Camera3D { Current = true, Fov = 48f, Far = 150f };
        AddChild(_camera);
        _camera.GlobalPosition = _excavator.GlobalPosition + new Vector3(0, 25, 28);
        _camera.LookAt(_excavator.GlobalPosition + new Vector3(0, .5f, -2));
        BuildHud();
        if (_restartMode is { } freePlay)
        {
            _restartMode = null;
            StartMode(freePlay);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_active || _finished || GetTree().Paused) return;
        if (_departDelay >= 0f)
        {
            _departDelay -= (float)delta;
            if (_departDelay < 0f) _truck.Depart();
        }
        if (_promptSeconds > 0f)
        {
            _promptSeconds -= (float)delta;
            if (_promptSeconds <= 0f) UpdateGuide();
        }
        var throttle = (Input.IsKeyPressed(Key.W) ? 1f : 0f) - (Input.IsKeyPressed(Key.S) ? 1f : 0f);
        var steering = (Input.IsKeyPressed(Key.A) ? 1f : 0f) - (Input.IsKeyPressed(Key.D) ? 1f : 0f);
        _excavator.Drive(throttle, steering, delta);
        _excavator.AimAt(MousePoint(GetViewport().GetMousePosition()), delta);
        if (_mission.BucketLoaded)
        {
            var aligned = _truck.IsCargoOverBed(_excavator.CargoPosition);
            _truck.SetCargoAligned(aligned);
            if (_promptSeconds <= 0f)
            {
                _prompt.Text = aligned ? "Tøm skovlen!" : "Fyld lastbilen";
                _help.Text = aligned ? "Klik for at læsse jorden i ladet"
                    : "Rød cirkel: lastbil  •  Drej skovlen over ladet";
            }
        }
        var desired = _excavator.GlobalPosition + new Vector3(0, 25, 28);
        _camera.GlobalPosition = _camera.GlobalPosition.Lerp(desired,
            Mathf.Clamp((float)delta * CameraFollowSpeed, 0f, 1f));
        _camera.LookAt(_excavator.GlobalPosition + new Vector3(0, .5f, -2));
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape && _active && !_finished)
        {
            if (_pauseOverlay.Visible) Resume(); else Pause();
            GetViewport().SetInputAsHandled();
        }
        if (@event is not InputEventMouseButton mouse || !mouse.Pressed || !_active || _finished || GetTree().Paused)
            return;
        if (mouse.ButtonIndex == MouseButton.Left)
            TryInteractAt(MousePoint(mouse.Position, _mission.BucketLoaded ? 1.8f : 0f));
        else if (mouse.ButtonIndex == MouseButton.Right)
            TryGroundDumpAt(MousePoint(mouse.Position));
    }

    public void StartMode(bool freePlay)
    {
        if (_active) return;
        _freePlay = freePlay;
        _mission = new ConstructionMission(RequiredLoads, SoilPileCapacity, freePlay);
        _active = true;
        _modeOverlay.Visible = false;
        GetViewport().GuiReleaseFocus();
        _pile.SetRemaining(_mission.SoilRemaining);
        _truck.SetLoadCount(0);
        UpdateGuide();
        SoundEffects.Play("ui_select", -12f);
    }

    public bool TryInteractAt(Vector3 groundPoint)
    {
        if (!_active || _finished || GetTree().Paused || _excavator.Busy || _mission.IsComplete) return false;
        if (!_mission.BucketLoaded)
        {
            if (!_pile.Contains(groundPoint) || !WithinReach(_pile.GlobalPosition, DigReach))
            {
                FlashPrompt("Kør tættere på jorden");
                return false;
            }
            if (!_mission.TryDig()) return false;
            _pile.SetRemaining(_mission.SoilRemaining);
            _excavator.SetLoaded(true);
            _excavator.PlayDig();
            Burst(groundPoint + Vector3.Up * .5f);
            SoundEffects.Play("construction_dig", -10f);
            UpdateGuide();
            return true;
        }

        if (!_truck.IsCargoOverBed(_excavator.CargoPosition))
        {
            FlashPrompt("Før skovlen hen over ladet");
            return false;
        }
        var result = _mission.Dump(true);
        _excavator.SetLoaded(false);
        _excavator.PlayDump();
        _truck.SetLoadCount(_mission.Delivered);
        if (_mission.RefillSourceIfEmpty()) _pile.SetRemaining(_mission.SoilRemaining);
        Burst(_truck.BedCenter);
        SoundEffects.Play("construction_load", -8f, 1f + _mission.Delivered * .025f);
        UpdateGuide();
        if (result == DumpResult.Completed)
        {
            _prompt.Text = "Godt gået! Lastbilen kører!";
            _help.Text = "Se den køre væk";
            _pile.SetHighlighted(false);
            _truck.SetHighlighted(false);
            _departDelay = .75f;
        }
        return true;
    }

    public bool TryGroundDumpAt(Vector3 groundPoint)
    {
        if (!_active || _finished || GetTree().Paused || _excavator.Busy || !_mission.BucketLoaded) return false;
        if (!WithinReach(groundPoint, TruckReach)) return false;
        _mission.Dump(false);
        _excavator.SetLoaded(false);
        _excavator.PlayDump();
        if (_mission.RefillSourceIfEmpty()) _pile.SetRemaining(_mission.SoilRemaining);
        var drop = new Node3D { Name = "DroppedSoil", Position = groundPoint };
        AddChild(drop);
        SiteVisuals.Sphere(drop, .55f, new Vector3(0, .35f, 0), SiteVisuals.Soil)
            .Scale = new Vector3(1.25f, .55f, 1.2f);
        _groundDrops.Add(drop);
        if (_groundDrops.Count > 14) { _groundDrops[0].QueueFree(); _groundDrops.RemoveAt(0); }
        Burst(groundPoint + Vector3.Up * .4f);
        SoundEffects.Play("construction_dump", -12f);
        UpdateGuide();
        return true;
    }

    private bool WithinReach(Vector3 point, float reach)
    {
        var difference = point - _excavator.GlobalPosition;
        return new Vector2(difference.X, difference.Z).Length() <= reach;
    }

    private Vector3 MousePoint(Vector2 screenPosition, float height = 0f)
    {
        var origin = _camera.ProjectRayOrigin(screenPosition);
        var direction = _camera.ProjectRayNormal(screenPosition);
        if (Mathf.Abs(direction.Y) < .001f) return _excavator.GlobalPosition - _excavator.GlobalBasis.Z * 4f;
        var time = (height - origin.Y) / direction.Y;
        return time > 0f ? origin + direction * time : _excavator.GlobalPosition;
    }

    private void Burst(Vector3 position)
    {
        var burst = new DirtBurst();
        AddChild(burst);
        burst.GlobalPosition = position;
    }

    private void UpdateGuide()
    {
        if (!_active) return;
        _progress.Text = _freePlay
            ? $"LASTBIL  {_mission.Delivered} LÆS"
            : $"LASTBIL  {_mission.Delivered} / {_mission.RequiredLoads}";
        _prompt.Text = _mission.BucketLoaded ? "Fyld lastbilen" : "Grav jord";
        _help.Text = _mission.BucketLoaded
            ? "Rød cirkel: lastbil  •  Drej skovlen over ladet"
            : "Kør tæt på den brune jord  •  Klik for at grave";
        _pile.SetHighlighted(!_mission.BucketLoaded);
        _truck.SetHighlighted(_mission.BucketLoaded);
        _truck.SetCargoAligned(_mission.BucketLoaded && _truck.IsCargoOverBed(_excavator.CargoPosition));
    }

    private void FlashPrompt(string text)
    {
        _prompt.Text = text;
        _promptSeconds = 1.6f;
        SoundEffects.Play("ui_back", -18f);
    }

    private void FinishMission()
    {
        if (_freePlay || _finished) return;
        _finished = true;
        var path = ProjectSettings.GlobalizePath("user://game_box_state.json");
        var state = GameBoxState.Load(path);
        state.ConstructionSiteCompleted = true;
        try { state.Save(path); }
        catch (Exception error) { GD.PushWarning($"Could not save construction completion: {error.Message}"); }
        _successOverlay.Visible = true;
        SoundEffects.Play("victory", -7f);
        GetTree().Paused = true;
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
        _restartMode = _freePlay;
        GetTree().Paused = false;
        GetTree().ChangeSceneToFile("res://Games/ConstructionSite/Scenes/ConstructionSite.tscn");
    }

    private void ReturnToBox()
    {
        SoundEffects.Play("ui_back", -11f);
        GetTree().Paused = false;
        GetTree().ChangeSceneToFile("res://Core/MainMenu.tscn");
    }

    private void BuildWorld()
    {
        AddChild(new WorldEnvironment { Environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("#a8dbe8"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("#f1ecdb"), AmbientLightEnergy = .42f
        } });
        AddChild(new DirectionalLight3D { Rotation = new Vector3(-.78f, -.5f, 0), LightEnergy = .9f,
            ShadowEnabled = true });
        SiteVisuals.Box(this, new Vector3(72, .3f, 72), new Vector3(0, -.2f, 0), new Color("#8ab47b"));
        SiteVisuals.Box(this, new Vector3(9, .055f, 72), new Vector3(10, .02f, 0), new Color("#777e79"));
        for (var z = -29; z <= 29; z += 8)
            SiteVisuals.Box(this, new Vector3(.15f, .07f, 3.2f), new Vector3(10, .065f, z),
                new Color("#e8dcb5"));
        SiteVisuals.Box(this, new Vector3(13, .08f, 13), new Vector3(-10, .03f, -4),
            new Color("#c8ad80"));
        for (var i = 0; i < 6; i++)
        {
            var x = i < 3 ? -24f : 25f;
            var z = -16f + (i % 3) * 16f;
            var cone = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = .08f, BottomRadius = .43f, Height = .95f },
                Position = new Vector3(x, .48f, z), MaterialOverride = SiteVisuals.Material(SiteVisuals.Orange)
            };
            AddChild(cone);
            SiteVisuals.Box(this, new Vector3(.52f, .1f, .52f), new Vector3(x, .46f, z), Colors.White);
        }
        foreach (var z in new[] { -20f, 21f })
        {
            SiteVisuals.Box(this, new Vector3(6f, .2f, .35f), new Vector3(-18, .9f, z),
                SiteVisuals.Yellow);
            SiteVisuals.Box(this, new Vector3(.22f, 1.6f, .3f), new Vector3(-20.7f, .8f, z),
                SiteVisuals.Dark);
            SiteVisuals.Box(this, new Vector3(.22f, 1.6f, .3f), new Vector3(-15.3f, .8f, z),
                SiteVisuals.Dark);
        }
        for (var i = 0; i < 3; i++)
            SiteVisuals.Cylinder(this, .47f, 3.2f, new Vector3(-20 + i * 1.1f, .48f, 11),
                new Color("#7c91a0")).Rotation = new Vector3(Mathf.Pi / 2f, 0, 0);
    }

    private void BuildHud()
    {
        var canvas = new CanvasLayer();
        AddChild(canvas);
        var top = new MarginContainer { AnchorRight = 1f, OffsetLeft = 30, OffsetRight = -30,
            OffsetTop = 24, MouseFilter = Control.MouseFilterEnum.Ignore };
        canvas.AddChild(top);
        var topPanel = new PanelContainer();
        topPanel.AddThemeStyleboxOverride("panel", UiStyle.Box(new Color(.07f, .14f, .19f, .9f), 15));
        top.AddChild(topPanel);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 30);
        topPanel.AddChild(row);
        _prompt = UiStyle.Label("Grav jord", 34, UiStyle.Gold);
        _prompt.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _progress = UiStyle.Label($"LASTBIL  0 / {RequiredLoads}", 31, UiStyle.Sky);
        row.AddChild(_prompt);
        row.AddChild(_progress);

        var bottom = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        bottom.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
        bottom.OffsetTop = -92;
        bottom.OffsetBottom = -12;
        bottom.OffsetLeft = 150;
        bottom.OffsetRight = -150;
        bottom.AddThemeStyleboxOverride("panel", UiStyle.Box(new Color(.07f, .14f, .19f, .91f), 12));
        var guide = new VBoxContainer();
        bottom.AddChild(guide);
        _help = UiStyle.Label("Klik på jorden", 24);
        guide.AddChild(_help);
        guide.AddChild(UiStyle.Label("W/S KØR   A/D DREJ   MUS PEG   VENSTREKLIK BRUG   ESC PAUSE", 17,
            UiStyle.Sky));
        canvas.AddChild(bottom);

        _modeOverlay = MakeOverlay(canvas, "Gravemaskine-plads", false);
        AddOverlayButton(_modeOverlay, "Opgave: Fyld lastbilen", () => StartMode(false));
        AddOverlayButton(_modeOverlay, "Fri leg", () => StartMode(true));
        AddOverlayButton(_modeOverlay, "Tilbage til Spil Box", ReturnToBox);
        _modeOverlay.Visible = true;

        _pauseOverlay = MakeOverlay(canvas, "Pause", true);
        AddOverlayButton(_pauseOverlay, "Fortsæt", Resume);
        AddOverlayButton(_pauseOverlay, "Spil igen", Restart);
        AddOverlayButton(_pauseOverlay, "Tilbage til Spil Box", ReturnToBox);

        _successOverlay = MakeOverlay(canvas, "Færdig!", true);
        AddOverlayButton(_successOverlay, "Spil igen", Restart);
        AddOverlayButton(_successOverlay, "Tilbage til Spil Box", ReturnToBox);
    }

    private static Control MakeOverlay(CanvasLayer canvas, string title, bool dark)
    {
        var overlay = new Control { ProcessMode = ProcessModeEnum.Always, Visible = false };
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        canvas.AddChild(overlay);
        overlay.AddChild(UiStyle.Backdrop(new Color(.04f, .08f, .13f, dark ? .86f : .72f)));
        var center = new CenterContainer { Name = "Center" };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(center);
        var panel = new PanelContainer { Name = "Dialog", CustomMinimumSize = new Vector2(680, 390) };
        panel.AddThemeStyleboxOverride("panel", UiStyle.Box(UiStyle.Panel, 24));
        center.AddChild(panel);
        var content = new VBoxContainer { Name = "Content", Alignment = BoxContainer.AlignmentMode.Center };
        content.AddThemeConstantOverride("separation", 19);
        panel.AddChild(content);
        content.AddChild(UiStyle.Label(title, 49, UiStyle.Gold));
        return overlay;
    }

    private static void AddOverlayButton(Control overlay, string title, Action action)
    {
        var button = UiStyle.Button(title, title.Length > 20 ? 26 : 30);
        button.CustomMinimumSize = new Vector2(550, 67);
        SoundEffects.BindHover(button);
        button.Pressed += action;
        overlay.GetNode<VBoxContainer>("Center/Dialog/Content").AddChild(button);
    }
}
