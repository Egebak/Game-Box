using System;
using System.Collections.Generic;
using GameBox.Shared.Audio;
using Godot;

namespace GameBox.Games.TankArena;

public partial class TankWeapon : Node3D
{
    private Marker3D[] _muzzles = null!;
    private ShotMagazine _normalMagazine = null!;
    private ShotMagazine _minigunMagazine = null!;
    private readonly MinigunTimer _minigunTimer = new();
    private readonly MineSupply _mines = new();
    private readonly NukeSupply _nukes = new();
    private readonly Dictionary<int, (int Remaining, ShotMagazine Magazine)> _remainingShellsByVolley = new();
    private int _nextVolleyId;
    public TankUnit Tank { get; set; } = null!;
    private ShotMagazine ActiveMagazine => IsMinigunActive ? _minigunMagazine : _normalMagazine;
    public bool IsMinigunActive => Tank.IsPlayer && _minigunTimer.Active;
    public float MinigunSecondsRemaining => _minigunTimer.SecondsRemaining;
    public int NukesRemaining => _nukes.Remaining;
    public float NukeSecondsRemaining => _nukes.SecondsRemaining;
    public int MinesRemaining => _mines.Remaining;
    public float MineSecondsRemaining => _mines.SecondsRemaining;
    public int ReadyCount => ActiveMagazine.ReadyCount;
    public int InFlightCount => ActiveMagazine.InFlightCount;
    public int Capacity => ActiveMagazine.Capacity;
    public int BarrelCount => _muzzles.Length;
    public float ReloadFraction => ActiveMagazine.NextChargeFraction;
    public event Action<Vector3, Vector3, TankUnit, int, int>? Fired;
    public event Action<Vector3, Vector3, TankUnit>? NukeFired;
    public event Action<Vector3, TankUnit>? MinePlaced;

    public override void _Ready()
    {
        var offsets = Tank.IsBoss ? new[] { -0.38f, 0.38f } : new[] { 0f };
        _muzzles = new Marker3D[offsets.Length];
        for (var i = 0; i < offsets.Length; i++)
        {
            _muzzles[i] = new Marker3D { Position = new Vector3(offsets[i], -0.05f, -1.8f) };
            AddChild(_muzzles[i]);
        }
        _normalMagazine = new ShotMagazine(Tank.IsPlayer ? 3 : 1, Tank.Tuning.ReloadSeconds, Tank.IsPlayer ? .32f : 0f);
        _minigunMagazine = new ShotMagazine(50, .18f, .025f);
    }

    public override void _PhysicsProcess(double delta)
    {
        _minigunTimer.Tick((float)delta);
        _nukes.Tick((float)delta);
        _mines.Tick((float)delta);
        _minigunMagazine.Tick((float)delta);
        if (_normalMagazine.Tick((float)delta) > 0 && Tank.IsPlayer && !Tank.IsDestroyed && !IsMinigunActive)
            TankAudio.Play("reload_ready", -13f);
    }

    public bool ActivateMinigun()
    {
        if (!Tank.IsPlayer || Tank.IsDestroyed) return false;
        _minigunTimer.Activate();
        return true;
    }

    public bool GrantNukes()
    {
        if (!Tank.IsPlayer || Tank.IsDestroyed) return false;
        _nukes.Grant();
        return true;
    }

    public bool TryFireNuke()
    {
        if (!Tank.IsPlayer || Tank.IsDestroyed || !_nukes.TryFire()) return false;
        NukeFired?.Invoke(_muzzles[0].GlobalPosition, -_muzzles[0].GlobalBasis.Z, Tank);
        return true;
    }

    public bool GrantMines()
    {
        if (!Tank.IsPlayer || Tank.IsDestroyed) return false;
        _mines.Grant();
        return true;
    }

    public bool TryPlaceMine()
    {
        if (!Tank.IsPlayer || Tank.IsDestroyed || !_mines.TryPlace()) return false;
        MinePlaced?.Invoke(Tank.GlobalPosition + Tank.GlobalBasis.Z * 2.3f, Tank);
        return true;
    }

    public bool TryFire()
    {
        var magazine = ActiveMagazine;
        if (Tank.IsDestroyed || !magazine.TryFire()) return false;
        var volleyId = ++_nextVolleyId;
        _remainingShellsByVolley[volleyId] = (_muzzles.Length, magazine);
        for (var i = 0; i < _muzzles.Length; i++)
            Fired?.Invoke(_muzzles[i].GlobalPosition, -_muzzles[i].GlobalBasis.Z, Tank, volleyId, i);
        return true;
    }

    public void Track(ShellProjectile shell, int volleyId) => shell.Ended += _ =>
    {
        if (!_remainingShellsByVolley.TryGetValue(volleyId, out var volley)) return;
        if (volley.Remaining > 1) _remainingShellsByVolley[volleyId] = (volley.Remaining - 1, volley.Magazine);
        else
        {
            _remainingShellsByVolley.Remove(volleyId);
            volley.Magazine.ShellEnded();
        }
    };
}
