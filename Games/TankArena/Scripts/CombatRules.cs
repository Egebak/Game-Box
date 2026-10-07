using System;
using System.Collections.Generic;

namespace GameBox.Games.TankArena;

public sealed class HealthModel
{
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
