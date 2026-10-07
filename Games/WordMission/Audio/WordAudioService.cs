using System.Collections.Generic;
using GameBox.Games.WordMission.Learning;
using Godot;

namespace GameBox.Games.WordMission.Audio;

public partial class WordAudioService : Node
{
    private readonly List<string> _warnings = new();
    private AudioStreamPlayer _player = null!;
    public IReadOnlyList<string> Warnings => _warnings;
    public bool Enabled { get; set; } = true;

    public override void _Ready()
    {
        _player = new AudioStreamPlayer();
        AddChild(_player);
    }

    public void Inspect(LearningContent content)
    {
        _warnings.Clear();
        Check(content.InstructionAudio, "instructionAudio");
        Check(content.SuccessAudio, "successAudio");
        foreach (var sound in content.Sounds) Check(sound.Audio, sound.Id);
        foreach (var letter in content.Letters) Check(letter.LetterNameAudio, letter.Id + " name");
        foreach (var word in content.Words) Check(word.WordAudio, word.Id);
        foreach (var warning in _warnings) GD.PushWarning("Ordmission audio: " + warning);
    }

    public void PlaySound(SoundDefinition sound) => Play(sound.Audio);
    public void PlayLetterName(LetterDefinition letter) => Play(letter.LetterNameAudio);
    public void PlayWord(WordDefinition word) => Play(word.WordAudio);
    public void PlayInstruction(LearningContent content) => Play(content.InstructionAudio);
    public void PlaySuccess(LearningContent content) => Play(content.SuccessAudio);

    private void Check(string? path, string description)
    {
        if (string.IsNullOrWhiteSpace(path)) _warnings.Add($"{description}: no recording configured");
        else if (!ResourceLoader.Exists(path)) _warnings.Add($"{description}: missing {path}");
    }

    private void Play(string? path)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path)) return;
        var stream = GD.Load<AudioStream>(path);
        if (stream is null) return;
        _player.Stream = stream;
        _player.Play();
    }
}
