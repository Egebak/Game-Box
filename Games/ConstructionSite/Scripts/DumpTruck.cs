using System;
using System.Collections.Generic;
using GameBox.Shared.Audio;
using Godot;

namespace GameBox.Games.ConstructionSite;

public partial class DumpTruck : Node3D
{
    [Export] public float DepartureSeconds = 3.5f;
    [Export] public float DeliveryRingRadius = 5.15f;
    private readonly List<MeshInstance3D> _soil = new();
    private MeshInstance3D _highlight = null!;
    private Node3D _deliveryRing = null!;
    private StandardMaterial3D _bedHighlightMaterial = null!;
    private AudioStreamPlayer? _engine;
    private Vector3 _departureStart;
    private float _departureTime;
    private bool _departing;
    private bool _departed;
    private int _capacity = 8;
    private float _ringPulse;

    public int VisibleLoads { get; private set; }
    public bool IsDeparting => _departing;
    public bool DeliveryRingVisible => _deliveryRing.Visible;
    public Vector3 BedCenter => ToGlobal(new Vector3(0, 1.9f, .72f));
    public event Action? DepartureFinished;

    public void Configure(int capacity) => _capacity = capacity;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Pausable;
        SiteVisuals.Box(this, new Vector3(4.7f, .38f, 7.4f), new Vector3(0, .8f, 0), SiteVisuals.Dark);
        SiteVisuals.Box(this, new Vector3(4.25f, 1.58f, 2.1f), new Vector3(0, 1.62f, -2.48f), new Color("#4cb9ce"));
        SiteVisuals.Box(this, new Vector3(3.85f, .7f, .1f), new Vector3(0, 1.92f, -3.56f), new Color("#bee9ef"));
        foreach (var x in new[] { -2.13f, 2.13f })
            foreach (var z in new[] { -2.35f, 1.6f })
                SiteVisuals.Cylinder(this, .68f, .44f, new Vector3(x, .65f, z), SiteVisuals.Dark)
                    .Rotation = new Vector3(0, 0, Mathf.Pi / 2f);

        SiteVisuals.Box(this, new Vector3(4.45f, .18f, 4.45f), new Vector3(0, 1.38f, .9f), new Color("#e0a044"));
        SiteVisuals.Box(this, new Vector3(.16f, .92f, 4.5f), new Vector3(-2.17f, 1.8f, .9f), SiteVisuals.Orange);
        SiteVisuals.Box(this, new Vector3(.16f, .92f, 4.5f), new Vector3(2.17f, 1.8f, .9f), SiteVisuals.Orange);
        SiteVisuals.Box(this, new Vector3(4.45f, .92f, .16f), new Vector3(0, 1.8f, -1.27f), SiteVisuals.Orange);
        SiteVisuals.Box(this, new Vector3(4.45f, .92f, .16f), new Vector3(0, 1.8f, 3.07f), SiteVisuals.Orange);

        var zone = new Area3D { Name = "LoadingZone", Position = new Vector3(0, 2.45f, .9f) };
        AddChild(zone);
        zone.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(4.6f, 2.4f, 4.6f) } });
        _bedHighlightMaterial = SiteVisuals.Material(SiteVisuals.Yellow, true, .45f);
        _highlight = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(4.65f, .08f, 4.68f) },
            Position = new Vector3(0, 2.33f, .9f),
            MaterialOverride = _bedHighlightMaterial,
            Visible = false
        };
        AddChild(_highlight);
        _deliveryRing = new Node3D { Name = "DeliveryRing", Visible = false };
        AddChild(_deliveryRing);
        var segmentMesh = new BoxMesh { Size = new Vector3(.58f, .08f, .86f) };
        var ringMaterial = SiteVisuals.Material(new Color("#ff3e51"), true);
        for (var i = 0; i < 48; i++)
        {
            var angle = Mathf.Tau * i / 48f;
            _deliveryRing.AddChild(new MeshInstance3D
            {
                Mesh = segmentMesh,
                MaterialOverride = ringMaterial,
                Position = new Vector3(Mathf.Cos(angle) * DeliveryRingRadius, .105f,
                    Mathf.Sin(angle) * DeliveryRingRadius),
                Rotation = new Vector3(0, -angle, 0)
            });
        }

        for (var i = 0; i < _capacity; i++)
        {
            var columns = Mathf.CeilToInt(Mathf.Sqrt(_capacity * 1.2f));
            var rows = Mathf.CeilToInt((float)_capacity / columns);
            var x = Mathf.Lerp(-1.45f, 1.45f, columns == 1 ? .5f : (float)(i % columns) / (columns - 1));
            var z = Mathf.Lerp(-.45f, 2.2f, rows == 1 ? .5f : (float)(i / columns) / (rows - 1));
            var mound = SiteVisuals.Sphere(this, .68f, new Vector3(x, 1.58f, z),
                i % 2 == 0 ? SiteVisuals.Soil : new Color("#b37948"));
            mound.Scale = new Vector3(1.1f, .65f, 1.15f);
            mound.Visible = false;
            _soil.Add(mound);
        }
    }

    public bool IsCargoOverBed(Vector3 cargoPosition)
    {
        var local = ToLocal(cargoPosition);
        return Mathf.Abs(local.X) <= 2.45f && local.Z >= -1.55f && local.Z <= 3.35f &&
            local.Y >= 1.45f && local.Y <= 4.2f;
    }

    public void SetHighlighted(bool highlighted)
    {
        _highlight.Visible = highlighted && !_departing;
        _deliveryRing.Visible = highlighted && !_departing;
    }

    public void SetCargoAligned(bool aligned)
        => _bedHighlightMaterial.AlbedoColor = aligned
            ? new Color(.28f, 1f, .47f, .7f)
            : new Color(SiteVisuals.Yellow.R, SiteVisuals.Yellow.G, SiteVisuals.Yellow.B, .45f);

    public void SetLoadCount(int count)
    {
        VisibleLoads = count;
        for (var i = 0; i < _soil.Count; i++) _soil[i].Visible = i < count;
    }

    public void Depart()
    {
        if (_departing || _departed) return;
        _departing = true;
        _highlight.Visible = false;
        _deliveryRing.Visible = false;
        _departureStart = GlobalPosition;
        _departureTime = 0f;
        _engine = SoundEffects.StartEngine(this);
        if (_engine is not null) _engine.VolumeDb = -23f;
    }

    public override void _Process(double delta)
    {
        if (_deliveryRing.Visible)
        {
            _ringPulse += (float)delta;
            _deliveryRing.Scale = Vector3.One * (1f + .025f * Mathf.Sin(_ringPulse * 4f));
        }
        if (!_departing) return;
        _departureTime += (float)delta;
        var progress = Mathf.Clamp(_departureTime / DepartureSeconds, 0f, 1f);
        GlobalPosition = _departureStart + Vector3.Forward * (37f * progress * progress);
        if (progress < 1f) return;
        _departing = false;
        _departed = true;
        if (_engine is not null) { _engine.Stop(); _engine.Stream = null; _engine = null; }
        DepartureFinished?.Invoke();
    }

    public override void _ExitTree()
    {
        if (_engine is null) return;
        _engine.Stop();
        _engine.Stream = null;
        _engine = null;
    }
}
