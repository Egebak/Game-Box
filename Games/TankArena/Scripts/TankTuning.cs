namespace ChristiansSpilBox.Games.TankArena;

public sealed class TankTuning
{
    public int Health { get; init; } = 5;
    public float ForwardSpeed { get; init; } = 11f;
    public float ReverseSpeed { get; init; } = 5f;
    public float Acceleration { get; init; } = 16f;
    public float TurnSpeed { get; init; } = 2.3f;
    public float TurretSpeed { get; init; } = 3.2f;
    public float ReloadSeconds { get; init; } = 1.7f;
    public float ShellSpeed { get; init; } = 14f;
    public float Scale { get; init; } = 1f;
    public int ShellDamage { get; init; } = 1;
}
