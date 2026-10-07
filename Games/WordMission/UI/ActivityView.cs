using System;
using System.Collections.Generic;
using System.Linq;
using GameBox.Games.WordMission.Audio;
using GameBox.Games.WordMission.Learning;
using GameBox.Shared.Audio;
using GameBox.Shared.UI;
using Godot;

namespace GameBox.Games.WordMission.UI;

public partial class ActivityView : Control
{
    private readonly Dictionary<string, List<Button>> _buttons = new(StringComparer.Ordinal);
    private readonly List<Button> _letterButtons = new();
    private readonly List<string> _placed = new();
    private LearningContent _content = null!;
    private WordMissionProgress _progress = null!;
    private WordAudioService _audio = null!;
    private Action<int, bool> _completedCallback = null!;
    private Label _feedback = null!;
    private Label _slots = null!;
    private GateVisual? _gate;
    private ToyCannonVisual? _cannon;
    private int _wrongAttempts;
    private bool _finished;

    public LearningChallenge Challenge { get; private set; } = null!;
    public int WrongAttempts => _wrongAttempts;

    public void Show(LearningChallenge challenge, LearningContent content,
        WordMissionProgress progress, WordAudioService audio, Action<int, bool> completed)
    {
        Challenge = challenge;
        _content = content;
        _progress = progress;
        _audio = audio;
        _completedCallback = completed;
        _wrongAttempts = 0;
        _finished = false;
        _placed.Clear();
        _buttons.Clear();
        _letterButtons.Clear();
        _cannon = null;
        foreach (var child in GetChildren()) { RemoveChild(child); child.QueueFree(); }

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(720, 0) };
        column.AddThemeConstantOverride("separation", 16);
        center.AddChild(column);
        var title = challenge.Activity switch
        {
            ActivityKind.FindSound => "FIND LYDEN",
            ActivityKind.BuildWord => "BYG ORDET",
            ActivityKind.ChooseWord => "HVILKET ORD?",
            ActivityKind.WordGate => "ORDPORTEN",
            _ => "TANKMISSION"
        };
        column.AddChild(UiStyle.Label(title, 44, UiStyle.Gold));

        if (challenge.Activity == ActivityKind.FindSound)
            BuildSound(column);
        else if (challenge.Activity is ActivityKind.BuildWord or ActivityKind.WordGate)
            BuildWord(column);
        else
            BuildChoice(column);

