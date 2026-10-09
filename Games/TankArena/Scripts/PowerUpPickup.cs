using System;
using Godot;

namespace GameBox.Games.TankArena;

public partial class PowerUpPickup : Node3D
{
    public PowerUpKind Kind { get; set; }
    public TankUnit Player { get; set; } = null!;
    public Func<PowerUpKind, bool> TryCollect { get; set; } = null!;

    private Node3D _ring = null!;
    private float _age;

    public override void _Ready()
    {
        var color = Kind == PowerUpKind.Health ? new Color("#6df5a5") : new Color("#ffcb59");
        var core = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = .66f, Height = 1.32f },
            MaterialOverride = Glow(color.Darkened(.45f))
        };
        AddChild(core);

        var icon = new Node3D { Position = new Vector3(0, .18f, .67f) };
        AddChild(icon);
        if (Kind == PowerUpKind.Health)
        {
            IconBlock(icon, new Vector3(.2f, .9f, .12f), Vector3.Zero, color);
            IconBlock(icon, new Vector3(.9f, .2f, .12f), Vector3.Zero, color);
        }
        else
        {
            IconBlock(icon, new Vector3(.26f, .54f, .14f), new Vector3(0, -.08f, 0), color);
            var tip = new MeshInstance3D
            {
                Mesh = new PrismMesh { Size = new Vector3(.55f, .42f, .15f) },
                Position = new Vector3(0, .36f, 0),
                Rotation = new Vector3(0, 0, Mathf.Pi),
                MaterialOverride = Glow(color)
            };
            icon.AddChild(tip);
        }

        _ring = new Node3D { Rotation = new Vector3(.24f, 0, .3f) };
        AddChild(_ring);
        _ring.AddChild(new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = .93f, OuterRadius = 1.07f },
            MaterialOverride = Glow(color)
        });
        for (var i = 0; i < 4; i++)
        {
            var angle = i * Mathf.Tau / 4f;
            _ring.AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = .15f, Height = .3f },
                Position = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)),
                MaterialOverride = Glow(Colors.White)
            });
        }
        AddChild(new OmniLight3D { LightColor = color, LightEnergy = 1.1f, OmniRange = 4f });
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Player.IsDestroyed) return;
        _age += (float)delta;
        _ring.Rotation = new Vector3(.24f + Mathf.Sin(_age * 2f) * .16f, _age * 2.4f, .3f);
        Position = new Vector3(Position.X, 2.3f + Mathf.Sin(_age * 2.8f) * .22f, Position.Z);
        var offset = Player.GlobalPosition - GlobalPosition;
        offset.Y = 0;
        if (offset.LengthSquared() > 2.8f * 2.8f || !TryCollect(Kind)) return;
        TankAudio.Play("powerup_pickup", -7f);
        QueueFree();
    }

    private static void IconBlock(Node3D parent, Vector3 size, Vector3 position, Color color)
        => parent.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size }, Position = position, MaterialOverride = Glow(color)
        });

    private static StandardMaterial3D Glow(Color color)
        => new() { AlbedoColor = color, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
}
