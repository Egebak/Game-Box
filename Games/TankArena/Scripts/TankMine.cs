using System;
using System.Linq;
using Godot;

namespace GameBox.Games.TankArena;

public partial class TankMine : Node3D
{
    public event Action<Vector3>? Exploded;
    private MeshInstance3D _lamp = null!;
    private OmniLight3D _beaconLight = null!;
    private float _age;

    public override void _Ready()
    {
        AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = .7f, BottomRadius = .76f, Height = .24f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#29343b"), Roughness = .8f } });
        AddChild(new MeshInstance3D { Name = "WarningRing", Position = Vector3.Up * .15f,
            Mesh = new TorusMesh { InnerRadius = .79f, OuterRadius = 1.04f },
            MaterialOverride = WarningMaterial(new Color("#ffba4e")) });
        for (var i = 0; i < 8; i++)
        {
            var angle = i * Mathf.Tau / 8f;
            AddChild(new MeshInstance3D { Position = new Vector3(Mathf.Cos(angle) * .54f, .15f, Mathf.Sin(angle) * .54f),
                Rotation = new Vector3(0, -angle, 0), Mesh = new BoxMesh { Size = new Vector3(.18f, .04f, .38f) },
                MaterialOverride = WarningMaterial(new Color("#ffc95f")) });
        }
        AddChild(new MeshInstance3D { Position = Vector3.Up * .32f,
            Mesh = new CylinderMesh { TopRadius = .09f, BottomRadius = .13f, Height = .42f },
            MaterialOverride = WarningMaterial(new Color("#4f5960")) });
        _lamp = new MeshInstance3D { Name = "Beacon", Position = Vector3.Up * .58f,
            Mesh = new SphereMesh { Radius = .3f, Height = .6f },
            MaterialOverride = WarningMaterial(new Color("#ff5d45")) };
        AddChild(_lamp);
        _beaconLight = new OmniLight3D { Position = Vector3.Up * .6f, LightColor = new Color("#ff7252"),
            OmniRange = 4.5f, LightEnergy = 1.8f };
        AddChild(_beaconLight);
    }

    public override void _PhysicsProcess(double delta)
    {
        _age += (float)delta;
        var pulse = .5f + .5f * Mathf.Sin(_age * 7f);
        _lamp.Scale = Vector3.One * Mathf.Lerp(.85f, 1.15f, pulse);
        _beaconLight.LightEnergy = Mathf.Lerp(1.2f, 2.6f, pulse);
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

    private static StandardMaterial3D WarningMaterial(Color color) => new()
    {
        AlbedoColor = color,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
    };
}
