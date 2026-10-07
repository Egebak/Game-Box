using System;
using System.Linq;
using GameBox.Games.WordMission.Learning;
using GameBox.Shared.UI;
using Godot;

namespace GameBox.Games.WordMission.UI;

public partial class ParentView : Control
{
    public void Show(LearningContent content, WordMissionProgress progress,
        System.Collections.Generic.IReadOnlyList<string> audioWarnings,
        Action save, Action reset, Action back)
    {
        foreach (var child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
        var scroll = new ScrollContainer();
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(scroll);
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(690, 0) };
        column.AddThemeConstantOverride("separation", 14);
        scroll.AddChild(column);
        column.AddChild(UiStyle.Label("FOR VOKSNE", 39, UiStyle.Gold));
        column.AddChild(UiStyle.Label($"Sessioner: {progress.CompletedSessions} · Hjælp: {progress.AssistedAnswers}", 23));
        var introducedLetters = content.Letters.Count(letter => progress.Get(letter.Id).Attempts > 0);
        var masteredLetters = content.Letters.Count(letter => progress.Get(letter.Id).Mastery == MasteryState.Mastered);
        var introducedWords = content.Words.Count(word => progress.Get(word.Id).Attempts > 0);
        var masteredWords = content.Words.Count(word => progress.Get(word.Id).Mastery == MasteryState.Mastered);
        column.AddChild(UiStyle.Label($"Bogstaver: {introducedLetters} mødt, {masteredLetters} lært", 22));
        column.AddChild(UiStyle.Label($"Ord: {introducedWords} mødt, {masteredWords} lært", 22));
        column.AddChild(UiStyle.Label($"Åbnet område: {progress.UnlockedStage}", 22));
        var difficult = progress.Items.Where(pair => pair.Value.LastOutcomeDifficult)
            .OrderByDescending(pair => pair.Value.LastSeenUtc).Take(5)
            .Select(pair => pair.Key).ToArray();
        column.AddChild(UiStyle.Label("Øv gerne igen: " +
            (difficult.Length == 0 ? "ingen endnu" : string.Join(", ", difficult)), 19));

        var sound = new CheckButton { Text = "Lyd", ButtonPressed = progress.SoundEnabled };
        sound.AddThemeFontSizeOverride("font_size", 24);
        sound.Toggled += enabled => { progress.SoundEnabled = enabled; save(); };
        column.AddChild(sound);

        column.AddChild(UiStyle.Label("Bogstavvisning", 23, UiStyle.Sky));
        var casePicker = new OptionButton { CustomMinimumSize = new Vector2(300, 55) };
        casePicker.AddItem("STORE", 0);
        casePicker.AddItem("små", 1);
        casePicker.AddItem("Blandet", 2);
        casePicker.Selected = progress.DisplayCase switch { "lower" => 1, "mixed" => 2, _ => 0 };
        casePicker.ItemSelected += index =>
        {
            progress.DisplayCase = index switch { 1 => "lower", 2 => "mixed", _ => "upper" };
            save();
        };
        column.AddChild(casePicker);

        column.AddChild(UiStyle.Label($"Manglende lydoptagelser: {audioWarnings.Count}", 20, UiStyle.Sky));
        foreach (var warning in audioWarnings.Take(4))
            column.AddChild(UiStyle.Label(warning, 15));
        if (audioWarnings.Count > 4)
            column.AddChild(UiStyle.Label($"... og {audioWarnings.Count - 4} mere (se Godot-log)", 16));
        column.AddChild(UiStyle.Label("Dansk lyd og ord skal gennemgås af en dansktalende voksen.", 18));

        var resetButton = UiStyle.Button("Nulstil læring", 22);
        var confirm = UiStyle.Button("Bekræft nulstilling", 22);
        confirm.Visible = false;
        resetButton.Pressed += () => confirm.Visible = true;
        confirm.Pressed += () => { reset(); confirm.Visible = false; };
        column.AddChild(resetButton);
        column.AddChild(confirm);
        var backButton = UiStyle.Button("Tilbage", 22);
        backButton.Pressed += back;
        column.AddChild(backButton);
    }
}
