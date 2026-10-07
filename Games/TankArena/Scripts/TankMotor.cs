using Godot;

namespace ChristiansSpilBox.Games.TankArena;

public sealed class TankMotor
{
    private float _speed;

    public void Step(TankUnit tank, float throttle, float turn, double delta)
    {
        var tune = tank.Tuning;
        tank.RotateY(turn * tune.TurnSpeed * (float)delta);
        var target = throttle >= 0f ? throttle * tune.ForwardSpeed : throttle * tune.ReverseSpeed;
        _speed = Mathf.MoveToward(_speed, target, tune.Acceleration * (float)delta);
        tank.Velocity = -tank.GlobalBasis.Z * _speed;
        tank.MoveAndSlide();
    }
}
