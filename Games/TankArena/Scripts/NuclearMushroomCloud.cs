using System.Collections.Generic;
using Godot;

namespace GameBox.Games.TankArena;

public partial class NuclearMushroomCloud : Node3D
{
    private readonly List<MeshInstance3D> _cap = new();
    private readonly List<StandardMaterial3D> _materials = new();
    private MeshInstance3D _stem = null!;
    private MeshInstance3D _shockwave = null!;
    private OmniLight3D _light = null!;
    private float _age;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _stem = Ball(new Color("#655c56"), new Vector3(0, 3f, 0), new Vector3(1.45f, 3.1f, 1.45f));
        for (var i = 0; i < 9; i++)
        {
            var angle = Mathf.Tau * i / 9f;
            _cap.Add(Ball(i % 2 == 0 ? new Color("#6e6158") : new Color("#86705e"),
                new Vector3(Mathf.Cos(angle) * 2.1f, 6.8f + (i % 3) * .2f, Mathf.Sin(angle) * 2.1f),
                new Vector3(2.25f, 1.65f, 2.25f)));
        }
        _cap.Add(Ball(new Color("#a17a5a"), new Vector3(0, 7.5f, 0), new Vector3(2.7f, 1.5f, 2.7f)));
        _shockwave = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = .88f, OuterRadius = 1.1f },
            Position = Vector3.Up * .12f,
            MaterialOverride = Glow(new Color(1f, .8f, .42f, .75f)) };
        AddChild(_shockwave);
        _light = new OmniLight3D { LightColor = new Color("#ffdc87"), LightEnergy = 7f, OmniRange = 20f };
        AddChild(_light);
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        var rise = Mathf.Clamp(_age / 1.3f, 0f, 1f);
        var fade = Mathf.Clamp((5.2f - _age) / 2f, 0f, 1f);
        _stem.Scale = new Vector3(1f + rise * .3f, rise, 1f + rise * .3f);
        foreach (var puff in _cap)
        {
            puff.Scale = Vector3.One * Mathf.Max(.01f, rise);
            puff.Position += Vector3.Up * (float)delta * .4f;
        }
        var wave = Mathf.Clamp(_age / .8f, 0f, 1f);
        _shockwave.Scale = Vector3.One * Mathf.Lerp(.3f, NukeRules.BlastRadius, wave);
        _shockwave.Visible = wave < 1f;
        _light.LightEnergy = 7f * (1f - Mathf.Clamp(_age / .8f, 0f, 1f));
        foreach (var material in _materials)
        {
            var color = material.AlbedoColor;
            material.AlbedoColor = new Color(color.R, color.G, color.B, fade);
        }
        if (_age >= 5.2f) QueueFree();
    }

    private MeshInstance3D Ball(Color color, Vector3 position, Vector3 scale)
    {
        var material = Glow(color);
        _materials.Add(material);
        var ball = new MeshInstance3D { Mesh = new SphereMesh { Radius = 1f, Height = 2f },
            Position = position, Scale = scale, MaterialOverride = material };
        AddChild(ball);
        return ball;
    }

    private static StandardMaterial3D Glow(Color color) => new()
    {
        AlbedoColor = color, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
    };
}
