using System;
using System.Collections.Generic;
using System.Linq;
using GameBox.Core;
using GameBox.Games.WordMission.Audio;
using GameBox.Games.WordMission.Learning;
using GameBox.Games.WordMission.UI;
using GameBox.Shared.Audio;
using GameBox.Shared.UI;
using Godot;

namespace GameBox.Games.WordMission;

public partial class WordMission : Control
{
    private const string ContentPath = "res://Games/WordMission/Data/starter.json";
    private readonly SessionGenerator _generator = new();
    private LearningContent _content = null!;
    private GameBoxState _state = null!;
    private WordAudioService _audio = null!;
    private Control _body = null!;
    private Control _pauseOverlay = null!;
    private IReadOnlyList<LearningChallenge> _session = Array.Empty<LearningChallenge>();
    private int _challengeIndex;
    private bool _sessionActive;
    private bool _debugVisible;

    public ActivityView? CurrentActivityView { get; private set; }
    public int ChallengeIndex => _challengeIndex;
    public bool SessionActive => _sessionActive;
    public int CompletedSessions => _state.WordMission.CompletedSessions;
    public LearningContent Content => _content;
    public string? SavePathOverride { get; set; }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _state = GameBoxState.Load(SavePath());
        _state.WordMission.UnlockedStage = Math.Clamp(_state.WordMission.UnlockedStage, 1, 4);
        if (_state.WordMission.DisplayCase is not ("upper" or "lower" or "mixed"))
            _state.WordMission.DisplayCase = "upper";
        _audio = new WordAudioService { Enabled = _state.WordMission.SoundEnabled };
        AddChild(_audio);
        BuildShell();
        try
        {
            using var file = Godot.FileAccess.Open(ContentPath, Godot.FileAccess.ModeFlags.Read);
            if (file is null) throw new Exception($"Cannot open {ContentPath}");
            _content = LearningContent.Parse(file.GetAsText());
            var errors = ContentValidator.Validate(_content, ContentPath);
            if (errors.Count > 0) throw new Exception(string.Join("\n", errors));
            _audio.Inspect(_content);
            foreach (var word in _content.Words.Where(word => !string.IsNullOrWhiteSpace(word.Image)
                && !ResourceLoader.Exists(word.Image)))
                GD.PushWarning($"Ordmission image: {word.Id} is missing {word.Image}");
            ShowMenu();
            _audio.PlayInstruction(_content);
        }
        catch (Exception error)
        {
            GD.PushError("Ordmission content: " + error);
            var column = NewScreen("Indholdet kan ikke åbnes");
            column.AddChild(UiStyle.Label("En voksen skal se på indholdsfilen.", 24));
            AddButton(column, "Tilbage til Game Box", ReturnToBox);
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;
        if (key.Keycode == Key.Escape)
        {
            if (_pauseOverlay.Visible) Resume(); else Pause();
            GetViewport().SetInputAsHandled();
        }
        else if (key.Keycode == Key.F9 && OS.IsDebugBuild())
        {
            _debugVisible = !_debugVisible;
            if (_debugVisible) ShowDebug(); else ShowMenu();
            GetViewport().SetInputAsHandled();
        }
    }

    public void StartSession(int? seed = null)
    {
        _session = _generator.Generate(_content, _state.WordMission,
            _content.SessionLength, _state.WordMission.UnlockedStage, seed ?? System.Environment.TickCount);
        _challengeIndex = 0;
        _sessionActive = true;
        ShowChallenge();
    }

    private void ShowChallenge()
    {
        ClearBody();
        var column = new VBoxContainer();
        column.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        column.AddThemeConstantOverride("separation", 6);
        _body.AddChild(column);
        column.AddChild(UiStyle.Label($"MISSION {_challengeIndex + 1} / {_session.Count}", 22, UiStyle.Sky));
        CurrentActivityView = new ActivityView { SizeFlagsVertical = SizeFlags.ExpandFill };
        column.AddChild(CurrentActivityView);
        CurrentActivityView.Show(_session[_challengeIndex], _content, _state.WordMission,
            _audio, CompleteChallenge);
    }

    private void CompleteChallenge(int wrongAttempts, bool assisted)
    {
        if (!_sessionActive) return;
        _state.WordMission.Record(_session[_challengeIndex].ItemId, wrongAttempts, assisted, DateTime.UtcNow);
        Save();
        _challengeIndex++;
        if (_challengeIndex < _session.Count) ShowChallenge();
        else FinishSession();
    }

