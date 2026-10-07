using System;
using System.IO;
using ChristiansSpilBox.Core;
using ChristiansSpilBox.Games.TankArena;
using ChristiansSpilBox.Games.ConstructionSite;

var count = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception($"FAIL: {name}");
    Console.WriteLine($"PASS: {name}");
    count++;
}

var health = new HealthModel(3);
Check(health.Current == 3 && !health.IsDead, "health starts full");
Check(!health.Damage(1) && health.Current == 2, "damage reduces health");
Check(health.Damage(5) && health.Current == 0, "damage clamps at zero and destroys");
Check(!health.Damage(1) && health.Current == 0, "dead tank cannot take more damage");

var progress = new ArenaProgress(10);
for (var i = 0; i < 9; i++) Check(!progress.RecordNormalKill(), $"kill {i + 1} does not summon boss");
Check(progress.RecordNormalKill() && progress.Kills == 10 && progress.BossActivated, "tenth kill summons boss");
Check(!progress.RecordNormalKill() && progress.Kills == 10, "extra kill does not exceed target");
progress.RecordBossKill();
Check(progress.Won, "boss defeat wins after activation");
var earlyBoss = new ArenaProgress(2);
earlyBoss.RecordBossKill();
Check(!earlyBoss.Won, "boss defeat cannot win before activation");

var magazine = new ShotMagazine(3, 1.7f, .32f);
Check(magazine.TryFire() && magazine.ReadyCount == 2 && magazine.InFlightCount == 1,
    "first shell consumes one charge");
Check(!magazine.TryFire(), "short interval prevents continuous fire");
magazine.Tick(.32f);
Check(magazine.TryFire(), "second shell can fly while first is airborne");
magazine.Tick(.32f);
Check(magazine.TryFire() && magazine.InFlightCount == 3, "three shells can fly together");
magazine.Tick(2f);
Check(magazine.ReadyCount == 3 && !magazine.TryFire(), "flight cap holds even after charges recover");
magazine.ShellEnded();
Check(magazine.TryFire() && magazine.InFlightCount == 3, "impact frees one flight slot");
magazine.ShellEnded();
magazine.ShellEnded();
magazine.ShellEnded();
Check(magazine.InFlightCount == 0, "flight count never goes below zero");

var registry = MiniGameRegistry.CreateDefault();
Check(registry.Get("tank-arena").ScenePath.EndsWith("TankArena.tscn"), "registered game has entry scene");
Check(registry.Get("construction-site").ScenePath.EndsWith("ConstructionSite.tscn"),
    "construction minigame has entry scene");
try { registry.Register(registry.Get("tank-arena")); throw new Exception("Duplicate was accepted"); }
catch (ArgumentException) { Check(true, "duplicate minigame is rejected"); }

var savePath = Path.Combine(Path.GetTempPath(), "christians-game-box-tests", "state.json");
Check(!GameBoxState.Load(savePath).TankArenaCompleted, "missing save defaults safely");
var state = new GameBoxState { TankArenaCompleted = true };
state.ConstructionSiteCompleted = true;
state.Save(savePath);
Check(GameBoxState.Load(savePath).TankArenaCompleted, "completion round trips through save file");
Check(GameBoxState.Load(savePath).ConstructionSiteCompleted, "construction completion uses shared save file");
Check(!GameBoxState.FromJson("not json").TankArenaCompleted, "invalid save defaults safely");
var mission = new ConstructionMission(8, 3);
Check(mission.TryDig() && mission.BucketLoaded && mission.SoilRemaining == 2, "dig moves one load into bucket");
Check(!mission.TryDig(), "full bucket cannot dig twice");
Check(mission.Dump(false) == DumpResult.Ground && mission.Delivered == 0, "ground dump does not advance mission");
for (var load = 1; load <= 8; load++)
{
    Check(mission.TryDig(), $"load {load} can be scooped");
    var result = mission.Dump(true);
    Check(result == (load == 8 ? DumpResult.Completed : DumpResult.Truck) && mission.Delivered == load,
        $"truck records load {load}");
}
Check(mission.IsComplete && !mission.TryDig(), "mission completes at required load count");
var renewal = new ConstructionMission(2, 1);
Check(renewal.TryDig() && renewal.SoilRemaining == 0, "single load empties source pile");
renewal.Dump(false);
Check(renewal.RefillSourceIfEmpty() && renewal.SoilRemaining == 1,
    "empty source pile renews after a ground dump");
var freePlay = new ConstructionMission(2, 1, true);
for (var load = 0; load < 4; load++)
{
    Check(freePlay.TryDig() && freePlay.Dump(true) == DumpResult.Truck,
        $"free play accepts load {load + 1}");
}
Check(!freePlay.IsComplete && freePlay.Delivered == 4, "free play continues after truck is full");
File.Delete(savePath);
Console.WriteLine($"{count} checks passed.");
