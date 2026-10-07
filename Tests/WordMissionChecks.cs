using System;
using System.IO;
using System.Linq;
using GameBox.Core;
using GameBox.Games.WordMission.Learning;

public static class WordMissionChecks
{
    public static void Run(Action<bool, string> check)
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "starter.json"));
        var content = LearningContent.Parse(json);
        check(ContentValidator.Validate(content, "starter.json").Count == 0,
            "starter educational data validates");
        var duplicate = LearningContent.Parse(json);
        duplicate.Letters.Add(new LetterDefinition { Id = duplicate.Letters[0].Id });
        check(ContentValidator.Validate(duplicate, "bad.json").Any(error => error.Contains("duplicate id")),
            "duplicate learning ID is rejected");
        var unknown = LearningContent.Parse(json);
        unknown.Words[0].LetterIds[0] = "letter_missing";
        check(ContentValidator.Validate(unknown, "bad.json").Any(error => error.Contains("unknown letter")),
            "unknown letter reference is rejected");
        var invalidWord = LearningContent.Parse(json);
        invalidWord.Words[0].Display = "";
        check(ContentValidator.Validate(invalidWord, "bad.json").Any(error => error.Contains("invalid display")),
            "invalid word definition is rejected");
        var unknownSound = LearningContent.Parse(json);
        unknownSound.Words[0].SoundIds[0] = "sound_missing";
        check(ContentValidator.Validate(unknownSound, "bad.json").Any(error => error.Contains("unknown sound")),
            "unknown sound reference is rejected");
        var missingList = LearningContent.Parse("{\"letters\":null}");
        check(ContentValidator.Validate(missingList, "bad.json").Any(error => error.Contains("required content lists")),
            "missing content lists are reported without crashing");

        var progress = new WordMissionProgress();
        for (var i = 0; i < 3; i++) progress.Record("letter_i", 0, false, DateTime.UtcNow);
        check(progress.Get("letter_i").Mastery == MasteryState.Mastered,
            "repeated first-try answers reach mastery");
        progress.Record("letter_i", 3, true, DateTime.UtcNow);
        check(progress.Get("letter_i").Mastery == MasteryState.Practicing
            && progress.Get("letter_i").CorrectAfterHelp == 1 && progress.AssistedAnswers == 1,
            "assisted difficulty moves mastered content back into practice");
        var stages = new WordMissionProgress();
        foreach (var id in new[] { "letter_i", "letter_s", "letter_n" })
            for (var i = 0; i < 3; i++) stages.Record(id, 0, false, DateTime.UtcNow);
        stages.FinishSession(content);
        check(stages.UnlockedStage == 2, "letter mastery opens short words");
        for (var i = 0; i < 3; i++) stages.Record("word_is", 0, false, DateTime.UtcNow);
        stages.FinishSession(content);
        check(stages.UnlockedStage == 3, "short-word mastery opens longer words");
        stages.SoundEnabled = false;
        stages.ResetLearning();
        check(stages.UnlockedStage == 1 && stages.Items.Count == 0 && !stages.SoundEnabled,
            "learning reset preserves parent settings");

        var generator = new SessionGenerator();
        var first = generator.Generate(content, new WordMissionProgress(), content.SessionLength, 1, 42);
        check(first.Count == 9 && first.Select(challenge => challenge.Activity).Distinct().Count() == 5,
            "first session has nine varied challenges");
        check(first.Select(challenge => challenge.ItemId).Distinct().Count() <= content.MaxNewItemsPerSession,
            "first session limits new target items");
        check(first.All(challenge => challenge.Choices.Contains(challenge.ItemId)
            || challenge.Activity is ActivityKind.BuildWord or ActivityKind.WordGate),
            "every choice challenge contains the target");
        check(first.Where(challenge => challenge.Activity is ActivityKind.BuildWord or ActivityKind.WordGate)
            .All(challenge => challenge.Choices.Count == content.Word(challenge.ItemId).LetterIds.Count),
            "word building uses each word's data-defined letters");
        var sameSeed = generator.Generate(content, new WordMissionProgress(), content.SessionLength, 1, 42);
        check(first.Select(challenge => $"{challenge.Activity}:{challenge.ItemId}:{string.Join(',', challenge.Choices)}")
            .SequenceEqual(sameSeed.Select(challenge =>
                $"{challenge.Activity}:{challenge.ItemId}:{string.Join(',', challenge.Choices)}")),
            "session selection is deterministic with a seed");

        var weighted = new WordMissionProgress();
        foreach (var letter in content.Letters)
            weighted.Items[letter.Id] = new LearningItemProgress
            {
                Attempts = 5, Mastery = MasteryState.Mastered, LastSeenUtc = DateTime.UtcNow
            };
        foreach (var word in content.Words)
            weighted.Items[word.Id] = new LearningItemProgress
            {
                Attempts = 5, Mastery = MasteryState.Mastered, LastSeenUtc = DateTime.UtcNow
            };
        weighted.Items["letter_i"].LastOutcomeDifficult = true;
        weighted.Items["letter_i"].Mastery = MasteryState.Practicing;
        var draws = Enumerable.Range(0, 70).SelectMany(seed => generator.Generate(content, weighted, 9, 2, seed))
            .Where(challenge => challenge.Activity == ActivityKind.FindSound).ToList();
        check(draws.Count(challenge => challenge.ItemId == "letter_i")
            > draws.Count(challenge => challenge.ItemId == "letter_s") * 3,
            "struggling sounds recur more than recently mastered sounds");

        var saved = new GameBoxState();
        saved.WordMission.Record("word_is", 0, false, DateTime.UtcNow);
        saved.WordMission.CompletedSessions = 2;
        var loaded = GameBoxState.FromJson(saved.ToJson());
        check(loaded.WordMission.Get("word_is").Attempts == 1
            && loaded.WordMission.CompletedSessions == 2,
            "Ordmission progress survives save and load");
        check(GameBoxState.FromJson("{\"ChildName\":\"Mia\"}").WordMission.UnlockedStage == 1,
            "older saves without Ordmission fields remain valid");
        check(loaded.WordMission.Get("future_word").Attempts == 0,
            "new content IDs can be added without invalidating progress");
    }
}