    private void FinishSession()
    {
        _sessionActive = false;
        CurrentActivityView = null;
        var oldStage = _state.WordMission.UnlockedStage;
        _state.WordMission.FinishSession(_content);
        Save();
        _audio.PlaySuccess(_content);
        var column = NewScreen("Godt gået!");
        column.AddChild(UiStyle.Label("Robotten er klar til næste mission!", 27, UiStyle.Sky));
        if (_state.WordMission.UnlockedStage > oldStage)
            column.AddChild(UiStyle.Label("Et nyt område er åbnet!", 28, UiStyle.Gold));
        column.AddChild(UiStyle.Label("★ Ordmission gennemført", 26, UiStyle.Gold));
        AddButton(column, "Spil igen", () => StartSession());
        AddButton(column, "Se min vej", ShowProgress);
        AddButton(column, "Tilbage til Game Box", ReturnToBox);
    }

    private void ShowMenu()
    {
        _debugVisible = false;
        _sessionActive = false;
        CurrentActivityView = null;
        var column = NewScreen("ORDMISSION");
        column.AddChild(UiStyle.Label("Hjælp robotten gennem ordlandet!", 26, UiStyle.Sky));
        AddMap(column);
        AddButton(column, "Start mission", () => StartSession());
        AddButton(column, "Min vej", ShowProgress);
        AddButton(column, "For voksne", ShowParent);
        AddButton(column, "Tilbage til Game Box", ReturnToBox);
    }

