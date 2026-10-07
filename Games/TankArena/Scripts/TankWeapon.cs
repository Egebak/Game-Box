using System;
using System.Collections.Generic;
using GameBox.Shared.Audio;
using Godot;

namespace GameBox.Games.TankArena;

public partial class TankWeapon : Node3D
{
    private Marker3D[] _muzzles = null!;
    private ShotMagazine _magazine = null!;
    private readonly Dictionary<int, int> _remainingShellsByVolley = new();
    private int _nextVolleyId;
    public TankUnit Tank { get; set; } = null!;
    public int ReadyCount => _magazine.ReadyCount;
    public int InFlightCount => _magazine.InFlightCount;
    public int Capacity => _magazine.Capacity;
    public int BarrelCount => _muzzles.Length;
    public float ReloadFraction => _magazine.NextChargeFraction;
    public event Action<Vector3, Vector3, TankUnit, int, int>? Fired;

    public override void _Ready()
    {
        var offsets = Tank.IsBoss ? new[] { -0.38f, 0.38f } : new[] { 0f };
        _muzzles = new Marker3D[offsets.Length];
        for (var i = 0; i < offsets.Length; i++)
        {
            _muzzles[i] = new Marker3D { Position = new Vector3(offsets[i], -0.05f, -1.8f) };
            AddChild(_muzzles[i]);
        }
        _magazine = new ShotMagazine(Tank.IsPlayer ? 3 : 1, Tank.Tuning.ReloadSeconds, Tank.IsPlayer ? .32f : 0f);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_magazine.Tick((float)delta) > 0 && Tank.IsPlayer && !Tank.IsDestroyed)
            SoundEffects.Play("reload_ready", -13f);
    }

    public bool TryFire()
    {
        if (Tank.IsDestroyed || !_magazine.TryFire()) return false;
        var volleyId = ++_nextVolleyId;
        _remainingShellsByVolley[volleyId] = _muzzles.Length;
        for (var i = 0; i < _muzzles.Length; i++)
            Fired?.Invoke(_muzzles[i].GlobalPosition, -_muzzles[i].GlobalBasis.Z, Tank, volleyId, i);
        return true;
    }

    public void Track(ShellProjectile shell, int volleyId) => shell.Ended += _ =>
    {
        if (!_remainingShellsByVolley.TryGetValue(volleyId, out var remaining)) return;
        if (remaining > 1) _remainingShellsByVolley[volleyId] = remaining - 1;
        else
        {
            _remainingShellsByVolley.Remove(volleyId);
            _magazine.ShellEnded();
        }
    };
}
