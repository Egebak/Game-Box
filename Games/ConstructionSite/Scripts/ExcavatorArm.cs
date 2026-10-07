using Godot;

namespace ChristiansSpilBox.Games.ConstructionSite;

public partial class ExcavatorArm : Node3D
{
    [Export] public float ActionSeconds = .82f;

    private Node3D _boom = null!;
    private Node3D _stick = null!;
    private Node3D _bucket = null!;
    private MeshInstance3D _cargo = null!;
    private Marker3D _tip = null!;
    private float _actionTime;
    private bool _digging;
    private bool _loaded;

    public bool Busy => _actionTime > 0f;
    public bool CargoVisible => _cargo.Visible;
    public Vector3 CargoPosition => _cargo.GlobalPosition;
    public Vector3 BucketTip => _tip.GlobalPosition;

    public override void _Ready()
    {
        _boom = new Node3D { Position = new Vector3(0, .27f, -.63f) };
        AddChild(_boom);
        SiteVisuals.Box(_boom, new Vector3(.42f, .43f, 2.35f), new Vector3(0, 0, -1.17f), SiteVisuals.Yellow);
        SiteVisuals.Cylinder(_boom, .31f, .62f, Vector3.Zero, SiteVisuals.Dark).Rotation = new Vector3(0, 0, Mathf.Pi / 2f);

        _stick = new Node3D { Position = new Vector3(0, 0, -2.25f) };
        _boom.AddChild(_stick);
        SiteVisuals.Box(_stick, new Vector3(.34f, .34f, 1.55f), new Vector3(0, 0, -.76f), SiteVisuals.Orange);
        SiteVisuals.Cylinder(_stick, .25f, .5f, Vector3.Zero, SiteVisuals.Dark).Rotation = new Vector3(0, 0, Mathf.Pi / 2f);

        _bucket = new Node3D { Position = new Vector3(0, 0, -1.55f) };
        _stick.AddChild(_bucket);
        SiteVisuals.Box(_bucket, new Vector3(1f, .14f, .88f), new Vector3(0, -.34f, -.35f), SiteVisuals.Dark);
        SiteVisuals.Box(_bucket, new Vector3(.13f, .55f, .88f), new Vector3(-.44f, -.12f, -.35f), SiteVisuals.Dark);
        SiteVisuals.Box(_bucket, new Vector3(.13f, .55f, .88f), new Vector3(.44f, -.12f, -.35f), SiteVisuals.Dark);
        SiteVisuals.Box(_bucket, new Vector3(.87f, .45f, .14f), new Vector3(0, -.16f, .02f), SiteVisuals.Dark);
        _cargo = SiteVisuals.Sphere(_bucket, .37f, new Vector3(0, -.1f, -.36f), SiteVisuals.Soil);
        _cargo.Visible = false;
        _tip = new Marker3D { Position = new Vector3(0, -.28f, -.8f) };
        _bucket.AddChild(_tip);
        ApplyPose(-.08f, -.12f, -.08f);
    }

    public void SetLoaded(bool loaded)
    {
        _loaded = loaded;
        _cargo.Visible = loaded;
    }

    public void PlayDig() { _digging = true; _actionTime = ActionSeconds; }
    public void PlayDump() { _digging = false; _actionTime = ActionSeconds; }

    public override void _Process(double delta)
    {
        if (_actionTime <= 0f)
        {
            var targetBoom = _loaded ? .25f : -.08f;
            ApplyPose(Mathf.Lerp(_boom.Rotation.X, targetBoom, (float)delta * 7f),
                Mathf.Lerp(_stick.Rotation.X, _loaded ? -.2f : -.12f, (float)delta * 7f),
                Mathf.Lerp(_bucket.Rotation.X, _loaded ? .16f : -.08f, (float)delta * 7f));
            return;
        }

        _actionTime = Mathf.Max(0f, _actionTime - (float)delta);
        var progress = 1f - _actionTime / ActionSeconds;
        if (_digging)
        {
            var stroke = Mathf.Sin(Mathf.Pi * progress);
            ApplyPose(Mathf.Lerp(-.08f, .25f, progress) - .28f * stroke,
                -.12f - .28f * stroke,
                -.08f + .72f * progress);
        }
        else
        {
            var tip = Mathf.Sin(Mathf.Pi * progress);
            ApplyPose(.25f + .14f * tip, -.2f + .18f * tip, .16f - 1.35f * tip);
        }
    }

    private void ApplyPose(float boom, float stick, float bucket)
    {
        _boom.Rotation = new Vector3(boom, 0, 0);
        _stick.Rotation = new Vector3(stick, 0, 0);
        _bucket.Rotation = new Vector3(bucket, 0, 0);
    }
}
