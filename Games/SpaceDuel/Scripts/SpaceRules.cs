using System;

namespace GameBox.Games.SpaceDuel;

public static class SpaceRules
{
    public const int WinningKills = 10;
    public const int ShipHealth = 8;
    public const int LaserDamage = 1;
    public const int TorpedoDamage = 4;
    public const float MaximumEnergy = 100f;
    public const float EnergyRegenPerSecond = 19f;
    public const float LaserEnergyCost = 4.5f;
    public const float LaserSpeed = 912f;
    public const float TorpedoEnergyCost = 32f;
    public const float ThrustEnergyPerSecond = 32f;
    public const float BrakeEnergyPerSecond = 22f;
    public const float ThrustAcceleration = 250f;
    public const float BrakeAcceleration = ThrustAcceleration * .5f;
    public const float TurnSpeed = 3.1f;
    public const float MaximumShipSpeed = 520f;
    public const float ShipRadius = 22f;
    public const float TorpedoLaunchSpeed = 225f;
    public const float TorpedoAcceleration = 190f;
    public const float TorpedoMaximumSpeed = 500f;
    public const float TorpedoTurnSpeed = 7f;
    public const float TorpedoFuelSeconds = 3.84f;
    public const float TorpedoCoastSeconds = 1.6f;

    public static float Wrap(float position, float extent)
    {
        if (extent <= 0f) throw new ArgumentOutOfRangeException(nameof(extent));
        return ((position % extent) + extent) % extent;
    }

    public static float WrappedDelta(float from, float to, float extent)
    {
        if (extent <= 0f) throw new ArgumentOutOfRangeException(nameof(extent));
        var delta = Wrap(to - from, extent);
        return delta > extent * .5f ? delta - extent : delta;
    }
}

public sealed class EnergyReserve
{
    public float Current { get; private set; } = SpaceRules.MaximumEnergy;
    public float TotalSpent { get; private set; }

    public void Recharge(float delta)
    {
        if (delta < 0f) throw new ArgumentOutOfRangeException(nameof(delta));
        Current = Math.Min(SpaceRules.MaximumEnergy, Current + SpaceRules.EnergyRegenPerSecond * delta);
    }

    public bool TrySpend(float cost)
    {
        if (cost < 0f) throw new ArgumentOutOfRangeException(nameof(cost));
        if (Current < cost) return false;
        Current -= cost;
        TotalSpent += cost;
        return true;
    }

    // Continuous controls can use the fraction of a frame that the reserve can pay for.
    public float SpendContinuous(float costPerSecond, float delta)
    {
        if (costPerSecond < 0f) throw new ArgumentOutOfRangeException(nameof(costPerSecond));
        if (delta < 0f) throw new ArgumentOutOfRangeException(nameof(delta));
        var requested = costPerSecond * delta;
        if (requested <= 0f) return 1f;
        var paid = Math.Min(Current, requested);
        Current -= paid;
        TotalSpent += paid;
        return paid / requested;
    }

    public void Refill() => Current = SpaceRules.MaximumEnergy;
}

public sealed class DuelScore
{
    private readonly int[] _kills = new int[2];
    public int Winner { get; private set; } = -1;
    public int Kills(int player) => _kills[player];

    public bool RecordKill(int player)
    {
        if (player is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(player));
        if (Winner >= 0) return false;
        _kills[player]++;
        if (_kills[player] < SpaceRules.WinningKills) return false;
        Winner = player;
        return true;
    }
}
