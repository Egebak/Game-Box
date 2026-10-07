using ChristiansSpilBox.Shared.Audio;
using Godot;

namespace ChristiansSpilBox.Games.ConstructionSite;

public partial class Excavator : Node3D
{
    [Export] public float DriveSpeed = 6.5f;
    [Export] public float TurnSpeed = 1.75f;
    [Export] public float TurretSpeed = 5.2f;
    [Export] public float SiteLimit = 28f;

    private Node3D _turret = null!;
    private ExcavatorArm _arm = null!;
    private AudioStreamPlayer? _engine;

    public Vector3 BucketTip => _arm.BucketTip;
    public bool Busy => _arm.Busy;
    public bool CargoVisible => _arm.CargoVisible;
    public Vector3 CargoPosition => _arm.CargoPosition;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Pausable;
        foreach (var x in new[] { -.94f, .94f })
        {
            SiteVisuals.Box(this, new Vector3(.65f, .68f, 2.95f), new Vector3(x, .38f, 0), SiteVisuals.Dark);
            for (var z = -1; z <= 1; z++)
                SiteVisuals.Cylinder(this, .26f, .7f, new Vector3(x, .35f, z * .82f), new Color("#647783"))
                    .Rotation = new Vector3(0, 0, Mathf.Pi / 2f);
        }
        SiteVisuals.Box(this, new Vector3(2.35f, .55f, 2.75f), new Vector3(0, .87f, 0), SiteVisuals.Yellow);
        _turret = new Node3D { Position = new Vector3(0, 1.17f, 0) };
        AddChild(_turret);
        SiteVisuals.Cylinder(_turret, .78f, .26f, new Vector3(0, .02f, 0), SiteVisuals.Dark);
        SiteVisuals.Box(_turret, new Vector3(2.05f, .93f, 2.23f), new Vector3(0, .47f, .16f), SiteVisuals.Yellow);
        SiteVisuals.Box(_turret, new Vector3(.66f, .68f, .9f), new Vector3(-.59f, .74f, -.36f), new Color("#91cdd9"));
        SiteVisuals.Box(_turret, new Vector3(.75f, .13f, 1.05f), new Vector3(-.59f, 1.12f, -.32f), SiteVisuals.Dark);
        _arm = new ExcavatorArm();
        _turret.AddChild(_arm);
        _engine = SoundEffects.StartEngine(this);
        if (_engine is not null) _engine.VolumeDb = -35f;
    }

    public void Drive(float throttle, float steering, double delta)
    {
        if (Busy) { throttle = 0; steering = 0; }
        Rotation = new Vector3(0, Rotation.Y + steering * TurnSpeed * (float)delta, 0);
        var step = -GlobalBasis.Z * (throttle * DriveSpeed * (float)delta);
        var next = GlobalPosition + step;
        next.X = Mathf.Clamp(next.X, -SiteLimit, SiteLimit);
        next.Z = Mathf.Clamp(next.Z, -SiteLimit, SiteLimit);
        GlobalPosition = next;
        if (_engine is not null) _engine.VolumeDb = Mathf.Abs(throttle) > .1f ? -26f : -35f;
    }

    public void AimAt(Vector3 point, double delta)
    {
        var direction = point - _turret.GlobalPosition;
        direction.Y = 0;
        if (direction.LengthSquared() < .05f) return;
        var desired = Mathf.Atan2(-direction.X, -direction.Z);
        var next = Mathf.RotateToward(_turret.GlobalRotation.Y, desired, TurretSpeed * (float)delta);
        _turret.GlobalRotation = new Vector3(0, next, 0);
    }

    public void SetLoaded(bool loaded) => _arm.SetLoaded(loaded);
    public void PlayDig() => _arm.PlayDig();
    public void PlayDump() => _arm.PlayDump();

    public override void _ExitTree()
    {
        if (_engine is null) return;
        _engine.Stop();
        _engine.Stream = null;
        _engine = null;
    }
}
