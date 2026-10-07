using System;
using GameBox.Shared.Audio;
using Godot;

namespace GameBox.Games.TankArena;

public partial class TankUnit : CharacterBody3D
{
    public TankTuning Tuning { get; private set; } = new();
    public bool IsPlayer { get; private set; }
    public bool IsBoss { get; private set; }
    public bool IsDestroyed => Health.IsDead;
    public HealthModel Health { get; private set; } = new(5);
    public TankWeapon Weapon { get; private set; } = null!;
    public Node3D Turret { get; private set; } = null!;
    public event Action<TankUnit>? Destroyed;
    public event Action<int>? Damaged;

    private MeshInstance3D _warningLamp = null!;
    private AudioStreamPlayer? _engineAudio;
    private readonly TankMotor _motor = new();
    private float _wreckSeconds;

    public void Configure(TankTuning tuning, bool player, bool boss = false)
    {
        Tuning = tuning;
        IsPlayer = player;
        IsBoss = boss;
        Health = new HealthModel(tuning.Health);
    }

    public override void _Ready()
    {
        CollisionLayer = 2;
        CollisionMask = 1 | 2;
        var shape = new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.8f, 1.25f, 2.5f) } };
        AddChild(shape);
        var hullColor = IsPlayer ? new Color("#58c9dd") : IsBoss ? new Color("#a45ce0") : new Color("#e6745c");
        AddMesh(new BoxMesh { Size = new Vector3(1.9f, 0.7f, 2.55f) }, new Vector3(0, 0, 0), hullColor);
        AddMesh(new BoxMesh { Size = new Vector3(2.18f, 0.42f, 2.5f) }, new Vector3(0, -0.40f, 0), new Color("#263242"));
        Turret = new Node3D { Position = new Vector3(0, 0.55f, 0) };
        AddChild(Turret);
        var dome = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.48f, BottomRadius = 0.68f, Height = 0.46f },
            MaterialOverride = Material(hullColor) };
        Turret.AddChild(dome);
        var barrelOffsets = IsBoss ? new[] { -0.38f, 0.38f } : new[] { 0f };
        foreach (var offset in barrelOffsets)
        {
            var barrel = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.13f, BottomRadius = 0.13f, Height = 1.9f },
                Position = new Vector3(offset, 0.05f, -1.1f), Rotation = new Vector3(Mathf.Pi / 2f, 0, 0),
                MaterialOverride = Material(new Color("#334354")) };
            Turret.AddChild(barrel);
        }
        _warningLamp = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.46f, Height = 0.92f },
            Position = new Vector3(0, 1.13f, -0.25f),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#ffdd43"),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded }, Visible = false };
        Turret.AddChild(_warningLamp);
        Weapon = new TankWeapon { Tank = this };
        Turret.AddChild(Weapon);
        Scale = Vector3.One * Tuning.Scale;
        if (IsPlayer) _engineAudio = SoundEffects.StartEngine(this);
    }

    public void Drive(float throttle, float turn, double delta)
    {
        if (IsDestroyed) return;
        _motor.Step(this, Mathf.Clamp(throttle, -1, 1), Mathf.Clamp(turn, -1, 1), delta);
        if (_engineAudio is not null) _engineAudio.VolumeDb = Mathf.Lerp(-29f, -21f, Mathf.Abs(throttle));
    }

    public void AimAt(Vector3 target, double delta)
    {
        if (IsDestroyed) return;
        var direction = target - Turret.GlobalPosition;
        direction.Y = 0;
        if (direction.LengthSquared() < 0.05f) return;
        var desired = Mathf.Atan2(-direction.X, -direction.Z);
        var current = Turret.GlobalRotation.Y;
        var next = Mathf.RotateToward(current, desired, Tuning.TurretSpeed * (float)delta);
        Turret.GlobalRotation = new Vector3(0, next, 0);
    }

    public void SetWarning(bool warning) => _warningLamp.Visible = warning && !IsDestroyed;

    public void TakeDamage(int amount)
    {
        if (IsDestroyed) return;
        var died = Health.Damage(amount);
        Damaged?.Invoke(Health.Current);
        if (!died) SoundEffects.Play("tank_hit", -10f);
        else if (IsPlayer) SoundEffects.Play("tank_destroy", -8f);
        if (!died) return;
        _wreckSeconds = IsBoss ? 6f : 5f;
        _warningLamp.Visible = false;
        var mat = Material(new Color("#4d5558"));
        foreach (var child in GetChildren())
        {
            if (child is MeshInstance3D mesh) mesh.MaterialOverride = mat;
        }
        foreach (var child in Turret.GetChildren())
        {
            if (child is MeshInstance3D mesh) mesh.MaterialOverride = mat;
        }
        Turret.Position += new Vector3(0, .28f, 0);
        AddChild(new TankSmoke { Position = new Vector3(0, 1.05f, 0), ProcessMode = ProcessModeEnum.Always });
        Destroyed?.Invoke(this);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsDestroyed || IsPlayer) return;
        _wreckSeconds -= (float)delta;
        if (_wreckSeconds <= 0) QueueFree();
    }

    public override void _ExitTree()
    {
        if (_engineAudio is null) return;
        _engineAudio.Stop();
        _engineAudio.Stream = null;
        _engineAudio = null;
    }

    private void AddMesh(Mesh mesh, Vector3 position, Color color)
        => AddChild(new MeshInstance3D { Mesh = mesh, Position = position, MaterialOverride = Material(color) });

    private static StandardMaterial3D Material(Color color)
        => new() { AlbedoColor = color, Roughness = 0.82f };
}
