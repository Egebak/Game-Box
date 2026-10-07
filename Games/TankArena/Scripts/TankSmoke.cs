using System;
using System.Collections.Generic;
using Godot;

namespace GameBox.Games.TankArena;

public partial class TankSmoke : Node3D
{
    public float EmissionSeconds { get; set; } = 5f;

    private sealed class Puff
    {
        public MeshInstance3D Mesh = null!;
        public StandardMaterial3D Material = null!;
        public Vector3 Velocity;
        public float Age;
        public float Lifetime;
    }

    private readonly List<Puff> _puffs = new();
    private readonly Random _random = new();
    private SphereMesh _mesh = null!;
    private float _emitClock;
    private float _age;

    public int ActivePuffCount => _puffs.Count;

    public override void _Ready()
    {
        _mesh = new SphereMesh { Radius = .5f, Height = 1f };
        EmitPuff();
    }

    public override void _Process(double delta)
    {
        var step = (float)delta;
        _age += step;
        if (_age < EmissionSeconds) _emitClock += step;
        while (_emitClock >= .12f)
        {
            _emitClock -= .12f;
            EmitPuff();
        }

        for (var i = _puffs.Count - 1; i >= 0; i--)
        {
            var puff = _puffs[i];
            puff.Age += step;
            if (puff.Age >= puff.Lifetime)
            {
                puff.Mesh.QueueFree();
                _puffs.RemoveAt(i);
                continue;
            }

            var progress = puff.Age / puff.Lifetime;
            puff.Mesh.Position += puff.Velocity * step;
            puff.Mesh.Scale = Vector3.One * Mathf.Lerp(.3f, 1.35f, progress);
            puff.Material.AlbedoColor = new Color(.19f, .22f, .24f, .65f * Mathf.Pow(1f - progress, 1.3f));
        }
        if (_age >= EmissionSeconds && _puffs.Count == 0) QueueFree();
    }

    private void EmitPuff()
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(.19f, .22f, .24f, .65f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        var mesh = new MeshInstance3D
        {
            Mesh = _mesh,
            MaterialOverride = material,
            Position = new Vector3(RandomOffset(.32f), 0f, RandomOffset(.32f)),
            Scale = Vector3.One * .3f
        };
        AddChild(mesh);
        _puffs.Add(new Puff
        {
            Mesh = mesh,
            Material = material,
            Velocity = new Vector3(RandomOffset(.48f), 1f + (float)_random.NextDouble() * .35f,
                RandomOffset(.48f)),
            Lifetime = 1.35f + (float)_random.NextDouble() * .45f
        });
    }

    private float RandomOffset(float range) => ((float)_random.NextDouble() * 2f - 1f) * range;
}
