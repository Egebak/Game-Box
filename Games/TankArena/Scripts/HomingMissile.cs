using System.Linq;
using Godot;

namespace GameBox.Games.TankArena;

public partial class HomingMissile : Node3D
{
    public TankUnit Target { get; set; } = null!;
    public int Damage { get; set; } = 3;

    private Vector3 _heading = Vector3.Forward;
    private float _life = 6f;
    private MeshInstance3D _body = null!;

    public override void _Ready()
    {
        _body = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = .08f, BottomRadius = .22f, Height = .95f },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color("#ffcb59"),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            }
        };
        AddChild(_body);
        _body.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = .25f, Height = .5f },
            Position = new Vector3(0, -.58f, 0),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color("#ff7045"),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            }
        });
        AddChild(new OmniLight3D { LightColor = new Color("#ff9e44"), LightEnergy = 1.6f, OmniRange = 3f });
    }

    public override void _PhysicsProcess(double delta)
    {
        _life -= (float)delta;
        if (_life <= 0f) { QueueFree(); return; }
        if (!GodotObject.IsInstanceValid(Target) || Target.IsDestroyed)
        {
            var next = GetParent().GetChildren().OfType<TankUnit>()
                .Where(tank => !tank.IsPlayer && !tank.IsDestroyed)
                .OrderBy(tank => tank.GlobalPosition.DistanceSquaredTo(GlobalPosition)).FirstOrDefault();
            if (next is null) { QueueFree(); return; }
            Target = next;
        }
        var destination = Target.GlobalPosition + Vector3.Up * 1.1f;
        var toward = destination - GlobalPosition;
        if (toward.Length() <= 1.2f + 20f * (float)delta)
        {
            Target.TakeDamage(Damage);
            var impact = new TankExplosion { RadiusScale = .55f };
            GetParent().AddChild(impact);
            impact.GlobalPosition = destination;
            TankAudio.Play("missile_impact", -7f);
            QueueFree();
            return;
        }
        var desired = toward.Normalized();
        _heading = _heading.Lerp(desired, Mathf.Clamp((float)delta * 11f, 0f, 1f)).Normalized();
        GlobalPosition += _heading * 20f * (float)delta;
        _body.Quaternion = new Quaternion(Vector3.Up, _heading);
    }
}
