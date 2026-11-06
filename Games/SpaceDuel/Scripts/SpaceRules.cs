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
    public const float TorpedoTurnSpeed = 14f;
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

    // Return an unwrapped offset to the earliest reachable intercept of a coasting ship.
    // Each neighboring copy of the target is considered because the arena wraps.
    public static (float X, float Y) TorpedoInterceptOffset(
        float missileX, float missileY, float missileSpeed, float fuel,
        float targetX, float targetY, float targetVelocityX, float targetVelocityY,
        float width, float height)
    {
        var fallbackX = WrappedDelta(missileX, targetX, width);
        var fallbackY = WrappedDelta(missileY, targetY, height);
        var bestTime = float.PositiveInfinity;
        var aimX = fallbackX;
        var aimY = fallbackY;
        var flightTime = Math.Max(0f, fuel) + TorpedoCoastSeconds;
        const float sampleSeconds = .08f;

        for (var imageX = -1; imageX <= 1; imageX++)
        for (var imageY = -1; imageY <= 1; imageY++)
        {
            var offsetX = targetX + imageX * width - missileX;
            var offsetY = targetY + imageY * height - missileY;
            var previousTime = 0f;
            for (var time = sampleSeconds; time <= flightTime + sampleSeconds; time += sampleSeconds)
            {
                var currentTime = Math.Min(time, flightTime);
                var separationX = offsetX + targetVelocityX * currentTime;
                var separationY = offsetY + targetVelocityY * currentTime;
                var travel = TorpedoTravelDistance(missileSpeed, fuel, currentTime);
                if (separationX * separationX + separationY * separationY > travel * travel)
                {
                    previousTime = currentTime;
                    continue;
                }

                var low = previousTime;
                var high = currentTime;
                for (var refinement = 0; refinement < 8; refinement++)
                {
                    var middle = (low + high) * .5f;
                    var x = offsetX + targetVelocityX * middle;
                    var y = offsetY + targetVelocityY * middle;
                    var distance = TorpedoTravelDistance(missileSpeed, fuel, middle);
                    if (x * x + y * y > distance * distance) low = middle;
                    else high = middle;
                }
                if (high < bestTime)
                {
                    bestTime = high;
                    aimX = offsetX + targetVelocityX * high;
                    aimY = offsetY + targetVelocityY * high;
                }
                break;
            }
        }
        return (aimX, aimY);
    }

    private static float TorpedoTravelDistance(float speed, float fuel, float seconds)
    {
        speed = Math.Clamp(speed, 0f, TorpedoMaximumSpeed);
        var powered = Math.Min(seconds, Math.Max(0f, fuel));
        var accelerating = Math.Min(powered, (TorpedoMaximumSpeed - speed) / TorpedoAcceleration);
        var distance = speed * accelerating + .5f * TorpedoAcceleration * accelerating * accelerating;
        var coastSpeed = speed + TorpedoAcceleration * accelerating;
        distance += coastSpeed * (seconds - accelerating);
        return distance;
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
