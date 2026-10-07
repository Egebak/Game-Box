using System;
using System.IO;
using System.Linq;
using ChristiansSpilBox.Core;
using ChristiansSpilBox.Games.ConstructionSite;
using ChristiansSpilBox.Shared.Audio;
using Godot;

public partial class ConstructionSmoke : Node
{
    public override void _Ready() => CallDeferred(nameof(Run));

    private async void Run()
    {
        var path = ProjectSettings.GlobalizePath("user://game_box_state.json");
        var oldSave = File.Exists(path) ? File.ReadAllText(path) : null;
        try
        {
            ProcessMode = ProcessModeEnum.Always;
            var site = GD.Load<PackedScene>("res://Games/ConstructionSite/Scenes/ConstructionSite.tscn")
                .Instantiate<ConstructionSite>();
            AddChild(site);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(SoundEffects.AllIds.All(id => SoundEffects.Get(id) is not null), "construction sound hooks load");
            Check(site.FindChildren("*", "Button", true, false).OfType<Button>()
                .Any(x => x.Text == "Opgave: Fyld lastbilen"), "mission appears in construction menu");
            Check(site.FindChildren("*", "Button", true, false).OfType<Button>()
                .Any(x => x.Text == "Fri leg"), "free play appears in construction menu");
            site.StartMode(false);
            Check(site.Mission.RequiredLoads == 8, "mission requires eight loads");
            var start = site.Player.GlobalPosition;
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.W, Pressed = true });
            for (var i = 0; i < 20; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.W, Pressed = false });
            Check(site.Player.GlobalPosition.DistanceTo(start) > .3f, "W drives excavator");

            GetViewport().PushInput(new InputEventKey { Keycode = Key.Escape, Pressed = true });
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(GetTree().Paused, "Esc pauses construction mission");
            GetViewport().PushInput(new InputEventKey { Keycode = Key.Escape, Pressed = false });
            GetViewport().PushInput(new InputEventKey { Keycode = Key.Escape, Pressed = true });
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(!GetTree().Paused, "Esc resumes construction mission");
            GetViewport().PushInput(new InputEventKey { Keycode = Key.Escape, Pressed = false });

            var pilePoint = site.Pile.GlobalPosition;
            var truckPoint = site.Truck.BedCenter;
            var camera = site.GetChildren().OfType<Camera3D>().Single();
            site.Player.GlobalPosition = pilePoint + new Vector3(0, 0, 5);
            Check(site.TryInteractAt(pilePoint), "a spare load can be scooped");
            Check(site.Truck.DeliveryRingVisible, "large red truck circle appears with a full bucket");
            await ToSignal(GetTree().CreateTimer(.9), SceneTreeTimer.SignalName.Timeout);
            Check(site.TryGroundDumpAt(site.Player.GlobalPosition + Vector3.Forward * 2f),
                "right click may dump soil on the ground");
            Check(site.Mission.Delivered == 0 && site.GetChildren().Any(x => x.Name == "DroppedSoil"),
                "ground dump is visible and does not block mission");
            Check(!site.Truck.DeliveryRingVisible, "truck circle hides when bucket is empty");
            await ToSignal(GetTree().CreateTimer(.9), SceneTreeTimer.SignalName.Timeout);
            for (var load = 1; load <= 8; load++)
            {
                site.Player.GlobalPosition = pilePoint + new Vector3(0, 0, 5);
                site.Player.AimAt(pilePoint, 1);
                var before = site.Pile.VisibleLoads;
                if (load == 1)
                {
                    var screen = camera.UnprojectPosition(pilePoint + Vector3.Up);
                    GetViewport().PushInput(new InputEventMouseButton
                    {
                        ButtonIndex = MouseButton.Left, Pressed = true, Position = screen
                    }, true);
                    Check(site.Mission.BucketLoaded, "screen click scoops visible soil");
                }
                else Check(site.TryInteractAt(pilePoint), $"scoop {load} succeeds near pile");
                Check(site.Player.CargoVisible && site.Pile.VisibleLoads != before,
                    $"scoop {load} transfers visible soil into bucket");
                Check(site.Truck.DeliveryRingVisible, $"load {load} highlights truck with red circle");
                await ToSignal(GetTree().CreateTimer(.9), SceneTreeTimer.SignalName.Timeout);
                site.Player.GlobalPosition = new Vector3(truckPoint.X - 5, 0, truckPoint.Z);
                if (load == 1)
                {
                    site.Player.AimAt(pilePoint, 1);
                    Check(!site.Truck.IsCargoOverBed(site.Player.CargoPosition),
                        "bucket points away from truck bed");
                    Check(!site.TryInteractAt(truckPoint) && site.Mission.BucketLoaded,
                        "clicking truck cannot unload a bucket beside the bed");
                }
                site.Player.AimAt(truckPoint, 1);
                Check(site.Truck.IsCargoOverBed(site.Player.CargoPosition),
                    $"bucket cargo is physically over truck bed for load {load}");
                if (load == 1)
                {
                    var screen = camera.UnprojectPosition(pilePoint);
                    GetViewport().PushInput(new InputEventMouseButton
                    {
                        ButtonIndex = MouseButton.Left, Pressed = true, Position = screen
                    }, true);
                    Check(!site.Mission.BucketLoaded, "screen click unloads when bucket is over bed");
                }
                else Check(site.TryInteractAt(pilePoint), $"dump {load} follows bucket position");
                Check(!site.Player.CargoVisible && site.Truck.VisibleLoads == load,
                    $"load {load} appears in truck bed");
                Check(!site.Truck.DeliveryRingVisible, $"red circle hides after load {load}");
                await ToSignal(GetTree().CreateTimer(.9), SceneTreeTimer.SignalName.Timeout);
            }
            Check(site.Mission.IsComplete, "eight loads complete mission");
            await ToSignal(GetTree().CreateTimer(1.1), SceneTreeTimer.SignalName.Timeout);
            Check(site.Truck.IsDeparting, "full truck drives away");
            await ToSignal(GetTree().CreateTimer(3.3), SceneTreeTimer.SignalName.Timeout);
            Check(site.SuccessVisible && GetTree().Paused, "Færdig screen appears after truck leaves");
            Check(GameBoxState.Load(path).ConstructionSiteCompleted, "mission completion saved");
            GetTree().Paused = false;
            site.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            var freeSite = GD.Load<PackedScene>("res://Games/ConstructionSite/Scenes/ConstructionSite.tscn")
                .Instantiate<ConstructionSite>();
            freeSite.RequiredLoads = 1;
            freeSite.SoilPileCapacity = 2;
            AddChild(freeSite);
            freeSite.StartMode(true);
            Check(freeSite.Mission.FreePlay && !freeSite.SuccessVisible, "free play starts without win state");
            for (var load = 1; load <= 3; load++)
            {
                freeSite.Player.GlobalPosition = freeSite.Pile.GlobalPosition + new Vector3(0, 0, 5);
                Check(freeSite.TryInteractAt(freeSite.Pile.GlobalPosition), $"free play scoops load {load}");
                await ToSignal(GetTree().CreateTimer(.9), SceneTreeTimer.SignalName.Timeout);
                var bed = freeSite.Truck.BedCenter;
                freeSite.Player.GlobalPosition = new Vector3(bed.X - 5, 0, bed.Z);
                freeSite.Player.AimAt(bed, 1);
                Check(freeSite.TryInteractAt(bed), $"free play dumps load {load}");
                await ToSignal(GetTree().CreateTimer(.9), SceneTreeTimer.SignalName.Timeout);
            }
            Check(freeSite.Mission.Delivered == 3 && freeSite.Pile.VisibleLoads == 1 &&
                !freeSite.SuccessVisible && !freeSite.Truck.IsDeparting,
                "free play continues after truck fills");
            freeSite.QueueFree();
            await ToSignal(GetTree().CreateTimer(1.6), SceneTreeTimer.SignalName.Timeout);
            GD.Print("CONSTRUCTION_SMOKE_PASS");
            GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PushError($"CONSTRUCTION_SMOKE_FAIL: {error}");
            GetTree().Quit(1);
        }
        finally
        {
            if (oldSave is null) File.Delete(path);
            else File.WriteAllText(path, oldSave);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        GD.Print($"PASS: {message}");
    }
}