    private void AddMap(VBoxContainer column)
    {
        var row = new HFlowContainer { Alignment = FlowContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("h_separation", 10);
        column.AddChild(row);
        foreach (var stage in _content.Stages)
        {
            if (stage.Id > 1)
                row.AddChild(UiStyle.Label("→", 26, UiStyle.Sky));
            var available = stage.Id <= _state.WordMission.UnlockedStage;
            var label = UiStyle.Label((available ? "★ " : "▣ ") + stage.Title, 20,
                available ? UiStyle.Gold : new Color("#71889a"));
            label.CustomMinimumSize = new Vector2(166, 78);
            label.AddThemeStyleboxOverride("normal", UiStyle.Box(UiStyle.Panel));
            row.AddChild(label);
        }
    }

    private void ShowProgress()
    {
        var column = NewScreen("MIN VEJ");
        foreach (var stage in _content.Stages)
        {
            var learned = stage.Id == 1
                ? _content.Letters.Count(letter => _state.WordMission.Get(letter.Id).Mastery == MasteryState.Mastered)
                : _content.Words.Count(word => word.Stage == stage.Id
                    && _state.WordMission.Get(word.Id).Mastery == MasteryState.Mastered);
            column.AddChild(UiStyle.Label(stage.Id <= _state.WordMission.UnlockedStage
                ? $"{stage.Title}  {new string('★', Math.Min(learned, 6))}"
                : $"{stage.Title}  LÅST", 27, UiStyle.Gold));
        }
        AddButton(column, "Tilbage", ShowMenu);
    }

    private void ShowParent()
    {
        ClearBody();
        var parent = new ParentView();
        parent.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _body.AddChild(parent);
        parent.Show(_content, _state.WordMission, _audio.Warnings,
            () => { _audio.Enabled = _state.WordMission.SoundEnabled; Save(); },
            () => { _state.WordMission.ResetLearning(); Save(); ShowParent(); }, ShowMenu);
    }

    private void ShowDebug()
    {
        var column = NewScreen("DEV · ORDMISSION");
        column.AddChild(UiStyle.Label("F9 lukker · kun i debug-build", 18));
        column.AddChild(UiStyle.Label($"Stage: {_state.WordMission.UnlockedStage}", 23));
        AddButton(column, "Næste trin", () =>
        {
            _state.WordMission.UnlockedStage = Math.Min(4, _state.WordMission.UnlockedStage + 1);
            Save(); ShowDebug();
        });
        var item = new OptionButton { CustomMinimumSize = new Vector2(320, 55) };
        foreach (var id in _content.Letters.Select(letter => letter.Id)
            .Concat(_content.Words.Select(word => word.Id))) item.AddItem(id);
        column.AddChild(item);
        var activity = new OptionButton { CustomMinimumSize = new Vector2(320, 55) };
        foreach (var kind in Enum.GetNames<ActivityKind>()) activity.AddItem(kind);
        column.AddChild(activity);
        AddButton(column, "Afspil bogstavnavn", () =>
        {
            var id = item.GetItemText(item.Selected);
            var letter = _content.Letters.FirstOrDefault(candidate => candidate.Id == id);
            if (letter is not null) _audio.PlayLetterName(letter);
        });
        AddButton(column, "Start valgt aktivitet", () =>
        {
            var id = item.GetItemText(item.Selected);
            var kind = (ActivityKind)activity.Selected;
            ForceChallenge(kind, id);
        });
        AddButton(column, "Marker lært / øv igen", () =>
        {
            var id = item.GetItemText(item.Selected);
            var record = _state.WordMission.Items.TryGetValue(id, out var current)
                ? current : _state.WordMission.Items[id] = new LearningItemProgress();
            record.Mastery = record.Mastery == MasteryState.Mastered
                ? MasteryState.Practicing : MasteryState.Mastered;
            record.MasteryScore = record.Mastery == MasteryState.Mastered ? 8 : 2;
            record.CorrectFirstTry = record.Mastery == MasteryState.Mastered ? 3 : 0;
            Save(); ShowDebug();
        });
        AddButton(column, "Nulstil læring", () => { _state.WordMission.ResetLearning(); Save(); ShowDebug(); });
        column.AddChild(UiStyle.Label($"Manglende lydfiler: {_audio.Warnings.Count}", 18));
        foreach (var warning in _audio.Warnings.Take(5))
            column.AddChild(UiStyle.Label(warning, 15));
        AddButton(column, "Tilbage", ShowMenu);
    }

    public void ForceChallenge(ActivityKind activity, string id)
    {
        if (!OS.IsDebugBuild()) return;
        var isLetter = _content.Letters.Any(letter => letter.Id == id);
        if (isLetter != (activity == ActivityKind.FindSound)) return;
        IReadOnlyList<string> choices = isLetter
            ? _content.Letters.Where(letter => letter.Id != id).Take(2)
                .Select(letter => letter.Id).Append(id).ToList()
            : activity is ActivityKind.BuildWord or ActivityKind.WordGate
                ? _content.Word(id).LetterIds
                : _content.Words.Where(word => word.Id != id).Take(2)
                    .Select(word => word.Id).Append(id).ToList();
        _session = new[] { new LearningChallenge(activity, id, choices) };
        _challengeIndex = 0;
        _sessionActive = true;
        ShowChallenge();
    }

    private void BuildShell()
    {
        AddChild(UiStyle.Backdrop(UiStyle.Night));
        var layout = new VBoxContainer();
        layout.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(layout);
        _body = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
        layout.AddChild(_body);

        _pauseOverlay = new Control { Visible = false, ProcessMode = ProcessModeEnum.Always };
        _pauseOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_pauseOverlay);
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.82f), MouseFilter = MouseFilterEnum.Stop };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _pauseOverlay.AddChild(dim);
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _pauseOverlay.AddChild(center);
        var pauseMenu = new VBoxContainer();
        pauseMenu.AddThemeConstantOverride("separation", 20);
        center.AddChild(pauseMenu);
        pauseMenu.AddChild(UiStyle.Label("PAUSE", 52, UiStyle.Gold));
        AddButton(pauseMenu, "Fortsæt", Resume);
        AddButton(pauseMenu, "Tilbage til Ordmission", () => { Resume(); ShowMenu(); });
        AddButton(pauseMenu, "Tilbage til Game Box", ReturnToBox);
    }

    private VBoxContainer NewScreen(string title)
    {
        ClearBody();
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _body.AddChild(center);
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(700, 0) };
        column.AddThemeConstantOverride("separation", 18);
        center.AddChild(column);
        column.AddChild(UiStyle.Label(title, 50, UiStyle.Gold));
        return column;
    }

    private void ClearBody()
    {
        foreach (var child in _body.GetChildren()) { _body.RemoveChild(child); child.QueueFree(); }
        CurrentActivityView = null;
    }

    private static Button AddButton(VBoxContainer parent, string text, Action action)
    {
        var button = UiStyle.Button(text, 25);
        button.CustomMinimumSize = new Vector2(360, 64);
        button.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        SoundEffects.BindHover(button);
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    private void Pause()
    {
        _pauseOverlay.Visible = true;
        if (_audio.Enabled) SoundEffects.Play("pause", -12f);
        GetTree().Paused = true;
    }

    private void Resume()
    {
        GetTree().Paused = false;
        _pauseOverlay.Visible = false;
        if (_audio.Enabled) SoundEffects.Play("resume", -12f);
    }

    private void ReturnToBox()
    {
        GetTree().Paused = false;
        GetTree().ChangeSceneToFile("res://Core/MainMenu.tscn");
    }

    private void Save()
    {
        try { _state.Save(SavePath()); }
        catch (Exception error) { GD.PushWarning("Ordmission progress could not be saved: " + error.Message); }
    }

    private string SavePath() => SavePathOverride ?? ProjectSettings.GlobalizePath("user://game_box_state.json");
}
