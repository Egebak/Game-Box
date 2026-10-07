using System;
using GameBox.Shared.Audio;
using Godot;

namespace GameBox.Games.TankArena;

public partial class EnemyBrain : Node
{
    public TankUnit Tank { get; set; } = null!;
    public TankUnit Target { get; set; } = null!;
    [Export] public float PreferredDistance = 14f;
    [Export] public float WarningSeconds = 0.95f;
    [Export] public float AimErrorRadians = 0.10f;

    private float _warningRemaining = -1f;
    private Vector3 _aimPoint;
    private readonly Random _random = new();

    public override void _PhysicsProcess(double delta)
    {
        if (Tank.IsDestroyed || Target.IsDestroyed) return;
        var toward = Target.GlobalPosition - Tank.GlobalPosition;
        toward.Y = 0;
        var distance = toward.Length();
        if (distance < 0.01f) return;
        var desired = toward / distance;
        var throttle = distance > PreferredDistance + 3f ? 0.75f : distance < PreferredDistance - 5f ? -0.5f : 0f;

        var query = PhysicsRayQueryParameters3D.Create(Tank.GlobalPosition, Tank.GlobalPosition - Tank.GlobalBasis.Z * 4.4f, 1);
        var blocked = GetViewport().World3D.DirectSpaceState.IntersectRay(query).Count > 0;
        if (blocked)
        {
            desired = desired.Rotated(Vector3.Up, Tank.GetInstanceId() % 2 == 0 ? 0.95f : -0.95f);
            throttle = 0.65f;
        }
        var cross = (-Tank.GlobalBasis.Z).Cross(desired).Y;
        var turn = Mathf.Clamp(cross * 3f, -1f, 1f);
        Tank.Drive(throttle, turn, delta);

        if (_warningRemaining < 0f && Tank.Weapon.ReloadFraction >= 1f && distance < 27f)
        {
            _warningRemaining = WarningSeconds;
            SoundEffects.Play("enemy_warning", -17f);
            var angle = (float)(_random.NextDouble() * 2 - 1) * AimErrorRadians;
            _aimPoint = Tank.GlobalPosition + toward.Rotated(Vector3.Up, angle);
        }
        if (_warningRemaining >= 0f)
        {
            _warningRemaining -= (float)delta;
            Tank.SetWarning((int)(_warningRemaining * 8f) % 2 == 0);
            Tank.AimAt(_aimPoint, delta);
            if (_warningRemaining < 0f)
            {
                Tank.SetWarning(false);
                Tank.Weapon.TryFire();
            }
        }
        else Tank.AimAt(Target.GlobalPosition, delta);
    }
}
