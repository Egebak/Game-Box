using System;
using GameBox.Shared.Audio;
using Godot;

namespace GameBox.Games.TankArena;

public partial class ShellProjectile : Node3D
{
    public Vector3 Direction { get; set; }
    public float Speed { get; set; } = 14f;
    public int Damage { get; set; } = 1;
    public TankUnit Shooter { get; set; } = null!;
    public event Action<ShellProjectile>? Ended;
    private float _life = 3.8f;
    private bool _ended;

    public override void _Ready()
    {
        AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.34f, Height = 0.68f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = Shooter.IsPlayer ? new Color("#ffe16b") : new Color("#ff775d"),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded }
        });
        AddChild(new OmniLight3D { LightColor = Shooter.IsPlayer ? new Color("#ffe16b") : new Color("#ff775d"), LightEnergy = 1.3f, OmniRange = 3f });
    }

    public override void _PhysicsProcess(double delta)
    {
        var step = Direction.Normalized() * Speed * (float)delta;
        var query = PhysicsRayQueryParameters3D.Create(GlobalPosition, GlobalPosition + step, 3);
        query.Exclude = new Godot.Collections.Array<Rid> { Shooter.GetRid() };
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count > 0)
        {
            var position = hit["position"].AsVector3();
            if (hit["collider"].AsGodotObject() is TankUnit tank)
            {
                TankAudio.Play("impact_metal", -10f);
                tank.TakeDamage(Damage);
            }
            else TankAudio.Play("impact_wall", -11f);
            Impact(position);
            Finish();
            return;
        }
        GlobalPosition += step;
        _life -= (float)delta;
        if (_life <= 0) Finish();
    }

    private void Finish()
    {
        if (_ended) return;
        _ended = true;
        Ended?.Invoke(this);
        QueueFree();
    }

    private void Impact(Vector3 position)
    {
        var flash = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.56f, Height = 1.12f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#ffe49b"),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded }
        };
        GetParent().AddChild(flash);
        flash.GlobalPosition = position;
        var tween = flash.CreateTween();
        tween.TweenProperty(flash, "scale", Vector3.Zero, 0.28f);
        tween.TweenCallback(Callable.From(flash.QueueFree));
    }
}
