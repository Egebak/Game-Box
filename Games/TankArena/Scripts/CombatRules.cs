using System;
using System.Collections.Generic;

namespace GameBox.Games.TankArena;

public sealed class HealthModel
{
    private int _damageFifths;
    public int Maximum { get; }
    public int Current { get; private set; }
    public bool IsDead => Current <= 0;

    public HealthModel(int maximum)
    {
        if (maximum < 1) throw new ArgumentOutOfRangeException(nameof(maximum));
        Maximum = maximum;
        Current = maximum;
    }

    public bool Damage(int amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (IsDead) return false;
        Current = Math.Max(0, Current - amount);
        return IsDead;
    }

    public bool DamageFifths(int fifths)
    {
        if (fifths < 0) throw new ArgumentOutOfRangeException(nameof(fifths));
        if (IsDead) return false;
        _damageFifths += fifths;
        var wholeDamage = _damageFifths / 5;
        _damageFifths %= 5;
        return Damage(wholeDamage);
    }

    public int Heal(int amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (IsDead) return 0;
        var restored = Math.Min(amount, Maximum - Current);
        Current += restored;
        return restored;
    }
}

public enum PowerUpKind { Health, Missile, Minigun, Nuke, Mine }

public static class PowerUpDropRules
{
    public const double Chance = 0.4;

    public static PowerUpKind? Roll(Random random, double chance = Chance)
    {
        if (chance < 0 || chance > 1) throw new ArgumentOutOfRangeException(nameof(chance));
        if (random.NextDouble() >= chance) return null;
        return (PowerUpKind)random.Next(5);
    }
}

public sealed class MinigunTimer
{
    public const float DurationSeconds = 15f;
    public float SecondsRemaining { get; private set; }
    public bool Active => SecondsRemaining > 0f;

    public void Activate() => SecondsRemaining = DurationSeconds;

    public void Tick(float delta)
    {
        if (delta < 0f) throw new ArgumentOutOfRangeException(nameof(delta));
        SecondsRemaining = Math.Max(0f, SecondsRemaining - delta);
    }
}

public static class NukeRules
{
    public const float TankLength = 2.55f;
    public const float BlastRadius = 4f * TankLength;
    public static bool InBlast(float dx, float dz) => dx * dx + dz * dz <= BlastRadius * BlastRadius;
}

public sealed class MineSupply
{
    public const int PickupCount = 10;
    public const float DurationSeconds = 15f;
    public int Remaining { get; private set; }
    public float SecondsRemaining { get; private set; }
    public bool Active => Remaining > 0 && SecondsRemaining > 0f;

    public void Grant()
    {
        Remaining = PickupCount;
        SecondsRemaining = DurationSeconds;
    }

    public void Tick(float delta)
    {
        if (delta < 0f) throw new ArgumentOutOfRangeException(nameof(delta));
        SecondsRemaining = Math.Max(0f, SecondsRemaining - delta);
        if (SecondsRemaining <= 0f) Remaining = 0;
    }

    public bool TryPlace()
    {
        if (!Active) return false;
        Remaining--;
        return true;
    }
}

public sealed class ArenaProgress
{
    public int TargetKills { get; }
    public int Kills { get; private set; }
    public bool BossActivated { get; private set; }
    public bool Won { get; private set; }

    public ArenaProgress(int targetKills)
    {
        if (targetKills < 1) throw new ArgumentOutOfRangeException(nameof(targetKills));
        TargetKills = targetKills;
    }

    public bool RecordNormalKill()
    {
        if (BossActivated) return false;
        Kills = Math.Min(TargetKills, Kills + 1);
        BossActivated = Kills == TargetKills;
        return BossActivated;
    }

    public void RecordBossKill()
    {
        if (BossActivated) Won = true;
    }
}

// A charge starts reloading when fired. The separate flight limit prevents a
// recovered charge from putting more than Capacity shells in the world.
public sealed class ShotMagazine
{
    private readonly List<float> _recharge = new();
    private float _fireGate;

    public int Capacity { get; }
    public int ReadyCount => Capacity - _recharge.Count;
    public int InFlightCount { get; private set; }
    public float ReloadSeconds { get; }
    public float NextChargeFraction
    {
        get
        {
            if (ReadyCount == Capacity) return 1f;
            var soonest = float.MaxValue;
            foreach (var time in _recharge) soonest = Math.Min(soonest, time);
            return Math.Clamp(1f - soonest / ReloadSeconds, 0f, 1f);
        }
    }

    public ShotMagazine(int capacity, float reloadSeconds, float minimumShotInterval)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (reloadSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(reloadSeconds));
        if (minimumShotInterval < 0) throw new ArgumentOutOfRangeException(nameof(minimumShotInterval));
        Capacity = capacity;
        ReloadSeconds = reloadSeconds;
        MinimumShotInterval = minimumShotInterval;
    }

    public float MinimumShotInterval { get; }

    public int Tick(float delta)
    {
        if (delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        _fireGate = Math.Max(0f, _fireGate - delta);
        var restored = 0;
        for (var i = _recharge.Count - 1; i >= 0; i--)
        {
            _recharge[i] -= delta;
            if (_recharge[i] > 0) continue;
            _recharge.RemoveAt(i);
            restored++;
        }
        return restored;
    }

    public bool TryFire()
    {
        if (ReadyCount == 0 || InFlightCount >= Capacity || _fireGate > 0) return false;
        _recharge.Add(ReloadSeconds);
        _fireGate = MinimumShotInterval;
        InFlightCount++;
        return true;
    }

    public void ShellEnded()
    {
        if (InFlightCount > 0) InFlightCount--;
    }
}
