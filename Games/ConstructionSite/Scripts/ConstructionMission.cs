using System;

namespace ChristiansSpilBox.Games.ConstructionSite;

public enum DumpResult { Empty, Ground, Truck, Completed }

public sealed class ConstructionMission
{
    public int RequiredLoads { get; }
    public int PileCapacity { get; }
    public int SoilRemaining { get; private set; }
    public int Delivered { get; private set; }
    public bool BucketLoaded { get; private set; }
    public bool FreePlay { get; }
    public bool IsComplete => !FreePlay && Delivered >= RequiredLoads;

    public ConstructionMission(int requiredLoads, int pileCapacity, bool freePlay = false)
    {
        if (requiredLoads < 1 || pileCapacity < 1) throw new ArgumentOutOfRangeException();
        RequiredLoads = requiredLoads;
        PileCapacity = pileCapacity;
        SoilRemaining = pileCapacity;
        FreePlay = freePlay;
    }

    public bool TryDig()
    {
        if (BucketLoaded || IsComplete) return false;
        if (SoilRemaining == 0) SoilRemaining = PileCapacity;
        SoilRemaining--;
        BucketLoaded = true;
        return true;
    }

    public bool RefillSourceIfEmpty()
    {
        if (SoilRemaining > 0) return false;
        SoilRemaining = PileCapacity;
        return true;
    }

    public DumpResult Dump(bool inTruck)
    {
        if (!BucketLoaded || IsComplete) return DumpResult.Empty;
        BucketLoaded = false;
        if (!inTruck) return DumpResult.Ground;
        Delivered++;
        return IsComplete ? DumpResult.Completed : DumpResult.Truck;
    }
}
