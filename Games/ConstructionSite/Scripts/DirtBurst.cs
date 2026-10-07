using System;
using Godot;

namespace ChristiansSpilBox.Games.ConstructionSite;

public partial class DirtBurst : Node3D
{
    private readonly MeshInstance3D[] _bits = new MeshInstance3D[7];
    private readonly Vector3[] _velocity = new Vector3[7];
    private float _age;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Pausable;
        var random = Random.Shared;
        for (var i = 0; i < _bits.Length; i++)
        {
            _bits[i] = SiteVisuals.Sphere(this, .11f + i % 3 * .035f, Vector3.Zero,
                i % 2 == 0 ? SiteVisuals.Soil : new Color("#bd8050"));
            var angle = Mathf.Tau * i / _bits.Length;
            _velocity[i] = new Vector3(Mathf.Cos(angle), .8f + (float)random.NextDouble() * .7f,
                Mathf.Sin(angle)) * 2.2f;
        }
    }

    public override void _Process(double delta)
    {
        var step = (float)delta;
        _age += step;
        for (var i = 0; i < _bits.Length; i++)
        {
            _bits[i].Position += _velocity[i] * step;
            _velocity[i].Y -= 5f * step;
            _bits[i].Scale = Vector3.One * Mathf.Max(.01f, 1f - _age / .55f);
        }
        if (_age >= .55f) QueueFree();
    }
}
