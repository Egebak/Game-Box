using GameBox.Shared.Audio;
using Godot;

namespace GameBox.Games.TankArena;

// The arena owns this value and resets it when leaving the scene.
public static class TankAudio
{
    public static float ZoomAttenuationDb { get; private set; }

    public static void SetZoom(float fraction)
        => ZoomAttenuationDb = -16f * Mathf.Clamp(fraction, 0f, 1f);

    public static void Play(string id, float volumeDb = -10f, float pitch = 1f)
        => SoundEffects.Play(id, volumeDb + ZoomAttenuationDb, pitch);
}
