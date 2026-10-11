using System;
using Godot;

namespace GameBox.Games.TankArena;

public partial class NukeProjectile : Node3D
{
    public Vector3 Direction { get; set; }
    public float Speed { get; set; }
    public TankUnit Shooter { get; set; } = null!;
    public event Action<Vector3>? Detonated;
    private float _life = 8f;
    private bool _detonated;

    public override void _Ready()
    {
        AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = .52f, Height = 1.04f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#f8ea72"),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } });
        AddChild(new OmniLight3D { LightColor = new Color("#f8df71"), LightEnergy = 1.8f, OmniRange = 5f });
    }

    public override void _PhysicsProcess(double delta)
    {
        var step = Direction.Normalized() * Speed * (float)delta;
        var query = PhysicsRayQueryParameters3D.Create(GlobalPosition, GlobalPosition + step, 3);
        query.Exclude = new Godot.Collections.Array<Rid> { Shooter.GetRid() };
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count > 0)
        {
            Detonate(hit["position"].AsVector3());
            return;
        }
        GlobalPosition += step;
        _life -= (float)delta;
        if (_life <= 0f) Detonate(GlobalPosition);
    }

    private void Detonate(Vector3 position)
    {
        if (_detonated) return;
        _detonated = true;
        Detonated?.Invoke(position);
        QueueFree();
    }
}