        _feedback = UiStyle.Label("", 24, UiStyle.Sky);
        column.AddChild(_feedback);
    }

    public void SubmitChoice(string id)
    {
        if (_finished || GetTree().Paused || !_buttons.TryGetValue(id, out var buttons)) return;
        var button = buttons.FirstOrDefault(candidate => !candidate.Disabled);
        if (button is null) return;
        if (Challenge.Activity is ActivityKind.BuildWord or ActivityKind.WordGate)
            PlaceLetter(id, button);
        else
            SelectOption(id, button);
    }

    private void BuildSound(VBoxContainer column)
    {
        var letter = _content.Letter(Challenge.ItemId);
        var sound = _content.Sound(letter.PrimarySoundId);
        column.AddChild(UiStyle.Label("Hjælp robotten med at finde lyden", 25));
        var cue = _audio.Enabled && ResourceLoader.Exists(sound.Audio ?? "")
            ? "Lyt til lyden" : $"Lyd mangler · testvisning: {sound.DisplayCue}";
        column.AddChild(UiStyle.Label(cue, 24, UiStyle.Sky));
        AddRepeatButton(column, sound.Audio, () => _audio.PlaySound(sound));
        var row = ChoiceRow(column);
        for (var index = 0; index < Challenge.Choices.Count; index++)
        {
            var choice = _content.Letter(Challenge.Choices[index]);
            var id = choice.Id;
            AddChoice(row, id, DisplayLetter(choice, index), 56);
        }
        _audio.PlaySound(sound);
    }

    private void BuildWord(VBoxContainer column)
    {
        var word = _content.Word(Challenge.ItemId);
        if (Challenge.Activity == ActivityKind.WordGate)
        {
            _gate = new GateVisual { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
            column.AddChild(_gate);
            column.AddChild(UiStyle.Label("Byg ordet og åbn porten!", 25));
        }
        else
        {
            _gate = null;
            column.AddChild(UiStyle.Label("Byg ordet til billedet", 25));
        }
        column.AddChild(Picture(word));
        AddRepeatButton(column, word.WordAudio, () => _audio.PlayWord(word));
        _slots = UiStyle.Label(SlotText(word), 48, UiStyle.Gold);
        column.AddChild(_slots);
        var row = ChoiceRow(column);
        for (var index = 0; index < Challenge.Choices.Count; index++)
        {
            var letter = _content.Letter(Challenge.Choices[index]);
            var button = AddChoice(row, letter.Id, DisplayLetter(letter, index), 48);
            _letterButtons.Add(button);
        }
        _audio.PlayWord(word);
    }

    private void BuildChoice(VBoxContainer column)
    {
        var word = _content.Word(Challenge.ItemId);
        column.AddChild(UiStyle.Label(Challenge.Activity == ActivityKind.TankWord
            ? "Tryk på det rigtige mål!" : "Find ordet til billedet", 25));
        column.AddChild(Picture(word));
        AddRepeatButton(column, word.WordAudio, () => _audio.PlayWord(word));
        if (Challenge.Activity == ActivityKind.TankWord)
        {
            _cannon = new ToyCannonVisual { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
            column.AddChild(_cannon);
        }
        var row = ChoiceRow(column);
        foreach (var id in Challenge.Choices)
        {
            var option = _content.Word(id);
            AddChoice(row, id, _progress.DisplayCase == "lower"
                ? option.Display.ToLowerInvariant() : option.Display, 37);
        }
        _audio.PlayWord(word);
    }

    private static HFlowContainer ChoiceRow(VBoxContainer column)
    {
        var row = new HFlowContainer { Alignment = FlowContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("h_separation", 18);
        row.AddThemeConstantOverride("v_separation", 18);
        column.AddChild(row);
        return row;
    }

    private static Control Picture(WordDefinition word)
    {
        if (!string.IsNullOrWhiteSpace(word.Image) && ResourceLoader.Exists(word.Image))
        {
            return new TextureRect
            {
                Texture = GD.Load<Texture2D>(word.Image),
                CustomMinimumSize = new Vector2(190, 170),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter
            };
        }
        return new WordVisual { VisualId = word.VisualId, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
    }

    private Button AddChoice(HFlowContainer row, string id, string text, int fontSize)
    {
        var button = UiStyle.Button(text, fontSize);
        button.CustomMinimumSize = Challenge.Activity == ActivityKind.TankWord
            ? new Vector2(205, 128) : new Vector2(160, 100);
        SoundEffects.BindHover(button);
        button.Pressed += () => SubmitChoice(id);
        row.AddChild(button);
        if (!_buttons.TryGetValue(id, out var list)) _buttons[id] = list = new List<Button>();
        list.Add(button);
        return button;
    }

    private void AddRepeatButton(VBoxContainer column, string? path, Action play)
    {
        if (!_audio.Enabled)
        {
            column.AddChild(UiStyle.Label("Lyd er slået fra", 19, UiStyle.Sky));
            return;
        }
        if (string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path))
        {
            column.AddChild(UiStyle.Label("Lydoptagelse kommer senere", 19, UiStyle.Sky));
            return;
        }
        var button = UiStyle.Button("▶ Hør igen", 22);
        button.CustomMinimumSize = new Vector2(200, 54);
        button.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        button.Pressed += play;
        column.AddChild(button);
    }

    private string DisplayLetter(LetterDefinition letter, int index) => _progress.DisplayCase switch
    {
        "lower" => letter.Lowercase,
        "mixed" => index % 2 == 0 ? letter.Uppercase : letter.Lowercase,
        _ => letter.Uppercase
    };

    private string SlotText(WordDefinition word) => string.Join("  ",
        word.LetterIds.Select((id, index) => index < _placed.Count
            ? DisplayLetter(_content.Letter(_placed[index]), index) : "_") );

    private void PlaceLetter(string id, Button button)
    {
        var word = _content.Word(Challenge.ItemId);
        var index = _placed.Count;
        if (id != word.LetterIds[index])
        {
            Wrong(button);
            if (_wrongAttempts >= 3) FinishWord(true);
            return;
        }
        _placed.Add(id);
        button.Disabled = true;
        _slots.Text = SlotText(word);
        _audio.PlaySound(_content.Sound(word.SoundIds[index]));
        if (_placed.Count == word.LetterIds.Count) FinishWord(_wrongAttempts >= 2);
    }

    private void SelectOption(string id, Button button)
    {
        _cannon?.Fire();
        if (id == Challenge.ItemId)
        {
            button.Modulate = new Color("#94f2a9");
            if (Challenge.Activity == ActivityKind.TankWord)
            {
                button.Text = "✦";
                button.CreateTween().TweenProperty(button, "scale", new Vector2(0.15f, 0.15f), 0.35f);
            }
            Finish(_wrongAttempts >= 2);
        }
        else
        {
            Wrong(button);
            if (_wrongAttempts >= 3) Finish(true);
        }
    }

    private void Wrong(Button button)
    {
        _wrongAttempts++;
        _feedback.Text = _wrongAttempts == 1 ? "Prøv igen · hør og kig en gang til"
            : "Her er lidt hjælp. Du kan prøve igen!";
        if (_audio.Enabled && Challenge.Activity == ActivityKind.TankWord)
            SoundEffects.Play("impact_wall", -13f);
        if (Challenge.Activity == ActivityKind.TankWord)
        {
            var bounce = button.CreateTween();
            bounce.TweenProperty(button, "scale", new Vector2(1.12f, 0.88f), 0.1f);
            bounce.TweenProperty(button, "scale", Vector2.One, 0.16f);
        }
        if (_wrongAttempts >= 2)
        {
            var correctId = Challenge.Activity is ActivityKind.BuildWord or ActivityKind.WordGate
                ? _content.Word(Challenge.ItemId).LetterIds[_placed.Count] : Challenge.ItemId;
            if (Challenge.Activity is not (ActivityKind.BuildWord or ActivityKind.WordGate))
                button.Disabled = true;
            if (_buttons.TryGetValue(correctId, out var correct))
                foreach (var target in correct) target.Modulate = UiStyle.Sky;
        }
        if (Challenge.Activity == ActivityKind.FindSound)
        {
            var letter = _content.Letter(Challenge.ItemId);
            _audio.PlaySound(_content.Sound(letter.PrimarySoundId));
        }
        else _audio.PlayWord(_content.Word(Challenge.ItemId));
    }

    private async void FinishWord(bool assisted)
    {
        if (_finished) return;
        _finished = true;
        var word = _content.Word(Challenge.ItemId);
        _feedback.Text = assisted ? "Vi bygger ordet sammen!" : "Maskinen virker!";
        for (var index = 0; index < word.LetterIds.Count; index++)
        {
            _placed.Clear();
            _placed.AddRange(word.LetterIds.Take(index + 1));
            _slots.Text = SlotText(word);
            _audio.PlaySound(_content.Sound(word.SoundIds[index]));
            await ToSignal(GetTree().CreateTimer(0.28, false), SceneTreeTimer.SignalName.Timeout);
        }
        _slots.Text = _progress.DisplayCase == "lower"
            ? word.Display.ToLowerInvariant() : word.Display;
        _gate?.Open();
        _audio.PlayWord(word);
        if (_audio.Enabled) SoundEffects.Play("ui_select", -11f);
        await ToSignal(GetTree().CreateTimer(0.8, false), SceneTreeTimer.SignalName.Timeout);
        _completedCallback(_wrongAttempts, assisted);
    }

    private async void Finish(bool assisted)
    {
        if (_finished) return;
        _finished = true;
        _feedback.Text = assisted ? "Sådan! Vi fandt den sammen." : "Godt fundet!";
        if (_audio.Enabled) SoundEffects.Play(Challenge.Activity == ActivityKind.TankWord
            ? "tank_explosion" : "ui_select", -11f);
        await ToSignal(GetTree().CreateTimer(0.8, false), SceneTreeTimer.SignalName.Timeout);
        _completedCallback(_wrongAttempts, assisted);
    }
}
