using System;
using System.Collections.Generic;
using Godot;

namespace GameBox.Games.TankArena;

// A short, harmless mechanical burst. It uses only meshes and materials, so it
// works in the test arena without a particle texture or external art package.
public partial class TankExplosion : Node3D
{
    public float RadiusScale { get; set; } = 1f;

    private readonly List<Bit> _bits = new();
    private MeshInstance3D _flash = null!;
    private StandardMaterial3D _flashMaterial = null!;
    private OmniLight3D _light = null!;
    private float _age;

    private sealed class Bit
    {
        public MeshInstance3D Mesh = null!;
        public StandardMaterial3D Material = null!;
        public Vector3 Velocity;
        public Vector3 Spin;
        public Color Color;
        public float Lifetime;
        public bool Smoke;
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _flashMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, .63f, .18f, .92f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        _flash = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 1f, Height = 2f },
            MaterialOverride = _flashMaterial,
            Scale = Vector3.One * .28f * RadiusScale
        };
        AddChild(_flash);
        _light = new OmniLight3D { LightColor = new Color("#ffb94a"), LightEnergy = 3.4f, OmniRange = 9f * RadiusScale };
        AddChild(_light);

        for (var i = 0; i < 10; i++) AddBit(i, 10, false, false);
        for (var i = 0; i < 12; i++) AddBit(i, 12, false, true);
        for (var i = 0; i < 5; i++) AddBit(i, 5, true, false);
    }

    public override void _Process(double delta)
    {
        var step = (float)delta;
        _age += step;
        var flashProgress = Mathf.Clamp(_age / .46f, 0f, 1f);
        _flash.Scale = Vector3.One * RadiusScale * Mathf.Lerp(.28f, 2.8f, flashProgress);
        _flashMaterial.AlbedoColor = new Color(1f, .63f, .18f, .92f * (1f - flashProgress));
        _flash.Visible = flashProgress < 1f;
        _light.LightEnergy = 3.4f * (1f - flashProgress);

        foreach (var bit in _bits)
        {
            if (_age >= bit.Lifetime) { bit.Mesh.Visible = false; continue; }
            var progress = _age / bit.Lifetime;
            bit.Mesh.Position += bit.Velocity * step;
            bit.Mesh.Rotation += bit.Spin * step;
            if (bit.Smoke)
            {
                bit.Mesh.Scale = Vector3.One * RadiusScale * (.28f + progress * 1.45f);
                bit.Material.AlbedoColor = WithAlpha(bit.Color, .64f * (1f - progress));
            }
            else
            {
                bit.Velocity.Y -= 7f * step;
                bit.Mesh.Scale = Vector3.One * RadiusScale * Mathf.Max(.04f, 1f - progress * .8f);
                bit.Material.AlbedoColor = WithAlpha(bit.Color, 1f - Mathf.Pow(progress, 3f));
            }
        }
        if (_age > 1.3f) QueueFree();
    }

    private void AddBit(int index, int count, bool smoke, bool spark)
    {
        var random = Random.Shared;
        var angle = Mathf.Tau * (index + (float)random.NextDouble() * .25f) / count;
        var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        var color = smoke ? new Color("#465158") : spark ? new Color("#ffac38") :
            index % 2 == 0 ? new Color("#626b68") : new Color("#a27d61");
        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = spark ? BaseMaterial3D.ShadingModeEnum.Unshaded : BaseMaterial3D.ShadingModeEnum.PerPixel
        };
        Mesh mesh = smoke || spark
            ? new SphereMesh { Radius = smoke ? .62f : .16f, Height = smoke ? 1.24f : .32f }
            : new BoxMesh { Size = new Vector3(.4f, .3f, .52f) };
        var instance = new MeshInstance3D
        {
            Mesh = mesh, MaterialOverride = material,
            Position = radial * (smoke ? .35f : .2f) * RadiusScale,
            Scale = Vector3.One * RadiusScale * (smoke ? .28f : 1f)
        };
        AddChild(instance);
        var speed = spark ? 7.5f : smoke ? .7f : 4.1f;
        _bits.Add(new Bit
        {
            Mesh = instance, Material = material, Color = color, Smoke = smoke,
            Velocity = (radial * speed + Vector3.Up * (spark ? 4.5f : smoke ? 1.35f : 3.5f)) * RadiusScale,
            Spin = smoke ? Vector3.Zero : new Vector3(2.8f, 3.4f, 1.9f),
            Lifetime = smoke ? 1.22f : spark ? .56f : .96f
        });
    }

    private static Color WithAlpha(Color color, float alpha)
        => new(color.R, color.G, color.B, Mathf.Clamp(alpha, 0f, 1f));
}
