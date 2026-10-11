using System;
using System.Linq;
using Godot;

namespace GameBox.Games.TankArena;

public partial class TankMine : Node3D
{
    public event Action<Vector3>? Exploded;
    private MeshInstance3D _lamp = null!;
    private float _age;

    public override void _Ready()
    {
        AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = .53f, BottomRadius = .62f, Height = .17f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#303a3d"), Roughness = .8f } });
        _lamp = new MeshInstance3D { Position = Vector3.Up * .12f,
            Mesh = new SphereMesh { Radius = .16f, Height = .32f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#ff735d"),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } };
        AddChild(_lamp);
    }

    public override void _PhysicsProcess(double delta)
    {
        _age += (float)delta;
        _lamp.Visible = _age % .7f < .46f;
        foreach (var tank in GetParent().GetChildren().OfType<TankUnit>())
        {
            if (tank.IsDestroyed) continue;
            var dx = tank.GlobalPosition.X - GlobalPosition.X;
            var dz = tank.GlobalPosition.Z - GlobalPosition.Z;
            var radius = 1.35f * tank.Tuning.Scale;
            if (dx * dx + dz * dz > radius * radius) continue;
            // The mine sits behind the tank when placed, so driving back over it is dangerous.
            Exploded?.Invoke(GlobalPosition);
            tank.TakeDamage(3);
            QueueFree();
            return;
        }
    }
}
