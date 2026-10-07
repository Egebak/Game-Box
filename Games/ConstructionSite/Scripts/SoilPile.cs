using System;
using System.Collections.Generic;
using Godot;

namespace ChristiansSpilBox.Games.ConstructionSite;

public partial class SoilPile : Node3D
{
    [Export] public float DigRadius = 3.4f;

    private readonly List<MeshInstance3D> _chunks = new();
    private MeshInstance3D _highlight = null!;
    private int _capacity = 12;

    public int VisibleLoads { get; private set; }

    public void Configure(int capacity) => _capacity = capacity;

    public override void _Ready()
    {
        _highlight = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = DigRadius + .3f, BottomRadius = DigRadius + .3f, Height = .06f },
            Position = new Vector3(0, .065f, 0),
            MaterialOverride = SiteVisuals.Material(SiteVisuals.Yellow, true, .36f)
        };
        AddChild(_highlight);
        SiteVisuals.Cylinder(this, DigRadius, .12f, new Vector3(0, .12f, 0), new Color("#caa77b"));
        for (var i = 0; i < _capacity; i++)
        {
            var angle = Mathf.Tau * i * .618034f;
            var ring = Mathf.Sqrt((i + .5f) / _capacity);
            var radius = 2.7f * ring;
            var size = Mathf.Lerp(.78f, 1.15f, 1f - ring);
            var color = i % 3 == 0 ? new Color("#ad7045") : i % 3 == 1
                ? new Color("#8c5938") : SiteVisuals.Soil;
            var chunk = SiteVisuals.Sphere(this, size,
                new Vector3(Mathf.Cos(angle) * radius, .25f + size * .55f, Mathf.Sin(angle) * radius), color);
            chunk.Scale = new Vector3(1.2f, .72f, 1.1f);
            _chunks.Add(chunk);
        }
        SetRemaining(_capacity);
    }

    public bool Contains(Vector3 groundPoint)
    {
        var local = ToLocal(groundPoint);
        return new Vector2(local.X, local.Z).Length() <= DigRadius + .35f;
    }

    public void SetRemaining(int count)
    {
        VisibleLoads = Mathf.Clamp(count, 0, _chunks.Count);
        for (var i = 0; i < _chunks.Count; i++) _chunks[i].Visible = i < VisibleLoads;
    }

    public void SetHighlighted(bool highlighted) => _highlight.Visible = highlighted;
}
