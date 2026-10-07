using Godot;

namespace ChristiansSpilBox.Shared.Audio;

public static class SoundEffects
{
    public static readonly string[] AllIds =
    {
        "ui_hover", "ui_select", "ui_back", "pause", "resume",
        "player_fire", "enemy_fire", "boss_fire", "impact_wall", "impact_metal",
        "tank_hit", "tank_destroy", "tank_explosion", "boss_explosion",
        "engine_loop", "reload_ready", "enemy_warning", "boss_arrive", "victory", "defeat",
        "construction_dig", "construction_dump", "construction_load"
    };

    public static AudioStream? Get(string id)
        => GD.Load<AudioStream>($"res://Shared/Audio/{id}.wav");

    public static void Play(string id, float volumeDb = -10f, float pitch = 1f)
    {
        if (Engine.GetMainLoop() is not SceneTree tree || Get(id) is not { } stream) return;
        var player = new AudioStreamPlayer
        {
            Stream = stream, VolumeDb = volumeDb, PitchScale = pitch,
            ProcessMode = Node.ProcessModeEnum.Always
        };
        tree.Root.AddChild(player);
        void Cleanup()
        {
            if (!GodotObject.IsInstanceValid(player)) return;
            player.Stop();
            player.Stream = null;
            player.QueueFree();
        }
        player.Finished += Cleanup;
        // The timer also releases one-shots with a dummy/headless audio driver.
        tree.CreateTimer(stream.GetLength() + .15f).Timeout += Cleanup;
        player.Play();
    }

    public static AudioStreamPlayer? StartEngine(Node tank)
    {
        if (Get("engine_loop") is not { } stream) return null;
        var player = new AudioStreamPlayer
        {
            Stream = stream, VolumeDb = -29f, ProcessMode = Node.ProcessModeEnum.Pausable
        };
        if (stream is AudioStreamWav wav) wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
        tank.AddChild(player);
        player.Play();
        return player;
    }

    public static void BindHover(BaseButton button)
        => button.MouseEntered += () => Play("ui_hover", -19f);
}
